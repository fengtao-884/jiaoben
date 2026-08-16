// D:\Code\脚本\TextParsing.cs
using System.Text.RegularExpressions;

namespace 脚本;

/// <summary>
/// 纯文本解析函数集合（无状态，便于单测）。
/// </summary>
internal static class TextParsing
{
    /// <summary>
    /// 从 OCR 结果文本提取整数：去 %、直接解析、失败后提取数字字符；无数字返回 -1。
    /// </summary>
        public static int ParseNumber(string? text)
    {
        if (string.IsNullOrEmpty(text)) return -1;
        string t = text.Replace("%", "");
        if (int.TryParse(t, out int n)) return n;
        string digits = new string(t.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out n) ? n : -1;
    }

    /// <summary>去掉所有空白字符；空/ null 原样返回。</summary>
    public static string RemoveSpaces(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        return Regex.Replace(text, @"\s+", "");
    }
}
