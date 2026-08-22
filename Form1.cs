using System.Diagnostics;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using Rect = OpenCvSharp.Rect;

namespace 脚本
{
    public partial class Form1 : Form
    {
        private LdPlayerCapturer _capturer;
        private GetNumberRecognizer _recognizer;
        private TemplateMatcher _satelliteMatcher;
        private TemplateMatcher _gunMatcher;
        private TemplateMatcher _resTitleMatcher;
        private Random _random;
        /// <summary>卫星站记忆位置（拖拽后画面确定，识别一次后直接复用，定期重新校准）</summary>
        private (int x, int y)? _satellitePos;
        private int _satelliteCheckCountdown;
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
            _random = new Random();
            this.TopMost = true;
            this.TopLevel = true;
            //_capturer.StartAppByLauncher();
        }


        private async void button1_Click(object sender, EventArgs e)
        {
            _isRunning = true;
            await Task.Run(() => ExecuteLogic());
        }
        private void ExecuteLogic()
        {
            int runCount = ReadUi(() => (int)numRun.Value);
            for (int i = 0; i < runCount; i++)
            {
                if (!_isRunning)
                    break;

                // 拖拽基地UI（实测 F5 可省，但需拖拽后卫星站才在画面中）
                var sw = Stopwatch.StartNew();
                _capturer.Drag(
                     Locationinformation.BaseUIDrag.startX,
                     Locationinformation.BaseUIDrag.startY,
                     Locationinformation.BaseUIDrag.endX,
                     Locationinformation.BaseUIDrag.endY);
                Debug.WriteLine($"[耗时] 拖拽: {sw.ElapsedMilliseconds}ms");
                Debug.WriteLine("拖拽基地UI");
                RandomSleep(250, 450);
                Debug.WriteLine($"[耗时] 拖拽后等待: {sw.ElapsedMilliseconds}ms");

                // 1. 模板识别卫星站并点击（失败时内部自动 F5+拖拽兜底）
                if (!FindAndTapSatellite())
                {
                    Debug.WriteLine("卫星站识别失败，跳过本轮");
                    continue;
                }
                Debug.WriteLine("点击卫星标志");
                // 2. 点击寻找敌人按钮
                RandomTap(Locationinformation.FindEnemy, 15, 15);

                Debug.WriteLine("点击寻找敌人按钮");
                // 3. 等待敌人加载
                RandomSleep(800, 1200);

                if (_recognizer.GetText(Locationinformation.Name.startX,
                        Locationinformation.Name.startY,
                        Locationinformation.Name.w,
                        Locationinformation.Name.h) == "大卡拉米")
                {
                    if (!FindAndTapSatellite())
                    {
                        Debug.WriteLine("卫星站识别失败，跳过本轮");
                        continue;
                    }
                    // 2. 点击寻找敌人按钮
                    RandomTap(Locationinformation.FindEnemy, 15, 15);
                }


                do
                {
                    int maxLevel = ReadUi(() => (int)numLevel.Value);
                    _capturer.Drag(
                        Locationinformation.EnemyUIDrag.startX + _random.Next(0, 100),
                        Locationinformation.EnemyUIDrag.startY + _random.Next(0, 100),
                        Locationinformation.EnemyUIDrag.endX + _random.Next(0, 100),
                        Locationinformation.EnemyUIDrag.endY + _random.Next(-200, 200));

                    Debug.WriteLine("开始识别敌人等级...");
                    // 5. 识别敌人等级
                    int level = _recognizer.GetNumber(
                        Locationinformation.LevelArea.x,
                        Locationinformation.LevelArea.y,
                        Locationinformation.LevelArea.width,
                        Locationinformation.LevelArea.height);


                    if (level < maxLevel && level > 0)
                    {
                        Debug.WriteLine($"找到合适敌人！等级: {level}");
                        // 执行战斗逻辑
                        ExecuteBattleLogic();
                        break;
                    }
                    else
                    {
                        Debug.WriteLine($"敌人等级过高 ({level})，寻找下一个敌人...");
                        RandomTap(Locationinformation.NextEnemy, 5, 5);
                        RandomSleep(1500, 2200);
                    }

                } while (_isRunning);
            }

        }
        private void ExecuteBattleLogic()
        {
            var heroCheckBoxes = new[] { checkBox1, checkBox2, checkBox3, checkBox4, checkBox5 };
            for (int i = 0; i < Locationinformation.HeroPosition.Count(); i++)
            {
                if (ReadUi(() => heroCheckBoxes[i].Checked))
                {
                    RandomTap(Locationinformation.HeroPosition[i], 10, 10);
                    RandomSleep(200, 300);
                    RandomTap(Locationinformation.Hero, 100, 40);
                }
            }

            // 全部英雄下完后等待 2 秒，再开始资源归零/胜利检测（等英雄部署动画就绪）
            RandomSleep(5000, 5000);

            DateTime startTime = DateTime.Now;
            // 资源归零为主要结束条件；90秒超时仅作兜底（防止打不动的敌人无限战斗）
            const int maxWaitTime = 90 * 1000;


            // 等待战斗胜利
            int victoryStatus;
            do
            {
                victoryStatus = -1;
                TimeSpan elapsed = DateTime.Now - startTime;

                if (elapsed.TotalMilliseconds >= maxWaitTime)
                {
                    RandomTap(Locationinformation.Retreat, 5, 5);///打不过 撤退
                    RandomSleep(1000, 2000);
                    Debug.WriteLine("战斗超时（90秒）资源未抢完，已撤退");
                    break; 
                }
                RandomSleep(1000, 2000);
                // 一次截图复用：资源归零检测与胜利检测共用同一帧（省一次全屏截图）
                using (var screen = _capturer.CaptureToBitmap())
                {
                    // 可掠夺资源已归零 → 提前撤退（不必等满超时）
                    if (IsBattleResourceZero(screen))
                    {
                        RandomTap(Locationinformation.Retreat, 5, 5);
                        RandomSleep(1000, 2000);
                        Debug.WriteLine("资源已抢空，提前撤退");
                        break;
                    }
                    victoryStatus = _recognizer.GetNumber(
                        screen,
                        Locationinformation.VictoryArea.x,
                        Locationinformation.VictoryArea.y,
                        Locationinformation.VictoryArea.width,
                        Locationinformation.VictoryArea.height,
                        false);
                }
                if (victoryStatus==100)
                {
                    Debug.WriteLine("战斗完成 100，已撤退");
                    break;
                }
                Debug.WriteLine($"victoryStatus:{victoryStatus},{_isRunning}");
            } while (_isRunning);

            // 战斗胜利后返回
            Debug.WriteLine("返回基地");
            RandomTap(Locationinformation.Return, 10, 10);
            RandomSleep(1200, 1800);
        }

