// D:\Code\脚本\tests\脚本.Tests\ImageProcessingTests.cs
using System.Drawing.Imaging;
using Xunit;
using 脚本;

namespace 脚本.Tests;

public class ImageProcessingTests
{
    [Theory]
    [InlineData(0, 0, 0, 0)]          // 黑
    [InlineData(255, 255, 255, 255)]  // 白
    [InlineData(255, 0, 0, 76)]       // 红 → 0.299*255 ≈ 76
    public void Gray_亮度正确(int r, int g, int b, int expected) =>
        Assert.Equal(expected, ImageProcessing.Gray(r, g, b));

    [Fact]
    public void Binarize_深色变黑_浅色变白()
    {
        using var bmp = new Bitmap(2, 1, PixelFormat.Format32bppArgb);
        bmp.SetPixel(0, 0, Color.FromArgb(0, 0, 0));
        bmp.SetPixel(1, 0, Color.FromArgb(255, 255, 255));
        using var bin = ImageProcessing.Binarize(bmp, 128);
        Assert.Equal(Color.Black.ToArgb(), bin.GetPixel(0, 0).ToArgb());
        Assert.Equal(Color.White.ToArgb(), bin.GetPixel(1, 0).ToArgb());
    }

    [Fact]
    public void ToArgbArray_FromArgbArray_往返保真()
    {
        using var bmp = new Bitmap(3, 2, PixelFormat.Format32bppArgb);
        bmp.SetPixel(0, 0, Color.Red);
        bmp.SetPixel(1, 0, Color.Green);
        bmp.SetPixel(2, 1, Color.FromArgb(10, 20, 30));

        var arr = ImageProcessing.ToArgbArray(bmp);
        using var back = ImageProcessing.FromArgbArray(arr, bmp.Width, bmp.Height);

        Assert.Equal(bmp.GetPixel(0, 0).ToArgb(), back.GetPixel(0, 0).ToArgb());
        Assert.Equal(bmp.GetPixel(1, 0).ToArgb(), back.GetPixel(1, 0).ToArgb());
        Assert.Equal(bmp.GetPixel(2, 1).ToArgb(), back.GetPixel(2, 1).ToArgb());
    }

    [Fact]
    public void Crop_越界区域_裁剪到边界()
    {
        using var bmp = new Bitmap(100, 100, PixelFormat.Format32bppArgb);
        using var crop = ImageProcessing.Crop(bmp, new Rectangle(90, 90, 50, 50));
        Assert.Equal(10, crop.Width);
        Assert.Equal(10, crop.Height);
    }

    [Fact]
    public void Crop_负起点_归零()
    {
        using var bmp = new Bitmap(100, 100, PixelFormat.Format32bppArgb);
        using var crop = ImageProcessing.Crop(bmp, new Rectangle(-5, -5, 20, 20));
        Assert.Equal(15, crop.Width);
        Assert.Equal(15, crop.Height);
    }

    [Fact]
    public void Crop_空区域_抛异常()
    {
        using var bmp = new Bitmap(100, 100, PixelFormat.Format32bppArgb);
        Assert.Throws<ArgumentOutOfRangeException>(
            () => ImageProcessing.Crop(bmp, new Rectangle(200, 0, 10, 10)));
    }

    [Fact]
    public void Binarize_等于阈值_置黑()
    {
        using var bmp = new Bitmap(1, 1, PixelFormat.Format32bppArgb);
        bmp.SetPixel(0, 0, Color.FromArgb(128, 128, 128));
        using var bin = ImageProcessing.Binarize(bmp, 128);
        Assert.Equal(Color.Black.ToArgb(), bin.GetPixel(0, 0).ToArgb());
    }

