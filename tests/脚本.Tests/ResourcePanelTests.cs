using System.Drawing;
using System.Drawing.Imaging;
using Xunit;
using Point = OpenCvSharp.Point;

namespace 脚本.Tests
{
    /// <summary>
    /// 资源面板定位 + 归零判据测试。
    /// 方案：模板匹配定位"可掠夺资源"标题（位置无关）→ 动态 ROI（标题 + 固定偏移）→ 数字字符块计数。
    /// 解决固定坐标 ROI 在"移动基地/视角漂移"后面板位置变化导致判据失效的问题。
    /// </summary>
    public class ResourcePanelTests
    {
        // 标题模板裁剪区（1920x1080，实测跨帧稳定命中）
        private const int TitleX = 30, TitleY = 102, TitleW = 150, TitleH = 28;
        // 标题 → 数字行检测 ROI 的固定偏移（scene11 实测：标题(30,102)、ROI(40,200)）
        private const int RoiDx = 10, RoiDy = 98, RoiW = 300, RoiH = 160;
        // 产品判据阈值：数字字符块 ≤3 判归零（实测归零 2、有值 5+）
        private const int MaxDigitLike = 3;

        private static TemplateMatcher CreateTitleMatcher()
        {
            using var s11 = new Bitmap(TestPaths.Scene11);
            using var crop = s11.Clone(new Rectangle(TitleX, TitleY, TitleW, TitleH), PixelFormat.Format32bppArgb);
            return new TemplateMatcher(crop);
        }

        private static int CountDigitLike(Bitmap screen, Point titlePos)
        {
            var roi = new Rectangle(titlePos.X + RoiDx, titlePos.Y + RoiDy, RoiW, RoiH);
            return ImageProcessing.CountDigitLikeComponents(screen, roi);
        }

        /// <summary>把图像整体向下平移（模拟"移动基地"导致面板位置下移）</summary>
        private static Bitmap ShiftDown(string imagePath, int dy)
        {
            using var src = new Bitmap(imagePath);
            var shifted = new Bitmap(src.Width, src.Height + dy, PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(shifted))
            {
                g.Clear(Color.Black);
                g.DrawImage(src, 0, dy);
            }
            return shifted;
        }

        [Fact]
        public void 归零帧_标题定位成功且判定归零()
        {
            using var matcher = CreateTitleMatcher();
            using var screen = new Bitmap(TestPaths.Scene11);
            var result = matcher.FindBestMatch(screen);
            Assert.True(result.Score >= 0.9, $"标题匹配分数 {result.Score:F3} 应 ≥0.9");
            Assert.InRange(result.Location.X, TitleX - 5, TitleX + 5);
            Assert.InRange(result.Location.Y, TitleY - 5, TitleY + 5);

            int digitLike = CountDigitLike(screen, result.Location);
            Assert.True(digitLike <= MaxDigitLike, $"归零帧数字字符块 {digitLike} 应 ≤{MaxDigitLike}");
        }

        [Fact]
        public void 动态帧_油料归零金币未归零_不判定归零()
        {
            using var matcher = CreateTitleMatcher();
            using var screen = new Bitmap(TestPaths.Scene13);
            var result = matcher.FindBestMatch(screen);
            Assert.True(result.Score >= 0.9, $"标题匹配分数 {result.Score:F3} 应 ≥0.9");

            int digitLike = CountDigitLike(screen, result.Location);
            Assert.True(digitLike > MaxDigitLike, $"动态帧数字字符块 {digitLike} 应 >{MaxDigitLike}");
        }

        [Fact]
        public void 有值帧_不判定归零()
        {
            using var matcher = CreateTitleMatcher();
            using var screen = new Bitmap(TestPaths.Scene12);
            var result = matcher.FindBestMatch(screen);
            Assert.True(result.Score >= 0.9, $"标题匹配分数 {result.Score:F3} 应 ≥0.9");

            int digitLike = CountDigitLike(screen, result.Location);
            Assert.True(digitLike > MaxDigitLike, $"有值帧数字字符块 {digitLike} 应 >{MaxDigitLike}");
        }

        [Fact]
        public void 运行取证图_判定正确()
        {
            using var matcher = CreateTitleMatcher();
            using var screen = new Bitmap(TestPaths.Scene13);   // 运行取证图（油料0、金币7位未归零）
            var result = matcher.FindBestMatch(screen);
            Assert.True(result.Score >= 0.9, $"标题匹配分数 {result.Score:F3} 应 ≥0.9");
            int digitLike = CountDigitLike(screen, result.Location);
            // 该取证图（zero_193634）：油料归零、金币 7 位未归零 → 不应判归零
            Assert.True(digitLike > MaxDigitLike, $"运行取证图数字字符块 {digitLike} 应 >{MaxDigitLike}");
        }

        [Fact]
        public void 画面下移40px_仍能定位并正确判定()
        {
            using var matcher = CreateTitleMatcher();
            using var shifted = ShiftDown(TestPaths.Scene11, 40);
            var result = matcher.FindBestMatch(shifted);
            Assert.True(result.Score >= 0.9, $"偏移后标题匹配分数 {result.Score:F3} 应 ≥0.9");
            Assert.InRange(result.Location.Y, TitleY + 35, TitleY + 45);   // 定位到下移后的新位置

            int digitLike = CountDigitLike(shifted, result.Location);
            Assert.True(digitLike <= MaxDigitLike, $"偏移后归零帧数字字符块 {digitLike} 应 ≤{MaxDigitLike}");
        }

        [Fact]
        public void 画面右移60px_仍能定位并正确判定()
        {
            using var matcher = CreateTitleMatcher();
            using var src = new Bitmap(TestPaths.Scene11);
            using (var shifted = new Bitmap(src.Width + 60, src.Height, PixelFormat.Format32bppArgb))
            {
                using (var g = Graphics.FromImage(shifted))
                {
                    g.Clear(Color.Black);
                    g.DrawImage(src, 60, 0);
                }
                var result = matcher.FindBestMatch(shifted);
                Assert.True(result.Score >= 0.9, $"右移后标题匹配分数 {result.Score:F3} 应 ≥0.9");
                Assert.InRange(result.Location.X, TitleX + 55, TitleX + 65);

                int digitLike = CountDigitLike(shifted, result.Location);
                Assert.True(digitLike <= MaxDigitLike, $"右移后归零帧数字字符块 {digitLike} 应 ≤{MaxDigitLike}");
            }
        }
    }
}
