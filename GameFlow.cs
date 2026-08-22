using System.Diagnostics;

namespace 脚本.Flows
{
    /// <summary>
    /// 游戏流程上下文：把"截图 / 点击 / 识别 / 等待 / 停止检查"等交互能力收拢为统一入口，
    /// 业务流程只依赖此抽象，不直接触碰 Form1 与具体控件。
    /// </summary>
    public sealed class GameFlowContext
    {
        private readonly LdPlayerCapturer _capturer;
        private readonly GetNumberRecognizer _recognizer;
        private readonly Random _random;
        private readonly Func<bool> _isRunning;
        private readonly Func<Func<object>, object>? _readUi;

        public LdPlayerCapturer Capturer => _capturer;
        public GetNumberRecognizer Recognizer => _recognizer;
        public TemplateMatcher SatelliteMatcher { get; }
        public TemplateMatcher GunMatcher { get; }
        public TemplateMatcher ResTitleMatcher { get; }

        /// <summary>卫星站记忆位置（拖拽后画面确定，识别一次后快路径复用）</summary>
        public (int x, int y)? SatellitePos { get; set; }

        /// <summary>卫星站校验倒计时（每 N 轮强制重新识别一次，防位置漂移）</summary>
        public int SatelliteCheckCountdown { get; set; }

        /// <summary>
        /// 读取第 index 个英雄槽位是否被勾选（由 Form1 注入 UI 读取逻辑；测试/无 UI 环境恒为 false）
        /// </summary>
        public Func<int, bool> HeroChecked { get; set; } = _ => false;

        // 设备/识别依赖在业务流程中始终由 Form1 注入真实实现；
        // 单元测试传入轻量实例（LdPlayerCapturer 构造无副作用）。
        public GameFlowContext(
            LdPlayerCapturer capturer,
            GetNumberRecognizer recognizer,
            TemplateMatcher satelliteMatcher,
            TemplateMatcher gunMatcher,
            TemplateMatcher resTitleMatcher,
            Random random,
            Func<bool> isRunning,
            Func<Func<object>, object>? readUi = null)
        {
            _capturer = capturer ?? throw new ArgumentNullException(nameof(capturer));
            _recognizer = recognizer ?? throw new ArgumentNullException(nameof(recognizer));
            SatelliteMatcher = satelliteMatcher ?? throw new ArgumentNullException(nameof(satelliteMatcher));
            GunMatcher = gunMatcher ?? throw new ArgumentNullException(nameof(gunMatcher));
            ResTitleMatcher = resTitleMatcher ?? throw new ArgumentNullException(nameof(resTitleMatcher));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _isRunning = isRunning ?? throw new ArgumentNullException(nameof(isRunning));
            _readUi = readUi;
        }

        /// <summary>用户是否请求了停止</summary>
        public bool ShouldStop => !_isRunning();

        /// <summary>在 UI 线程安全读取控件值（控制台/测试环境下无 UI 则直接求值）</summary>
        public T ReadUi<T>(Func<T> getter)
        {
            if (_readUi == null) return getter();
            return (T)_readUi(() => getter()!);
        }

        /// <summary>全屏截图（调用方负责 Dispose）</summary>
        public Bitmap Capture() => _capturer.CaptureToBitmap();

        /// <summary>带随机偏移的点击（模拟人手抖动）</summary>
        public void Tap((int x, int y) position, int jitterX = 0, int jitterY = 0)
        {
            if (jitterX == 0 && jitterY == 0)
            {
                _capturer.Tap(position.x, position.y);
                return;
            }
            int ox = _random.Next(-jitterX, jitterX + 1);
            int oy = _random.Next(-jitterY, jitterY + 1);
            _capturer.Tap(position.x + ox, position.y + oy);
        }

        /// <summary>拖拽（起终点可加随机扰动）</summary>
        public void Drag((int x, int y) start, (int x, int y) end, int jitterX = 0, int jitterY = 0, bool endJitterWide = false)
        {
            int sx = start.x + (_random.Next(0, Math.Max(1, jitterX + 1)));
            int sy = start.y + (_random.Next(0, Math.Max(1, jitterY + 1)));
            int ex = end.x + _random.Next(0, Math.Max(1, jitterX + 1));
            int ey = end.y + _random.Next(endJitterWide ? -200 : -jitterY, jitterY + 1);
            _capturer.Drag(sx, sy, ex, ey);
        }

        /// <summary>
        /// 随机时长等待。
        /// 返回 false 表示等待期间用户请求停止（提前中断），调用方应尽快退出流程。
        /// </summary>
        public bool Sleep(int minMs, int maxMs)
        {
            int ms = _random.Next(minMs, maxMs + 1);
            const int slice = 100;
            int waited = 0;
            while (waited < ms)
            {
                if (ShouldStop) return false;
                int step = Math.Min(slice, ms - waited);
                Thread.Sleep(step);
                waited += step;
            }
            return !ShouldStop;
        }

        /// <summary>带耗时日志的动作执行辅助</summary>
        public void Timed(string label, Action action)
        {
            var sw = Stopwatch.StartNew();
            action();
            Debug.WriteLine($"[耗时] {label}: {sw.ElapsedMilliseconds}ms");
        }
    }

    /// <summary>
    /// 流程与 UI 的桥接：Form1 启动流程时注入，用于在 UI 上展示进度标题。
    /// </summary>
    public static class FlowUiBridge
    {
        /// <summary>设置主窗体标题文本（Form1 注入；null 表示无 UI 环境）</summary>
        public static Action<string>? SetTitle { get; set; }
    }

    /// <summary>
    /// 流程步骤：名字（日志标识）+ 执行体（返回是否成功）+ 最大重试次数。
    /// </summary>
    public sealed class GameStep
    {
        public string Name { get; }
        private readonly Func<GameFlowContext, bool> _execute;
        public int MaxRetries { get; }

        public GameStep(string name, Func<GameFlowContext, bool> execute, int maxRetries = 1)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            MaxRetries = Math.Max(1, maxRetries);
        }

        /// <summary>执行步骤（内部按 MaxRetries 重试），全部失败返回 false</summary>
        internal bool Run(GameFlowContext ctx)
        {
            for (int attempt = 1; attempt <= MaxRetries; attempt++)
            {
                if (ctx.ShouldStop) return false;
                try
                {
                    if (_execute(ctx)) return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[状态:{Name}] 异常(第{attempt}次): {ex.Message}");
                }
                if (attempt < MaxRetries)
                    Debug.WriteLine($"[状态:{Name}] 未成功，重试 {attempt}/{MaxRetries}");
            }
            Debug.WriteLine($"[状态:{Name}] 失败（已重试 {MaxRetries} 次）");
            return false;
        }
    }

    /// <summary>
    /// 步骤执行引擎：顺序执行步骤列表，任一步骤最终失败即中止整个流程（返回 false）。
    /// </summary>
    public sealed class GameFlowRunner
    {
        /// <summary>顺序执行各步骤；被停止或任一步骤失败时立即返回 false</summary>
        public static bool Run(GameFlowContext ctx, IEnumerable<GameStep> steps)
        {
            foreach (var step in steps)
            {
                if (ctx.ShouldStop)
                {
                    Debug.WriteLine("[流程] 用户停止，流程结束");
                    return false;
                }
                Debug.WriteLine($"[状态] {step.Name}");
                if (!step.Run(ctx))
                    return false;
            }
            return true;
        }
    }
}
