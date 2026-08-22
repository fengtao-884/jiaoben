using System.Diagnostics;
using OpenCvSharp;
using 脚本.Flows;

namespace 脚本
{
    /// <summary>
    /// 主窗体：只负责 UI 交互与流程装配。
    /// 业务逻辑全部在 Flows/ 目录的流程类中，通过 GameFlowContext 注入能力运行。
    /// </summary>
    public partial class Form1 : Form
    {
        private LdPlayerCapturer _capturer = null!;
        private GetNumberRecognizer _recognizer = null!;
        private TemplateMatcher _satelliteMatcher = null!;
        private TemplateMatcher _gunMatcher = null!;
        private TemplateMatcher _resTitleMatcher = null!;
        private Random _random = new();
        private bool _isRunning = false;

        public Form1()
        {
            InitializeComponent();

            _capturer = new LdPlayerCapturer();
            _recognizer = new GetNumberRecognizer(_capturer);
            _satelliteMatcher = new TemplateMatcher(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "satellite_base.png"));
            // 命中尺度稳定在 1.10（多轮日志验证），收窄全图扫描范围提速（29尺度→9尺度）
            _satelliteMatcher.MinScale = 0.9;
            _satelliteMatcher.MaxScale = 1.3;
            _satelliteMatcher.ScaleStep = 0.05;
            _gunMatcher = new TemplateMatcher(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "gun.png"));
            // 资源面板标题模板：用于定位"可掠夺资源"面板（位置无关，容忍移动基地/视角漂移）
            _resTitleMatcher = new TemplateMatcher(
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", "res_title.png"));
            _resTitleMatcher.MinScale = 0.9;
            _resTitleMatcher.MaxScale = 1.15;
            _resTitleMatcher.ScaleStep = 0.05;

            this.TopMost = true;
            // 流程 → UI 桥接：流程内更新窗体标题显示进度
            FlowUiBridge.SetTitle = title => BeginInvoke(() => this.Text = title);
            //_capturer.StartAppByLauncher();
        }

        /// <summary>构造流程上下文：把游戏交互能力注入给业务流程</summary>
        private GameFlowContext CreateFlowContext()
        {
            var ctx = new GameFlowContext(
                _capturer,
                _recognizer,
                _satelliteMatcher,
                _gunMatcher,
                _resTitleMatcher,
                _random,
                isRunning: () => _isRunning,
                readUi: getter => Invoke(getter));
            ctx.HeroChecked = ReadHeroChecked;
            return ctx;
        }

        /// <summary>读取第 i 个英雄槽位勾选状态（供战斗流程下英雄）</summary>
        private bool ReadHeroChecked(int index)
        {
            var boxes = new[] { checkBox1, checkBox2, checkBox3, checkBox4, checkBox5 };
            return index >= 0 && index < boxes.Length && ReadUi(() => boxes[index].Checked);
        }

        /// <summary>刷资源：按敌人等级筛选并进攻</summary>
        private async void button1_Click(object sender, EventArgs e)
        {
            _isRunning = true;
            int runCount = ReadUi(() => (int)numRun.Value);
            int maxLevel = ReadUi(() => (int)numLevel.Value);
            await Task.Run(() => FarmByLevelFlow.Run(CreateFlowContext(), runCount, maxLevel));
        }

        /// <summary>打人机资源：资源区间 + 机枪数量双重判定</summary>
        private async void button2_Click(object sender, EventArgs e)
        {
            _isRunning = true;
            int runCount = ReadUi(() => (int)numRun.Value);
            int resMin = ReadUi(() => (int)numResMin.Value);
            int resMax = ReadUi(() => (int)numResMax.Value);
            await Task.Run(() => FarmByLootFlow.Run(CreateFlowContext(), runCount, resMin, resMax));
        }

        /// <summary>波兰守卫：开始防御 → 等"战斗胜利" → 返回</summary>
        private async void button3_Click(object sender, EventArgs e)
        {
            _isRunning = true;
            int runCount = ReadUi(() => (int)numRun.Value);
            await Task.Run(() => GuardFlow.Run(CreateFlowContext(), runCount));
        }

        /// <summary>复仇X：开始防御 → 拖动战场 → 直接开打</summary>
        private async void button4_Click(object sender, EventArgs e)
        {
            _isRunning = true;
            int runCount = ReadUi(() => (int)numRun.Value);
            await Task.Run(() => RevengeFlow.Run(CreateFlowContext(), runCount));
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            _isRunning = false;
        }

        /// <summary>
        /// 调试按钮：截当前画面 → 卫星站模板匹配 → 保存截图并打开，标题显示分数和位置。
        /// 用于实测各种操作状态下卫星站是否可见。
        /// </summary>
        private void button5_Click(object sender, EventArgs e)
        {
            Task.Run(() =>
            {
                try
                {
                    using var screen = _capturer.CaptureToBitmap();
                    var result = _satelliteMatcher.FindBestMatch(screen);
                    string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Screenshots");
                    Directory.CreateDirectory(folder);
                    string path = Path.Combine(folder, $"debug_{DateTime.Now:HHmmss}.png");
                    screen.Save(path);
                    Debug.WriteLine($"调试截图已保存: {path}  卫星站分数={result.Score:F2} 位置=({result.Center.X},{result.Center.Y})");
                    BeginInvoke(() =>
                    {
                        this.Text = $"分数{result.Score:F2} ({result.Center.X},{result.Center.Y})";
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"调试截图失败: {ex.Message}");
                }
            });
        }

        /// <summary>在 UI 线程上安全读取控件属性（后台线程专用）</summary>
        private T ReadUi<T>(Func<T> getter) => (T)Invoke(getter);

        private void RandomSleep(int minMs, int maxMs)
        {
            Thread.Sleep(_random.Next(minMs, maxMs + 1));
        }
    }
}
