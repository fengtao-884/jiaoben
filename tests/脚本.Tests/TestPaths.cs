namespace 脚本.Tests;

/// <summary>
/// 测试资源路径（开发机环境；tessdata/截图/模板来自主项目输出与实验目录）
/// </summary>
public static class TestPaths
{
    public static readonly string Root = @"D:\Code\脚本";
    public static readonly string TessData = Path.Combine(Root, "bin", "Debug", "net8.0-windows", "tessdata");
    public static readonly string Screenshots = Path.Combine(Root, "bin", "Debug", "net8.0-windows", "Screenshots");
    public static readonly string Templates = Path.Combine(Root, "Templates");

    // 实验素材（matchtest 目录）
    public static readonly string Scene4 = @"C:\Users\ft\AppData\Local\Temp\opencode\matchtest\scene4.png";   // 卫星站在 (772,723) 的基地图
    public static readonly string Scene6 = @"C:\Users\ft\AppData\Local\Temp\opencode\matchtest\scene6.png";   // 含机枪群的基地全景（1920x1080）
    public static readonly string Scene11 = @"C:\Users\ft\AppData\Local\Temp\opencode\matchtest\scene11.png"; // 战斗归零帧（1920x1080）
    public static readonly string Scene12 = @"C:\Users\ft\AppData\Local\Temp\opencode\matchtest\scene12.png"; // 战斗有值帧（1920x1080）
    public static readonly string Scene13 = @"C:\Users\ft\AppData\Local\Temp\opencode\matchtest\scene13.png"; // 运行时取证（油料0、金币7位）

    // 运行取证图目录（debug_*.png 为用户手动"调试截图"收集的战斗画面样本）
    public static readonly string DebugShots = Path.Combine(Root, "bin", "Debug", "net8.0-windows", "Screenshots");
}
