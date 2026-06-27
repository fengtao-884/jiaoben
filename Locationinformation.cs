using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace 脚本
{
    public class Locationinformation
    {
        /// <summary>
        /// 等级坐标
        /// </summary>
        public static (int x, int y, int width, int height) LevelArea => (15, 23, 35, 35);

        /// <summary>
        /// 胜利坐标
        /// </summary>
        public static (int x, int y, int width, int height) VictoryArea => (885, 273, 150, 52);

        public static (int x, int y) Home => (1736, 944);


        /// <summary>
        /// 敌人UI拖拽区域（起始坐标和结束坐标）
        /// </summary>
        public static (int startX, int startY, int endX, int endY) EnemyUIDrag => (900, 500, 1800, 1200);

        /// <summary>
        /// 基地UI拖拽区域（起始坐标和结束坐标）
        /// </summary>
        public static (int startX, int startY, int endX, int endY) BaseUIDrag => (1450, 387, 100, -500);

        public static (int startX, int startY, int w, int h) Name => (270, 25, 125, 40);
        /// <summary>
        /// 卫星标记坐标
        /// </summary>
        public static (int x, int y) MoonMark => (680, 725);

        public static (int x, int y) Center => (905, 500);

        /// <summary>
        /// 寻找敌人按钮坐标
        /// </summary>
        public static (int x, int y) FindEnemy => (1040, 940);

        /// <summary>
        /// 下一个敌人按钮坐标
        /// </summary>
        public static (int x, int y) NextEnemy => (1671, 284);

        public static (int x, int y)[] HeroPosition { get; } = new[] {(160, 950),  (320, 950), (480, 950),   (640, 950),   (800, 950)  };
        //public static (int x, int y)[] HeroPosition { get; } = new[] { (480, 950),(640, 950) };
        /// <summary>
        /// 下英雄的位置
        /// </summary>
        public static (int x, int y) Hero => (550, 330);

        public static (int x, int y) Return => (940, 930);
        /// <summary>
        /// 打不过 撤退坐标
        /// </summary>
        public static (int x, int y) Retreat => (1830, 100);


        public static (int x, int y) 作战中心 => (1840, 665);

        public static (int x, int y) 军备收集 => (1433, 525);

        public static (int x, int y) 开始战斗 => (1411, 900);

        public static (int x, int y) 开始防御 => (1024, 891);

        public static (int x, int y, int width, int height) 战斗胜利 => (823, 260, 300, 100);

        public static (int x, int y, int width, int height) 敌人名称 => (120, 24, 220, 40);

    }
}