    [Fact]
    public void FilterWhiteComponents_移除孤立白点_保留连通块()
    {
        // 4x4：右下 2x2 白色连通块 + 左上角单个孤立白点（8-连通下与块无对角接触）
        using var bmp = new Bitmap(4, 4, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
            g.Clear(Color.Black);
        bmp.SetPixel(0, 0, Color.White);           // 孤立点（面积 1）
        bmp.SetPixel(2, 2, Color.White);           // 2x2 块（面积 4）
        bmp.SetPixel(3, 2, Color.White);
        bmp.SetPixel(2, 3, Color.White);
        bmp.SetPixel(3, 3, Color.White);

        using var kept = ImageProcessing.FilterWhiteComponents(bmp, count => count >= 4);

        Assert.Equal(Color.Black.ToArgb(), kept.GetPixel(0, 0).ToArgb()); // 孤立点被移除
        Assert.Equal(Color.White.ToArgb(), kept.GetPixel(3, 3).ToArgb()); // 连通块保留
    }

    [Fact]
    public void CountWhiteComponents_合成图_计数与面积过滤正确()
    {
        // 5x5：孤立白点 (0,0) 面积1 + 2x2 白块 (1,1)-(2,2) 面积4
        using var bmp = new Bitmap(5, 5, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
            g.Clear(Color.Black);
        bmp.SetPixel(0, 0, Color.White);
        bmp.SetPixel(1, 1, Color.White);
        bmp.SetPixel(2, 1, Color.White);
        bmp.SetPixel(1, 2, Color.White);
        bmp.SetPixel(2, 2, Color.White);

        Assert.Equal(1, ImageProcessing.CountWhiteComponents(bmp, new Rectangle(0, 0, 5, 5)));    // minArea=4：过滤孤立点
        Assert.Equal(2, ImageProcessing.CountWhiteComponents(bmp, new Rectangle(0, 0, 5, 5), 1));  // minArea=1：两个都算
    }

    [Fact]
    public void CountDigitLikeComponents_矮线干扰被过滤()
    {
        // 5x20：两个 2x16 高块（模拟"0"字符）+ 一条 1x2 矮线（模拟虚线/分隔线干扰）
        using var bmp = new Bitmap(5, 20, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
            g.Clear(Color.Black);
        for (int y = 1; y < 17; y++) { bmp.SetPixel(0, y, Color.White); bmp.SetPixel(1, y, Color.White); }  // 高块1（2x16）
        for (int y = 1; y < 17; y++) { bmp.SetPixel(3, y, Color.White); bmp.SetPixel(4, y, Color.White); }  // 高块2（2x16）
        bmp.SetPixel(2, 19, Color.White);                                                                    // 矮线（1x1）

        Assert.Equal(2, ImageProcessing.CountDigitLikeComponents(bmp, new Rectangle(0, 0, 5, 20)));           // 高块2个（矮线高度不够）
        Assert.Equal(2, ImageProcessing.CountWhiteComponents(bmp, new Rectangle(0, 0, 5, 20)));                // minArea=4：矮线被面积过滤
        Assert.Equal(3, ImageProcessing.CountWhiteComponents(bmp, new Rectangle(0, 0, 5, 20), 1));             // minArea=1：矮线也统计
    }

    [Fact]
    public void CountDigitLikeComponents_取证图_归零与有值区分()
    {
        // 产品判据阈值：≤3 判归零（实测：归零 2、动态(油料0金币7位) 10、有值 5）
        var region = new Rectangle(40, 200, 300, 160);
        using var s11 = new Bitmap(TestPaths.Scene11);   // 都归零
        using var s13 = new Bitmap(TestPaths.Scene13);   // 动态帧：油料0、金币7位
        using var s12 = new Bitmap(TestPaths.Scene12);   // 有值

        int c11 = ImageProcessing.CountDigitLikeComponents(s11, region);
        int c13 = ImageProcessing.CountDigitLikeComponents(s13, region);
        int c12 = ImageProcessing.CountDigitLikeComponents(s12, region);

        Assert.True(c11 <= 3, $"归零帧数字字符块 {c11} 应 ≤3");
        Assert.True(c13 > 3, $"动态帧(金币未归零)数字字符块 {c13} 应 >3");
        Assert.True(c12 > 3, $"有值帧数字字符块 {c12} 应 >3");
    }
}
