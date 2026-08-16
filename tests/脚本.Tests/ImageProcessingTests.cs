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
    public void CountWhiteComponents_取证图_归零与有值区分()
    {
        // 产品判据阈值：≤5 判归零（实测：归零 3、动态(油料0金币7位) 16、有值 19）
        var region = new Rectangle(40, 200, 300, 160);
        using var s11 = new Bitmap(TestPaths.Scene11);   // 都归零
        using var s13 = new Bitmap(TestPaths.Scene13);   // 动态帧：油料0、金币7位
        using var s12 = new Bitmap(TestPaths.Scene12);   // 有值

        int c11 = ImageProcessing.CountWhiteComponents(s11, region);
        int c13 = ImageProcessing.CountWhiteComponents(s13, region);
        int c12 = ImageProcessing.CountWhiteComponents(s12, region);

        Assert.True(c11 <= 5, $"归零帧连通域 {c11} 应 ≤5");
        Assert.True(c13 > 5, $"动态帧(金币未归零)连通域 {c13} 应 >5");
        Assert.True(c12 > 5, $"有值帧连通域 {c12} 应 >5");
    }
}