        /// <summary>
        /// 在 UI 线程上安全读取控件属性（后台线程专用）
        /// </summary>
        private T ReadUi<T>(Func<T> getter)
        {
            return (T)Invoke(getter);
        }
        private void RandomSleep(int minMs, int maxMs)
        {
            int sleepTime = _random.Next(minMs, maxMs + 1);
            Thread.Sleep(sleepTime);
        }
        private void RandomTap((int x, int y) position, int maxOffsetX, int maxOffsetY)
        {
            int offsetX = _random.Next(-maxOffsetX, maxOffsetX + 1);
            int offsetY = _random.Next(-maxOffsetY, maxOffsetY + 1);
            _capturer.Tap(position.x + offsetX, position.y + offsetY);
        }

        /// <summary>
        /// 用给定截图检测机枪数量（复用截图，避免重复截屏）
        /// </summary>
        private bool IsGunCountEnough(Bitmap screen)
        {
            const int requiredCount = 8;
            const double matchThreshold = 0.7;
            using var mat = BitmapConverter.ToMat(screen);
            using var gray = new Mat();
            Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);
            var matches = _gunMatcher.FindAllMatches(gray, matchThreshold, 0.8, 1.2);
            Debug.WriteLine($"机枪检测: {matches.Count} 个 (需≥{requiredCount})");
            return matches.Count >= requiredCount;
        }

        /// <summary>
        /// 用给定截图检测资源是否归零（复用截图，避免重复截屏）。
        /// 判据：模板匹配定位"可掠夺资源"标题（位置无关，容忍移动基地/视角漂移）
        /// → 在标题下方固定偏移的数字行区域内统计"数字字符块"（高度≥15px 的白色块）数量 ≤ 3。
        /// 归零时每行只有单个"0"字符（共 2 个高块），有值时多位数字（5+ 个高块）；
        /// 高度过滤排除虚线/分隔线/噪点等矮条干扰。实测（取证图）：归零 2、动态帧 10、有值 5。
        /// </summary>
        private bool IsBattleResourceZero(Bitmap screen)
        {
            const int maxDigitLike = 3;
            const int roiDx = 10, roiDy = 98, roiW = 300, roiH = 160;
            const double titleThreshold = 0.7;

            // 全图搜索标题模板（位置无关），找到面板后按固定偏移计算数字行检测区域
            var title = _resTitleMatcher.FindBestMatch(screen);
            if (title.Score < titleThreshold)
            {
                Debug.WriteLine($"资源面板标题定位失败(分数{title.Score:F2})");
                SaveTitleFailEvidence(screen);
                return false;
            }
            var roi = new Rectangle(title.Location.X + roiDx, title.Location.Y + roiDy, roiW, roiH);
            int digitLike = ImageProcessing.CountDigitLikeComponents(screen, roi);
            Debug.WriteLine($"资源归零检测: 数字字符块={digitLike} (需≤{maxDigitLike})");
            return digitLike <= maxDigitLike;
        }

        private DateTime _lastTitleFailSave = DateTime.MinValue;

        /// <summary>
        /// 标题定位失败时保存当前画面（10 秒节流），供分析运行时画面与取证图的差异
        /// </summary>
        private void SaveTitleFailEvidence(Bitmap screen)
        {
            if ((DateTime.Now - _lastTitleFailSave).TotalSeconds < 10) return;
            _lastTitleFailSave = DateTime.Now;
            try
            {
                string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Screenshots");
                Directory.CreateDirectory(folder);
                screen.Save(Path.Combine(folder, $"titlefail_{DateTime.Now:HHmmss}.png"));
                Debug.WriteLine("已保存标题定位失败取证图");
            }
            catch { }
        }

        /// <summary>
        /// 用模板匹配识别基地中的卫星站并点击（替代固定坐标）。
        /// 连续重试多次（卫星站天线会旋转，单帧可能匹配不到理想角度）。
        /// </summary>
        private bool FindAndTapSatellite()
        {
            const int SatelliteVerifyInterval = 5;

            // 快路径：记忆位置有效且未到校验轮 → 直接点击（省掉截图+匹配，约快 0.5 秒/轮）
            if (_satellitePos is { } pos && _satelliteCheckCountdown > 0)
            {
                var sw = Stopwatch.StartNew();
                _satelliteCheckCountdown--;
                _capturer.Tap(pos.x, pos.y);
                Debug.WriteLine($"[耗时] 快路径点击: {sw.ElapsedMilliseconds}ms");
                return true;
            }

            // 识别路径：成功则更新记忆位置并重置校验计数
            if (TryFindAndTapSatellite())
            {
                _satelliteCheckCountdown = SatelliteVerifyInterval;
                return true;
            }

            Debug.WriteLine("卫星站直接识别失败，执行 F5+拖拽兜底");
            var swFallback = Stopwatch.StartNew();
            _capturer.MoveMouseTo(950, 500);
            _capturer.SendF5ToLdPlayer();
            RandomSleep(1000, 1500);
            _capturer.Drag(
                Locationinformation.BaseUIDrag.startX,
                Locationinformation.BaseUIDrag.startY,
                Locationinformation.BaseUIDrag.endX,
                Locationinformation.BaseUIDrag.endY);
            RandomSleep(250, 450);
            Debug.WriteLine($"[耗时] 兜底F5+拖拽: {swFallback.ElapsedMilliseconds}ms");
            return TryFindAndTapSatellite();
        }

        /// <summary>
        /// 直接截图识别卫星站并点击（连续重试，卫星站天线会旋转，单帧可能匹配不到理想角度）。
        /// </summary>
        private bool TryFindAndTapSatellite()
        {
            const int maxAttempts = 3;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var swShot = Stopwatch.StartNew();
                using var screen = _capturer.CaptureToBitmap();
                Debug.WriteLine($"[耗时] 截图: {swShot.ElapsedMilliseconds}ms");

                swShot.Restart();
                using var mat = BitmapConverter.ToMat(screen);
                using var gray = new Mat();
                Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);

                // 优先在记忆位置附近 ROI 匹配（快）；ROI 分数不足再全图匹配（画面变化时兜底）
                TemplateMatchResult result;
                if (_satellitePos is { } lastPos)
                {
                    const int roiSize = 320;
                    int rx = Math.Max(0, lastPos.x - roiSize / 2);
                    int ry = Math.Max(0, lastPos.y - roiSize / 2);
                    int rw = Math.Min(roiSize, gray.Width - rx);
                    int rh = Math.Min(roiSize, gray.Height - ry);
                    result = _satelliteMatcher.FindBestMatchInRoi(gray, new Rect(rx, ry, rw, rh), 0.9, 1.3);
                    if (result.Score < _satelliteMatcher.Threshold)
                        result = _satelliteMatcher.FindBestMatch(gray);
                }
                else
                {
                    result = _satelliteMatcher.FindBestMatch(gray);
                }
                Debug.WriteLine($"[耗时] 匹配: {swShot.ElapsedMilliseconds}ms");

                if (result.Score >= _satelliteMatcher.Threshold)
                {
                    swShot.Restart();
                    _capturer.Tap(result.Center.X, result.Center.Y);
                    Debug.WriteLine($"[耗时] 点击: {swShot.ElapsedMilliseconds}ms");
                    _satellitePos = (result.Center.X, result.Center.Y);   // 记忆位置，供后续快路径复用
                    Debug.WriteLine($"卫星站识别成功: 分数{result.Score:F2} 尺度{result.Scale:F2} 位置({result.Center.X},{result.Center.Y})");
                    return true;
                }
                Debug.WriteLine($"卫星站识别失败(分数{result.Score:F2})，重试 {attempt}/{maxAttempts}");
                RandomSleep(400, 700);
            }
            return false;
        }

        private void btnStop_Click(object sender, EventArgs e)
        {
            _isRunning = false;
        }

        /// <summary>
        /// 调试按钮：截当前画面 → 卫星站模板匹配 → 保存截图并打开，标题显示分数和位置。
        /// 用于实测各种操作状态下卫星站是否可见（F5/拖拽前置步骤能否省略）。
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
        /// <summary>
        /// 波兰守卫
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void button3_Click(object sender, EventArgs e)
        {
            await Task.Run(() => 波兰守卫());
        }
        public void 波兰守卫()
        {
            int runCount = ReadUi(() => (int)numRun.Value);
            for (int i = 0; i < runCount; i++)
            {
                Debug.WriteLine($"执行{i}");
                RandomTap(Locationinformation.开始防御, 2, 2);
                string str;
                do
                {
                    RandomSleep(1000, 3000);
                    str = _recognizer.GetText(
                        Locationinformation.战斗胜利.x,
                        Locationinformation.战斗胜利.y,
                        Locationinformation.战斗胜利.width,
                        Locationinformation.战斗胜利.height);
                } while (str != "战斗胜利");
                RandomTap(Locationinformation.Return, 2, 2);
                RandomSleep(2000, 3000);
            }
        }

        private async void button4_Click(object sender, EventArgs e)
        {
            _isRunning = true;
            await Task.Run(() => 复仇X());
        }
        private void 复仇X()
        {
            int runCount = ReadUi(() => (int)numRun.Value);
            for (int i = 0; i < runCount; i++)
            {
                Debug.WriteLine($"执行{i}");
                RandomTap(Locationinformation.开始防御, 2, 2);
                RandomSleep(1500, 2200);
                _capturer.Drag(
                        Locationinformation.EnemyUIDrag.startX + _random.Next(0, 100),
                        Locationinformation.EnemyUIDrag.startY + _random.Next(0, 100),
                        Locationinformation.EnemyUIDrag.endX + _random.Next(0, 100),
                        Locationinformation.EnemyUIDrag.endY + _random.Next(-200, 200));
                ExecuteBattleLogic();
                //RandomTap(Locationinformation.Return, 2, 2);
                RandomSleep(1500, 2200);
            }
        }

        private async void button2_Click(object sender, EventArgs e)
        {
            _isRunning = true;
            await Task.Run(() => 打资源());
        }
        private void 打资源()
        {
            int succeed = 0;
            int runCount = ReadUi(() => (int)numRun.Value);
            for (int i = 0; i < runCount; i++)
            {
                if (!_isRunning)
                    break;

                // 拖拽基地UI（实测 F5 可省，但需拖拽后卫星站才在画面中）
                var sw = Stopwatch.StartNew();
                _capturer.Drag(
                     Locationinformation.BaseUIDrag.startX,
                     Locationinformation.BaseUIDrag.startY,
                     Locationinformation.BaseUIDrag.endX,
                     Locationinformation.BaseUIDrag.endY);
                Debug.WriteLine($"[耗时] 拖拽: {sw.ElapsedMilliseconds}ms");
                Debug.WriteLine("拖拽基地UI");
                RandomSleep(300, 500);
                Debug.WriteLine($"[耗时] 拖拽后等待: {sw.ElapsedMilliseconds}ms");

                // 1. 模板识别卫星站并点击（失败时内部自动 F5+拖拽兜底）
                if (!FindAndTapSatellite())
                {
                    Debug.WriteLine("卫星站识别失败，跳过本轮");
                    continue;
                }
                Debug.WriteLine("点击卫星标志");
                // 2. 点击寻找敌人按钮
                RandomTap(Locationinformation.FindEnemy, 15, 15);

                Debug.WriteLine("点击寻找敌人按钮");
                // 3. 等待敌人加载
                RandomSleep(2000, 3000);

                while (_isRunning &&
             _recognizer.GetText(
                 Locationinformation.Name.startX,
                 Locationinformation.Name.startY,
                 Locationinformation.Name.w,
                 Locationinformation.Name.h) == "大卡拉米")
                {
                    Debug.WriteLine("检测到大卡拉米，重新寻找敌人...");
                    // 拖拽基地UI后重新识别卫星站（F5 已确认不需要）
                    _capturer.Drag(
                  Locationinformation.BaseUIDrag.startX,
                  Locationinformation.BaseUIDrag.startY,
                  Locationinformation.BaseUIDrag.endX,
                  Locationinformation.BaseUIDrag.endY);
                    RandomSleep(800, 1500);
                    // 卫星站识别并点击（失败时内部自动 F5+拖拽兜底）
                    if (!FindAndTapSatellite())
                    {
                        Debug.WriteLine("卫星站识别失败，重新等待敌人加载");
                        break;
                    }
                    RandomTap(Locationinformation.FindEnemy, 15, 15);
                    RandomSleep(1200, 1500); // 等待新敌人加载
                }


                do
                {
                    // 一次截图复用：资源 OCR 与机枪检测共用同一帧（省一次全屏截图）
                    using var screen = _capturer.CaptureToBitmap();
                    var input = _recognizer.GetText(screen, 80, 220, 200, 40);
                    string cleaned = input.Replace(",", "").Replace(".", "");
                    if (int.TryParse(cleaned, out int res))
                    {
                        int resMin = ReadUi(() => (int)numResMin.Value);
                        int resMax = ReadUi(() => (int)numResMax.Value);
                        if (res % 17000 == 0 && res / 17000 > resMin && res / 17000 < resMax)
                        {
                            // 机枪数量判断：>= 8 才进攻，否则找下一个（判定链：资源区间 → 机枪数量）
                            if (!IsGunCountEnough(screen))
                            {
                                Debug.WriteLine($"资源达标但机枪不足，找下一个（资源{res}）");
                                RandomTap(Locationinformation.NextEnemy, 5, 5);
                                RandomSleep(1200, 1200);
                                continue;
                            }
                            _capturer.Drag(
                     Locationinformation.EnemyUIDrag.startX + _random.Next(0, 100),
                     Locationinformation.EnemyUIDrag.startY + _random.Next(0, 100),
                     Locationinformation.EnemyUIDrag.endX + _random.Next(0, 100),
                     Locationinformation.EnemyUIDrag.endY + _random.Next(-200, 200));

                            Debug.WriteLine($"找到合适敌人！等级,资源：{res}");
                            succeed++;
                            BeginInvoke(() => { this.Text = $"已打{succeed}次"; });
                            // 执行战斗逻辑
                            ExecuteBattleLogic();
                            break;
                        }
                        else
                        {
                            Debug.WriteLine($"没找到合适敌人！资源：{res}");
                            RandomTap(Locationinformation.NextEnemy, 5, 5);
                            RandomSleep(1200, 1600);
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"没找到合适敌人！ 找下一个，资源：{res}");
                        RandomTap(Locationinformation.NextEnemy, 5, 5);
                        RandomSleep(1500, 1800);
                    }

                } while (_isRunning);
            }

        }
    }
}
