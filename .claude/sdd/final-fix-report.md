# 终审修复报告 (Final-Review Fix Report)

日期: 2026-08-16
项目: D:\Code\脚本 (.NET 8 WinForms)

## 修复总览

| 修复 | 文件 | 结果 |
|------|------|------|
| Fix 1: `ExecuteAdbCommand` 排空 stdout | `D:\Code\脚本\ScreenCapturer.cs` | 完成 |
| Fix 2: `.Result` → `.GetAwaiter().GetResult()` | `D:\Code\脚本\ScreenCapturer.cs` | 完成 |
| Fix 3: 新增 3 个单元测试 | `tests\脚本.Tests\ImageProcessingTests.cs`, `tests\脚本.Tests\TextParsingTests.cs` | 完成 |

---

## Fix 1 (Important): 排空 stdout — 修复 `ExecuteAdbCommand` 死锁风险

`ExecuteAdbCommand` 原本只异步读 stderr，stdout 被重定向但从未读取，缓冲区写满时会与 `WaitForExit` 死锁。现改为先并发启动 stdout/stderr 两个异步读取，再等待进程退出。

文件: `D:\Code\脚本\ScreenCapturer.cs`

修改后方法体（`process == null` 保护之后）：

```csharp
            if (process == null) throw new Exception("无法启动ADB进程");
            // 先并发读 stdout/stderr 再等退出，避免任一缓冲写满导致死锁
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(3000))
            {
                try { process.Kill(); } catch { /* 已退出 */ }
                throw new Exception($"ADB命令超时: {arguments}");
            }
            if (process.ExitCode != 0)
                throw new Exception($"ADB命令执行失败: {stderr.GetAwaiter().GetResult()}");
```

`using var process = Process.Start(...)` 及 processInfo（`Arguments`、`RedirectStandardOutput = true`、`RedirectStandardError = true`、`CreateNoWindow = true`）保持原样。

## Fix 2 (Minor): `.Result` → `.GetAwaiter().GetResult()` 清扫

文件: `D:\Code\脚本\ScreenCapturer.cs`，全部阻塞式 `.Result` 替换完毕：

- `ExecuteAdbCommand`: `stderr.Result` 已在 Fix 1 中一并改为 `stderr.GetAwaiter().GetResult()`。
- `ExecuteAdbCommandWithOutput`:
  - `string output = stdoutTask.Result;` → `string output = stdoutTask.GetAwaiter().GetResult();`
  - `Debug.WriteLine($"ADB命令执行失败: {stderrTask.Result}");` → `Debug.WriteLine($"ADB命令执行失败: {stderrTask.GetAwaiter().GetResult()}");`
- `CaptureToBitmap`:
  - `throw new Exception($"截图失败: {stderrTask.Result}");` → `throw new Exception($"截图失败: {stderrTask.GetAwaiter().GetResult()}");`
  - 其中 `stdoutTask.GetAwaiter().GetResult()` 原本已正确，未改动。

## Fix 3 (Minor): 新增 3 个单元测试

### `tests\脚本.Tests\ImageProcessingTests.cs`

新增 2 个测试：

```csharp
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
```

验证依据（对应 `ImageProcessing.cs`）：
- `Crop`：`region.Right = 210 > source.Width = 100` → `region.Width = 100 - 200 = -100` → `Width <= 0` → 抛 `ArgumentOutOfRangeException`（第 95-96 行）。
- `Binarize`：`Gray(128,128,128) = (int)(128*0.299 + 128*0.587 + 128*0.114) = 128`，`128 <= 128` → 置黑（第 26 行）。

### `tests\脚本.Tests\TextParsingTests.cs`

`ParseNumber_无数字_返回负1` 理论增加 `[InlineData(null)]`：

```csharp
    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("无数字")]
    [InlineData(null)]
    public void ParseNumber_无数字_返回负1(string text) =>
        Assert.Equal(-1, TextParsing.ParseNumber(text));
```

验证依据（`TextParsing.cs`）：`ParseNumber` 首行 `if (string.IsNullOrEmpty(text)) return -1;`，null 走该分支返回 -1。

---

## 验证

### 构建主项目

命令:
```
dotnet build "D:\Code\脚本\脚本.csproj" --nologo
```

输出:
```
  脚本 -> D:\Code\脚本\bin\Debug\net8.0-windows\脚本.dll
已成功生成。
    2 个警告
    0 个错误
已用时间 00:00:03.28
```

结论: 0 错误；2 个 CS8625 警告（`ScreenCapturer.cs` 317、320 行，既有，非本次改动引入）。

### 全量测试套件

命令:
```
dotnet test "D:\Code\脚本\tests\脚本.Tests\脚本.Tests.csproj" --nologo --no-restore
```

输出:
```
已通过! - 失败:     0，通过:    42，已跳过:     0，总计:    42，持续时间: 3 s - 脚本.Tests.dll (net8.0)
```

结论: 42/42 通过（39 既有 + 3 新增），0 失败，0 跳过。

补充说明: `dotnet test` 新增一条 xUnit1012 analyzer 警告（`TextParsingTests.cs` 22 行，提示 null 传给非 nullable string 参数）——由任务明确要求的 `[InlineData(null)]` 产生，属警告非错误，不影响构建/测试结果。

---

## 状态: DONE
