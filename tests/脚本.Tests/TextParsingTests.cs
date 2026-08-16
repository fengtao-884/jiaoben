// D:\Code\脚本\tests\脚本.Tests\TextParsingTests.cs
using Xunit;
using 脚本;

namespace 脚本.Tests;

public class TextParsingTests
{
    [Theory]
    [InlineData("123", 123)]
    [InlineData("12%", 12)]
    [InlineData("0", 0)]
    [InlineData("abc42def", 42)]
    [InlineData("100", 100)]
    public void ParseNumber_合法输入_返回数字(string text, int expected) =>
        Assert.Equal(expected, TextParsing.ParseNumber(text));

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("无数字")]
    [InlineData(null)]
    public void ParseNumber_无数字_返回负1(string text) =>
        Assert.Equal(-1, TextParsing.ParseNumber(text));

    [Fact]
    public void RemoveSpaces_去空白()
    {
        Assert.Equal("战斗胜利", TextParsing.RemoveSpaces("战 斗 胜 利"));
        Assert.Equal("abc", TextParsing.RemoveSpaces(" a b\nc "));
    }

    [Fact]
    public void RemoveSpaces_空或null_原样返回()
    {
        Assert.Equal("", TextParsing.RemoveSpaces(""));
        Assert.Null(TextParsing.RemoveSpaces(null!));
    }
}
