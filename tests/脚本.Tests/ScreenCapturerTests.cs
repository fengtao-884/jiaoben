// D:\Code\脚本\tests\脚本.Tests\ScreenCapturerTests.cs
using Xunit;
using 脚本;

namespace 脚本.Tests;

public class ScreenCapturerTests
{
    [Fact]
    public void BuildCommand_带设备序列号()
    {
        var c = new ScreenCapturer(@"C:\fake\adb.exe");
        Assert.Equal("-s emulator-5554 shell input tap 1 2", c.BuildCommand("shell input tap 1 2"));
    }

    [Fact]
    public void CloseApp_空包名_不抛异常()
    {
        // 空包名直接 return，不应触发 ADB 调用
        var c = new LdPlayerCapturer();
        var ex = Record.Exception(() => c.CloseApp(""));
        Assert.Null(ex);
    }
}
