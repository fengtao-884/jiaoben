using System.Drawing;
using System.Drawing.Imaging;
using Xunit;
using Point = OpenCvSharp.Point;
using Rect = OpenCvSharp.Rect;

namespace 脚本.Tests
{
    /// <summary>
    /// 资源归零检测（抗视角变化版）回归测试。
    /// 链路与产品 IsBattleResourceZero 完全一致：
    /// ① 标题模板只在左上角 ROI (0,0,420,260) 内搜索，分数 ≥0.6 定位成功；
    /// ② 数字行大区域（标题+偏移(10,98)，300x160）内统计"数字字符块"
    ///   （中心在油料/金币两条窄带内 ±13、宽 6~24、高 20~32）≤3 判归零。
    /// 样本：用户"调试截图"收集的 8 张真实战斗画面（含移动视角后的各种状态）。
    /// </summary>
    public class ResourceZeroRobustTests
    {
        private const int MaxDigitChars = 3;
        private const double TitleThreshold = 0.6;

        private static TemplateMatcher CreateTitleMatcher()
        {
            using var s11 = new Bitmap(TestPaths.Scene11);
            using var crop = s11.Clone(new Rectangle(30, 102, 150, 28), PixelFormat.Format32bppArgb);
            return new TemplateMatcher(crop);
        }

        /// <summary>复刻产品检测链路：返回 (定位成功?, 数字字符块数)</summary>
        private static (bool located, int digitChars) Detect(Bitmap screen, TemplateMatcher matcher)
        {
            using var mat = OpenCvSharp.Extensions.BitmapConverter.ToMat(screen);
            using var gray = new OpenCvSharp.Mat();
            OpenCvSharp.Cv2.CvtColor(mat, gray, OpenCvSharp.ColorConversionCodes.BGR2GRAY);

            var title = matcher.FindBestMatchInRoi(gray, new Rect(0, 0, 420, 260), matcher.MinScale, matcher.MaxScale);
            if (title.Score < TitleThreshold) return (false, -1);

            var digitRoi = new Rectangle(title.Location.X + 10, title.Location.Y + 98, 300, 160);
            int chars = ImageProcessing.CountDigitCharsInBands(screen, digitRoi, new[] { 30, 120 });
            return (true, chars);
        }

        [Theory]
        [InlineData("debug_102126.png", false)]   // 有值：两行各 7 位数字 → 未归零
        [InlineData("debug_102145.png", false)]   // 移动后有值：场景干扰块多 → 未归零
        [InlineData("debug_102152.png", false)]   // 视角大变：全图最高分在远处 → 不应误判归零
        [InlineData("debug_102200.png", false)]   // 移动后有值 → 未归零
        [InlineData("debug_102222.png", false)]   // 移动后有值（含巨型干扰块）→ 未归零
        [InlineData("debug_102232.png", true)]    // 归零：两个"0"字符 → 归零
        [InlineData("debug_102239.png", true)]    // 归零延续帧 → 归零
        [InlineData("debug_102248.png", true)]    // 归零延续帧 → 归零
        public void 运行取证图_判定正确(string fileName, bool expectZero)
        {
            string path = Path.Combine(TestPaths.DebugShots, fileName);
            Assert.True(File.Exists(path), $"样本不存在: {path}");

            using var screen = new Bitmap(path);
            using var matcher = CreateTitleMatcher();
            var (located, chars) = Detect(screen, matcher);

            // 真归零必有"0"字符（chars≥1）；chars=0 说明面板不在画面（假定位），不能判归零
            bool judgedZero = located && chars >= 1 && chars <= MaxDigitChars;
            Assert.True(judgedZero == expectZero,
                $"{fileName}: located={located}, chars={chars}, 判定={(judgedZero ? "归零" : "未归零")}，期望={expectZero}");
        }
    }
}
