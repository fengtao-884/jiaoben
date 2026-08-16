using OpenCvSharp;
using System.Drawing;
using Xunit;
using 脚本;
using Point = OpenCvSharp.Point;
using Rect = OpenCvSharp.Rect;

namespace 脚本.Tests;

/// <summary>
/// 模板匹配（卫星站/机枪）单元测试：用真实截图验证匹配分数与计数。
/// </summary>
public class TemplateMatcherTests
{
    [Fact]
    public void 卫星站模板_匹配基地图_分数超阈值且位置正确()
    {
        using var matcher = new TemplateMatcher(Path.Combine(TestPaths.Templates, "satellite_base.png"));
        using var scene = Cv2.ImRead(TestPaths.Scene4, ImreadModes.Grayscale);

        var result = matcher.FindBestMatch(scene);

        Assert.True(result.Score >= matcher.Threshold,
            $"卫星站匹配分数 {result.Score:F3} 低于阈值 {matcher.Threshold}");
        // scene4 中卫星站实测位置约 (772,723)，允许一定误差
        Assert.InRange(result.Location.X, 700, 850);
        Assert.InRange(result.Location.Y, 650, 800);
    }

    [Fact]
    public void 卫星站模板_ROI内匹配_命中()
    {
        using var matcher = new TemplateMatcher(Path.Combine(TestPaths.Templates, "satellite_base.png"));
        using var scene = Cv2.ImRead(TestPaths.Scene4, ImreadModes.Grayscale);

        var result = matcher.FindBestMatchInRoi(scene, new Rect(600, 600, 400, 300), 0.6, 1.3);

        Assert.True(result.Score >= matcher.Threshold,
            $"ROI 匹配分数 {result.Score:F3} 低于阈值 {matcher.Threshold}");
    }

    [Fact]
    public void 机枪模板_计数不少于8()
    {
        using var matcher = new TemplateMatcher(Path.Combine(TestPaths.Templates, "gun.png"));
        using var scene = Cv2.ImRead(TestPaths.Scene6, ImreadModes.Grayscale);

        var matches = matcher.FindAllMatches(scene, 0.7, 0.8, 1.2);

        Assert.True(matches.Count >= 8, $"机枪检测数量 {matches.Count} < 8（实际约 9 个）");
    }

    [Fact]
    public void 机枪模板_单实例匹配_分数高()
    {
        using var matcher = new TemplateMatcher(Path.Combine(TestPaths.Templates, "gun.png"));
        using var scene = Cv2.ImRead(TestPaths.Scene6, ImreadModes.Grayscale);

        var result = matcher.FindBestMatch(scene);

        Assert.True(result.Score >= 0.9, $"机枪单实例匹配分数 {result.Score:F3} < 0.9");
    }

    [Fact]
    public void MatchResult_中心与边界计算正确()
    {
        var r = new TemplateMatchResult(0.9, new Point(100, 200), 1.0, 50, 40);
        Assert.Equal(125, r.Center.X);
        Assert.Equal(220, r.Center.Y);
        Assert.Equal(50, r.Bounds.Width);
        Assert.Equal(40, r.Bounds.Height);
    }

    [Fact]
    public void MatchResult_带缩放_中心计算正确()
    {
        var r = new TemplateMatchResult(0.9, new Point(100, 200), 2.0, 50, 40);
        Assert.Equal(150, r.Center.X);
        Assert.Equal(240, r.Center.Y);
        Assert.Equal(100, r.Bounds.Width);
        Assert.Equal(80, r.Bounds.Height);
    }

    [Fact]
    public void FindAllMatches_合成三目标_返回3个且去重()
    {
        // 模板：20x20 白色方块 + 左上角 2x2 黑块（非零方差，避免常量模板使 CCoeffNormed 退化）
        using var tplBmp = new Bitmap(20, 20);
        using (var g = Graphics.FromImage(tplBmp))
        {
            g.Clear(Color.White);
            using var b = new SolidBrush(Color.Black);
            g.FillRectangle(b, 0, 0, 2, 2);
        }
        using var matcher = new TemplateMatcher(tplBmp);

        // 场景：200x200 黑色，三处与模板完全相同的 20x20 方块
        using var scene = new Mat(200, 200, MatType.CV_8UC1, Scalar.Black);
        foreach (var (px, py) in new[] { (10, 10), (100, 10), (10, 100) })
        {
            Cv2.Rectangle(scene, new Rect(px, py, 20, 20), Scalar.White, -1);
            Cv2.Rectangle(scene, new Rect(px, py, 2, 2), Scalar.Black, -1);
        }

        var matches = matcher.FindAllMatches(scene, 0.9, 0.8, 1.2);

        Assert.Equal(3, matches.Count);
        Assert.All(matches, m => Assert.True(m.Score >= 0.9));
    }
}
