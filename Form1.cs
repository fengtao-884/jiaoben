using OpenCvSharp;
using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

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
            //string str = _recognizer.GetText(Locationinformation.敌人名称.x, Locationinformation.敌人名称.y,
            //            Locationinformation.敌人名称.width, Locationinformation.敌人名称.height); return;
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
                _capturer.MoveMouseTo(950,500);
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
                RandomTap(Locationinformation.MoonMark, 2, 2);
                Debug.WriteLine("点击卫星标志");
                // 2. 点击寻找敌人按钮
                RandomTap(Locationinformation.FindEnemy, 15, 15);

                Debug.WriteLine("点击寻找敌人按钮");
                // 3. 等待敌人加载
                RandomSleep(2000, 3000);

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
            // 遍历所有英雄位置，点击英雄并放置
            Random rnd = new Random();
            var shuffledPositions = Locationinformation.HeroPosition
                .OrderBy(x => rnd.Next())
                .ToList();

            // 遍历打乱后的英雄位置，点击英雄并放置
            foreach (var heroPosition in shuffledPositions)
            {
                RandomTap(heroPosition, 10, 10);
                RandomSleep(500, 1000);
                RandomTap(Locationinformation.Hero, 40, 40);
            }

            // 等待战斗胜利
            int victoryStatus;
            DateTime startTime = DateTime.Now;
            const int maxWaitTime = 2 * 60 * 1000;
            do
            {
                TimeSpan elapsed = DateTime.Now - startTime;
                if (elapsed.TotalMilliseconds >= maxWaitTime)
                {
                    RandomTap(Locationinformation.Retreat, 5, 5);///打不过 撤退
                }
                RandomSleep(1000, 3000);
                victoryStatus = _recognizer.GetNumber(
                    Locationinformation.VictoryArea.x,
                    Locationinformation.VictoryArea.y,
                    Locationinformation.VictoryArea.width,
                    Locationinformation.VictoryArea.height,
                    false);
            } while (victoryStatus == -1&& _isRunning); //有数字则跳出循环

            // 战斗胜利后返回
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
        /// 军备收集
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void button2_Click(object sender, EventArgs e)
        {
            await Task.Run(() => 军备收集());

        }
        private void 军备收集()
        {
            RandomTap(Locationinformation.作战中心, 2, 2);

            RandomTap(Locationinformation.军备收集, 2, 2);
            for (int i = 0; i < 3; i++)
            {
                RandomTap(Locationinformation.开始战斗, 2, 2);

                RandomSleep(2000, 4000);

                _capturer.Drag(
                         Locationinformation.EnemyUIDrag.startX + _random.Next(0, 100),
                         Locationinformation.EnemyUIDrag.startY + _random.Next(0, 100),
                         Locationinformation.EnemyUIDrag.endX + _random.Next(0, 100),
                         Locationinformation.EnemyUIDrag.endY + _random.Next(-200, 200));
                RandomSleep(2000, 4000);
                ExecuteBattleLogic();
            }
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
            for (int i = 0; i < 40; i++)
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

        private async void  button4_Click(object sender, EventArgs e)
        {
            await Task.Run(() => 复仇X());
        }
        private void 复仇X()
        {
            for (int i = 0; i < 35; i++)
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
                RandomTap(Locationinformation.Return, 2, 2);
                RandomSleep(2000, 3000);
            }
        }

    }
}
