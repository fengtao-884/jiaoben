# Task 5 Report: Locationinformation 全覆盖 + TemplateMatcher.FindAllMatches 合成 NMS 测试

**Status: DONE_WITH_CONCERNS**

## What Was Added

### `tests\脚本.Tests\LocationinformationTests.cs` (appended, existing 3 tests kept)
- `[Fact] 所有点击坐标_在1920x1080内()` — asserts all 12 click points (Home, MoonMark, Center, FindEnemy, NextEnemy, Hero, Return, Retreat, 作战中心, 军备收集, 开始战斗, 开始防御) have x in [0,1920] and y in [0,1080].
- `[Fact] 所有识别区域_宽高为正且在屏内()` — asserts all 5 areas (LevelArea, VictoryArea, 战斗胜利, 敌人名称, 战斗金币数字) have positive width/height and right/bottom edges within [0,1920]x[0,1080].
- `[Fact] HeroPosition_所有英雄坐标_在屏内()` — asserts all 5 HeroPosition entries are in-bounds.

### `tests\脚本.Tests\TemplateMatcherTests.cs` (appended, existing 6 tests kept; added `using System.Drawing;`)
- `[Fact] FindAllMatches_合成三目标_返回3个且去重()` — synthetic scene: 20x20 white `Bitmap` template, 200x200 `CV_8UC1` black scene with three 20x20 white squares at (10,10),(100,10),(10,100), then `FindAllMatches(scene, 0.9, 0.8, 1.2)`. Asserts `Assert.Equal(3, matches.Count)` and all `Score >= 0.9`.

## Plan Warts Resolved (as directed by controller)
1. Plan's two coordinate `[Theory]` tests (unused `_` string param, body loops all coords anyway) collapsed into single `[Fact]` each. Loop bodies reference `Locationinformation.X` members directly, preserving compile-time member-name safety.
2. Plan code used tuple field names `r.w`/`r.h`, but the actual source `Locationinformation.cs` declares areas as `(int x, int y, int width, int height)`. Adapted to `r.width`/`r.height`. No coordinate VALUES were changed.
3. Verified `Scalar.White`/`Scalar.Black` exist in OpenCvSharp 4.11.0.20250507 (via DLL metadata check) — used as in plan.

## Test Results

`dotnet test "D:\Code\脚本\tests\脚本.Tests\脚本.Tests.csproj"`

- Total: 39 tests, **38 passed, 1 failed**, 0 skipped (~3s).
- All 35 pre-existing tests pass. All 3 new Locationinformation tests pass.
- **Failing:** `TemplateMatcherTests.FindAllMatches_合成三目标_返回3个且去重` — `Assert.Equal` Expected: 3, Actual: **441**.

## Concern: FindAllMatches Synthetic Test Yields 441, Not 3 (root cause investigated)

**Root cause: constant-value (zero-variance) template makes `TM_CCOEFF_NORMED` degenerate, returning exactly 1.0 at every result position.** Evidence from a temporary diagnostic (replicated `FindAllMatches` internals with the exact same template/scene, then deleted):

- Template grayscale stats: `min=255 max=255 mean=255.000` — fully constant white, zero variance.
- Every scale in 0.8–1.2 reported `maxVal=1`.
- Best-scale (0.8, 16x16) match result: all **34225/34225** positions = exactly 1.0 (`nGte0.9=34225, nExact1=34225, nNaN=0, nInf=0`). CCoeffNormed is mathematically undefined for a zero-variance template (0/0); this OpenCV build evaluates it as 1.0 everywhere, regardless of scene content — so the score field carries no location information.
- NMS then keeps a 16x16-half-size grid: **441** matches, all Score=1.0, Bounds=16x16 (first kept at (0,0), then every 9px).

The Scalar/MatType single-channel setup itself is correct (verified: black CV_8UC1 background + three white rectangles) — it is NOT the cause.

Per controller instruction, the assertion was **NOT weakened** to make the test pass. The test stays as written in the plan and currently fails.

**Suggested fix (for controller/plan decision, not applied):** make the template non-constant so CCoeffNormed is well-defined — e.g. a white 20x20 square with a small black corner pixel (single-pixel variance) still matching the three scene squares at score ~0.99, which would yield exactly 3 after NMS. This changes only the template construction, not the assertion.

## Files Changed
- `D:\Code\脚本\tests\脚本.Tests\LocationinformationTests.cs` — appended 3 Facts.
- `D:\Code\脚本\tests\脚本.Tests\TemplateMatcherTests.cs` — added `using System.Drawing;`, appended 1 Fact.
- (temporary `_DiagnosticTest.cs` created and deleted; not in final tree)

No source/production code touched. No git (repo not initialized).

## Fix (constant-template → non-constant)

Applied the previously suggested fix in `tests\脚本.Tests\TemplateMatcherTests.cs`, method `FindAllMatches_合成三目标_返回3个且去重` only. Template and scene construction replaced; the two `Assert` lines (`Assert.Equal(3, matches.Count)` and `Assert.All(matches, m => Assert.True(m.Score >= 0.9))`) kept exactly as-is.

**Change:** template is now a 20x20 white square with a 2x2 black block in its top-left corner (non-zero variance, so `TM_CCOEFF_NORMED` is well-defined). The 200x200 black scene draws three exact copies of that template at (10,10), (100,10), (10,100) — white 20x20 square plus the matching 2x2 black block at each corner.

**Exact command:**
`dotnet test "D:\Code\脚本\tests\脚本.Tests\脚本.Tests.csproj" --filter FindAllMatches`

**Output:** 已通过! - 失败: 0，通过: 1，已跳过: 0，总计: 1，持续时间: 912 ms

**Full suite:**
`dotnet test "D:\Code\脚本\tests\脚本.Tests\脚本.Tests.csproj"`

**Output:** 已通过! - 失败: 0，通过: 39，已跳过: 0，总计: 39，持续时间: 3 s

The previously failing test now reports exactly **3** matches (previously 441), all with Score >= 0.9. Assertion NOT weakened. No other test or file touched. No git commands run.
