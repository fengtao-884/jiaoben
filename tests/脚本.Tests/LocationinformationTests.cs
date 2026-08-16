using Xunit;
using 脚本;

namespace 脚本.Tests;

/// <summary>
/// 坐标常量 sanity 测试：防误改导致越界/非法区域。
/// </summary>
public class LocationinformationTests
{
    [Fact]
    public void 区域坐标_宽高为正()
    {
        Assert.True(Locationinformation.LevelArea.width > 0);
        Assert.True(Locationinformation.LevelArea.height > 0);
        Assert.True(Locationinformation.VictoryArea.width > 0);
        Assert.True(Locationinformation.VictoryArea.height > 0);
        Assert.True(Locationinformation.战斗资源区.width > 0);
        Assert.True(Locationinformation.战斗资源区.height > 0);
    }

    [Fact]
    public void 战斗资源区_在1920x1080屏幕内()
    {
        var r = Locationinformation.战斗资源区;
        Assert.True(r.x >= 0 && r.y >= 0);
        Assert.True(r.x + r.width <= 1920, "资源区右边界越界");
        Assert.True(r.y + r.height <= 1080, "资源区下边界越界");
    }

    [Fact]
    public void 点击坐标_在屏幕内()
    {
        Assert.True(Locationinformation.FindEnemy.x > 0 && Locationinformation.FindEnemy.y > 0);
        Assert.True(Locationinformation.NextEnemy.x > 0 && Locationinformation.NextEnemy.y > 0);
        Assert.True(Locationinformation.Retreat.x > 0 && Locationinformation.Retreat.y > 0);
    }

    [Fact]
    public void 所有点击坐标_在1920x1080内()
    {
        // 逐项显式断言（编译器校验成员名，防误改名）
        foreach (var (x, y) in new (int x, int y)[]
        {
            Locationinformation.Home, Locationinformation.MoonMark, Locationinformation.Center,
            Locationinformation.FindEnemy, Locationinformation.NextEnemy, Locationinformation.Hero,
            Locationinformation.Return, Locationinformation.Retreat, Locationinformation.作战中心,
            Locationinformation.军备收集, Locationinformation.开始战斗, Locationinformation.开始防御
        })
        {
            Assert.InRange(x, 0, 1920);
            Assert.InRange(y, 0, 1080);
        }
    }

    [Fact]
    public void 所有识别区域_宽高为正且在屏内()
    {
        foreach (var r in new (int x, int y, int width, int height)[]
        {
            Locationinformation.LevelArea, Locationinformation.VictoryArea, Locationinformation.战斗胜利,
            Locationinformation.敌人名称, Locationinformation.战斗资源区
        })
        {
            Assert.True(r.width > 0 && r.height > 0);
            Assert.InRange(r.x + r.width, 0, 1920);
            Assert.InRange(r.y + r.height, 0, 1080);
        }
    }

    [Fact]
    public void HeroPosition_所有英雄坐标_在屏内()
    {
        Assert.All(Locationinformation.HeroPosition, p =>
        {
            Assert.InRange(p.x, 0, 1920);
            Assert.InRange(p.y, 0, 1080);
        });
    }
}
