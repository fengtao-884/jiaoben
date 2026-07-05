using System.Diagnostics;

namespace 脚本
{
    public partial class Form1 : Form
    {
        private LdPlayerCapturer _capturer;
        private GetNumberRecognizer _recognizer;
        private Random _random;
        private bool _isRunning = false;


        public Form1()
        {
            InitializeComponent();
            _capturer = new LdPlayerCapturer();
            _recognizer = new GetNumberRecognizer(_capturer);
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
            int succeed = 0;
            for (int i = 0; i < numRun.Value; i++)
            {
                if (!_isRunning)
                    break;
                _capturer.MoveMouseTo(950, 500);
                _capturer.SendF5ToLdPlayer();
                RandomSleep(1000, 1500);
                _capturer.Drag(
                     Locationinformation.BaseUIDrag.startX,
                     Locationinformation.BaseUIDrag.startY,
                     Locationinformation.BaseUIDrag.endX,
                     Locationinformation.BaseUIDrag.endY);
                Debug.WriteLine("拖拽基地UI");
                RandomSleep(1000, 1500);

                // 1. 
                RandomTap(Locationinformation.MoonMark, 1, 1);
                Debug.WriteLine("点击卫星标志");
                // 2. 点击寻找敌人按钮
                RandomTap(Locationinformation.FindEnemy, 15, 15);

                Debug.WriteLine("点击寻找敌人按钮");
                // 3. 等待敌人加载
                RandomSleep(2000, 3000);

                if (_recognizer.GetText(Locationinformation.Name.startX,
                        Locationinformation.Name.startY,
                        Locationinformation.Name.w,
                        Locationinformation.Name.h) == "大卡拉米")
                {
                    RandomTap(Locationinformation.MoonMark, 1, 1);
                    // 2. 点击寻找敌人按钮
                    RandomTap(Locationinformation.FindEnemy, 15, 15);
                }


                do
                {
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


                    if (level < numLevel.Value && level > 0)
                    {
                        Debug.WriteLine($"找到合适敌人！等级: {level}");
                        succeed++;
                        // 执行战斗逻辑
                        ExecuteBattleLogic();
                        break;
                    }
                    else
                    {
                        Debug.WriteLine($"敌人等级过高 ({level})，寻找下一个敌人...");
                        RandomTap(Locationinformation.NextEnemy, 5, 5);
                        RandomSleep(2000, 2200);
                    }

                } while (_isRunning);
            }

        }
        private void ExecuteBattleLogic()
        {
            var heroCheckBoxes = new[] { checkBox1, checkBox2, checkBox3, checkBox4, checkBox5 };
            for (int i = 0; i < Locationinformation.HeroPosition.Count(); i++)
            {
                if (heroCheckBoxes[i].Checked)
                {
                    RandomTap(Locationinformation.HeroPosition[i], 10, 10);
                    RandomSleep(800, 1200);
                    RandomTap(Locationinformation.Hero, 100, 40);
                }
            }

            DateTime startTime = DateTime.Now;
            const int maxWaitTime = 35 * 1000;//40秒


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
                    Debug.WriteLine("战斗失败，已撤退");
                    break; 
                }
                RandomSleep(1000, 3000);
                victoryStatus = _recognizer.GetNumber(
                    Locationinformation.VictoryArea.x,
                    Locationinformation.VictoryArea.y,
                    Locationinformation.VictoryArea.width,
                    Locationinformation.VictoryArea.height,
                    false);
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
            RandomSleep(2000, 3000);
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

        private void btnStop_Click(object sender, EventArgs e)
        {
            _isRunning = false;
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
            for (int i = 0; i < numRun.Value; i++)
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
            for (int i = 0; i < numRun.Value; i++)
            {
                Debug.WriteLine($"执行{i}");
                RandomTap(Locationinformation.开始防御, 2, 2);
                RandomSleep(2000, 3000);
                _capturer.Drag(
                        Locationinformation.EnemyUIDrag.startX + _random.Next(0, 100),
                        Locationinformation.EnemyUIDrag.startY + _random.Next(0, 100),
                        Locationinformation.EnemyUIDrag.endX + _random.Next(0, 100),
                        Locationinformation.EnemyUIDrag.endY + _random.Next(-200, 200));
                ExecuteBattleLogic();
                //RandomTap(Locationinformation.Return, 2, 2);
                RandomSleep(2000, 3000);
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
            for (int i = 0; i < numRun.Value; i++)
            {
                if (!_isRunning)
                    break;
                _capturer.MoveMouseTo(950, 500);
                _capturer.SendF5ToLdPlayer();
                RandomSleep(1000, 1500);
                _capturer.Drag(
                     Locationinformation.BaseUIDrag.startX,
                     Locationinformation.BaseUIDrag.startY,
                     Locationinformation.BaseUIDrag.endX,
                     Locationinformation.BaseUIDrag.endY);
                Debug.WriteLine("拖拽基地UI");
                RandomSleep(1000, 1500);

                // 1. 
                RandomTap(Locationinformation.MoonMark, 1, 1);
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
                    _capturer.SendF5ToLdPlayer();
                    RandomSleep(1000, 1500);
                    _capturer.Drag(
                  Locationinformation.BaseUIDrag.startX,
                  Locationinformation.BaseUIDrag.startY,
                  Locationinformation.BaseUIDrag.endX,
                  Locationinformation.BaseUIDrag.endY); RandomSleep(800, 1500);
                    RandomTap(Locationinformation.MoonMark, 1, 1);
                    RandomTap(Locationinformation.FindEnemy, 15, 15);
                    RandomSleep(1200, 1500); // 等待新敌人加载
                }


                do
                {
                    var input = _recognizer.GetText(80, 220, 200, 40);
                    string cleaned = input.Replace(",", "").Replace(".", "");
                    if (int.TryParse(cleaned, out int res))
                    {
                        if (res % 17000 == 0 && res / 17000 > 380&& res / 17000 < 520)
                        {
                            _capturer.Drag(
                     Locationinformation.EnemyUIDrag.startX + _random.Next(0, 100),
                     Locationinformation.EnemyUIDrag.startY + _random.Next(0, 100),
                     Locationinformation.EnemyUIDrag.endX + _random.Next(0, 100),
                     Locationinformation.EnemyUIDrag.endY + _random.Next(-200, 200));

                            // 5. 识别敌人等级
                            int level = _recognizer.GetNumber(
                                Locationinformation.LevelArea.x,
                                Locationinformation.LevelArea.y,
                                Locationinformation.LevelArea.width,
                                Locationinformation.LevelArea.height);


                            if (level > numLevel.Value && level > 0)
                            {
                                Debug.WriteLine($"找到合适敌人！等级: {level},资源：{res}");
                                succeed++;
                                BeginInvoke(() => { this.Text = $"已打{succeed}次"; });  
                                // 执行战斗逻辑
                                ExecuteBattleLogic();
                                break;
                            }
                            else
                            {
                                Debug.WriteLine($"敌人等级过高 ({level})，寻找下一个敌人...");
                                RandomTap(Locationinformation.NextEnemy, 5, 5);
                                RandomSleep(1500, 1800);
                            }
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
