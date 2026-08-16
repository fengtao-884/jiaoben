# Task 1 Report: 新建 `TextParsing` 工具类 + 单测

## Status: DONE

## What Was Implemented

Implemented plan Task 1 of `docs/superpowers/plans/2026-08-16-单元测试与代码优化.md` verbatim, following RED→GREEN TDD:

- Created `D:\Code\脚本\tests\脚本.Tests\TextParsingTests.cs` — xUnit tests for `TextParsing` (5 theory cases `ParseNumber_合法输入_返回数字`, 3 theory cases `ParseNumber_无数字_返回负1`, 2 facts `RemoveSpaces_去空白` / `RemoveSpaces_空或null_原样返回`).
- Created `D:\Code\脚本\TextParsing.cs` — `internal static class TextParsing` in namespace `脚本` with `ParseNumber(string)` and `RemoveSpaces(string)`, exactly as in the plan.
- Modified `D:\Code\脚本\tests\脚本.Tests\脚本.Tests.csproj` — added `<Compile Include="..\..\TextParsing.cs" Link="Src\TextParsing.cs" />` inside the existing `<!-- 源码链接 -->` `<ItemGroup>` (after the `Locationinformation.cs` entry), matching the existing per-file include pattern.

## Test Results (TDD Evidence)

### RED (test written, implementation absent)

Command:
```
dotnet test "D:\Code\脚本\tests\脚本.Tests\脚本.Tests.csproj" --filter TextParsingTests
```
Exit code 1 — compile failure as expected. Key output:
```
D:\Code\脚本\tests\脚本.Tests\TextParsingTests.cs(16,32): error CS0103: 当前上下文中不存在名称“TextParsing”
D:\Code\脚本\tests\脚本.Tests\TextParsingTests.cs(23,26): error CS0103: 当前上下文中不存在名称“TextParsing”
D:\Code\脚本\tests\脚本.Tests\TextParsingTests.cs(28,30): error CS0103: 当前上下文中不存在名称“TextParsing”
D:\Code\脚本\tests\脚本.Tests\TextParsingTests.cs(29,29): error CS0103: 当前上下文中不存在名称“TextParsing”
D:\Code\脚本\tests\脚本.Tests\TextParsingTests.cs(35,26): error CS0103: 当前上下文中不存在名称“TextParsing”
D:\Code\脚本\tests\脚本.Tests\TextParsingTests.cs(36,21): error CS0103: 当前上下文中不存在名称“TextParsing”
```
Failed for the correct reason: `TextParsing` does not exist yet (6 CS0103 errors, all referencing the missing type). Also emitted 2 pre-existing CS8625 nullable warnings in `ScreenCapturer.cs` (unrelated to this task, present before).

### GREEN (implementation created + linked into test csproj)

Command (identical to RED):
```
dotnet test "D:\Code\脚本\tests\脚本.Tests\脚本.Tests.csproj" --filter TextParsingTests
```
```
已通过! - 失败: 0，通过: 10，已跳过: 0，总计: 10，持续时间: 74 ms - 脚本.Tests.dll (net8.0)
```
All 10 test cases passed. (Plan Step 4 states "全部 PASS（9 个用例）" — actual count is 10: 5 + 3 theory cases + 2 facts. Minor count discrepancy in the plan text; all cases pass.)

### Full-suite regression check

Command:
```
dotnet test "D:\Code\脚本\tests\脚本.Tests\脚本.Tests.csproj"
```
```
已通过! - 失败: 0，通过: 25，已跳过: 0，总计: 25，持续时间: 2 s - 脚本.Tests.dll (net8.0)
```
All 25 tests pass (10 new TextParsingTests + 15 existing). No regression from the csproj edit.

## Files Changed

1. Created `D:\Code\脚本\tests\脚本.Tests\TextParsingTests.cs`
2. Created `D:\Code\脚本\TextParsing.cs`
3. Modified `D:\Code\脚本\tests\脚本.Tests\脚本.Tests.csproj` (one line added)

## Concerns / Notes

- **csproj is NOT wildcard-based.** The task brief described linking via `<Compile Include="..\..\*.cs">`, but the actual `脚本.Tests.csproj` uses explicit per-file `<Compile Include>` entries. I added the `TextParsing.cs` entry per the plan's Step 5 to match the existing pattern. No duplicate-compile risk because there is no wildcard include.
- Plan says "9 个用例" but there are 10; all pass. Cosmetic only.
- 2 pre-existing warnings `CS8625` in `ScreenCapturer.cs` (lines 318, 321) appear in both RED and GREEN runs; not caused by this task.
- No `git commit` performed — per instructions, the directory is not a git repository.
- `TextParsing` is `internal`; because the test project links the source file directly (same assembly), the tests access it without `InternalsVisibleTo`, as expected.
- Ran `dotnet test` against the csproj directly (not the sln), so Task 0 sln integration is not required for this task.
