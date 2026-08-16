# Task 6 Report: 死代码与常量清理

## Status: DONE

## Changes Made

### 1. Form1.cs — deleted two unused parameterless overloads
- Deleted `private bool IsGunCountEnough()` (no-arg, called `CaptureToBitmap()` then delegated). Kept the `IsGunCountEnough(Bitmap screen)` overload.
- Deleted `private bool IsBattleResourceZero()` (no-arg, same pattern). Kept the `IsBattleResourceZero(Bitmap screen)` overload.

### 2. Form1.cs — ExecuteLogic() unused local
- Deleted `int succeed = 0;` and its `succeed++;` line from `ExecuteLogic()` (was only incremented, never read).
- `打资源()`'s `succeed` left untouched — it is read in `BeginInvoke(() => { this.Text = $"已打{succeed}次"; })` (verified still present at lines 433/519-520).

### 3. ScreenCapturer.cs — comment only, no code change
- Added one-line comment above `BuildCommand`'s `_deviceSerial != null` ternary: `// 当前恒走 serial 分支：_deviceSerial 构造即固定 emulator-5554 且永不置空，null 分支仅防御性保留`. No code modified.

## Caller verification (before deleting)
- `grep IsBattleResourceZero()` across `D:\Code\脚本`: only the definition itself (now deleted). Real call sites all pass a Bitmap: `IsBattleResourceZero(screen)`.
- `grep IsGunCountEnough()` across `D:\Code\脚本`: only the definition itself (now deleted). Real call sites pass a Bitmap: `IsGunCountEnough(screen)` (two sites).
- Post-edit grep confirms: only Bitmap-overload definitions remain and are called with a `screen` argument.
- `Form1.cs` is NOT in the test csproj `<Compile Include>` list (test project links only GameLevelRecognizer/ScreenCapturer/TemplateMatcher/Locationinformation/TextParsing/ImageProcessing), and both methods are `private`, so no test-assembly reference risk.

## Build / Test results
- `dotnet build "D:\Code\脚本\脚本.csproj"` → SUCCESS, 0 errors, 2 warnings (pre-existing CS8625 in ScreenCapturer.cs lines 316/319 — unchanged, expected).
- `dotnet test "D:\Code\脚本\tests\脚本.Tests\脚本.Tests.csproj"` → 39/39 passed, 0 failed, 0 skipped.

## Files changed
- `D:\Code\脚本\Form1.cs` (3 edits: 2 method deletions, 1 unused-local removal)
- `D:\Code\脚本\ScreenCapturer.cs` (1 comment added, no code change)
- `D:\Code\脚本\.claude\sdd\task-6-report.md` (this report)

## Concerns
- None. Plan Step 2 also mentions `LdPlayerCapturer.Drag`'s deprecated `holdTime` param — kept signature per plan (out of scope, comment already explains deprecation).
