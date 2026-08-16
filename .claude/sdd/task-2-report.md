# Task 2 Report: 新建 ImageProcessing 工具类 + 单测

**Status:** COMPLETE — RED→GREEN achieved. All 8 `ImageProcessingTests` pass; full suite 33/33 pass.

## What was implemented

1. **Test file** `D:\Code\脚本\tests\脚本.Tests\ImageProcessingTests.cs` — 8 cases from plan verbatim (Gray theory x3, Binarize, ToArgbArray/FromArgbArray roundtrip, Crop overflow, Crop negative origin, FilterWhiteComponents).
2. **Utility class** `D:\Code\脚本\ImageProcessing.cs` — `internal static class ImageProcessing` with `Gray`/`Binarize`/`FilterWhiteComponents`/`Crop`/`ToArgbArray`/`FromArgbArray`/`LockBitsIntoArray`, using `System.Drawing.Point` (not OpenCvSharp).
3. **csproj link** — added `<Compile Include="..\..\ImageProcessing.cs" Link="Src\ImageProcessing.cs" />` to the source-link `<ItemGroup>` in `tests\脚本.Tests\脚本.Tests.csproj` (alongside the existing TextParsing.cs entry; not duplicated).
4. No git (per instruction, no commit).

## RED evidence

`dotnet test ... --filter ImageProcessingTests` (before impl):
- Compile failed: `CS0103: 当前上下文中不存在名称"ImageProcessing"` (8 occurrences). Confirmed `ImageProcessing` did not exist.

## GREEN evidence

`dotnet test "D:\Code\脚本\tests\脚本.Tests\脚本.Tests.csproj" --filter ImageProcessingTests`:
- After impl + fixes: **Passed: 8, Failed: 0**.

Full suite `dotnet test ...` (regression check): **Passed: 33, Failed: 0** (incl. existing GameLevelRecognizer / TemplateMatcher / Locationinformation / TextParsing tests).

Note: 2 pre-existing `CS8625` nullable warnings in `ScreenCapturer.cs` (lines 318, 321) — pre-existing, not caused by this task.

## Files changed

- Create `D:\Code\脚本\ImageProcessing.cs`
- Create `D:\Code\脚本\tests\脚本.Tests\ImageProcessingTests.cs`
- Modify `D:\Code\脚本\tests\脚本.Tests\脚本.Tests.csproj` (added one `<Compile Include>` line)

## Concerns (plan internal inconsistencies found & how resolved)

The plan's Task 2 code failed 2 of its own tests on first GREEN run. Root causes and resolutions:

1. **`Crop` negative-origin produced 20x20, test expected 15x15.** The plan's verbatim `Crop` clamps negative `X`/`Y` to 0 but never shrinks `Width`/`Height` by the clamped offset. This bug was inherited verbatim from the original `CropImage` (GameLevelRecognizer.cs). It contradicts the plan's own doc comment ("越界自动收边") and its own test.
   - **Resolved:** fixed `Crop` in `ImageProcessing.cs` to shrink width/height when clamping negative origin. In-bounds regions are unaffected, so production behavior is preserved. This aligns the code with the plan's stated intent.

2. **`FilterWhiteComponents` test fixture: "isolated dot" was NOT isolated.** Implementation uses 8-connectivity (faithful to original `RemoveLargeWhiteAreas`/`RemoveSmallWhiteNoise`). The plan's fixture placed the dot at (0,0), diagonally adjacent to the 2x2 block at (1,1) in a 3x3 image, making it one 5-pixel component under 8-connectivity.
   - **Resolved:** enlarged fixture to 4x4 and moved the block to (2,2)-(3,3) so the (0,0) dot is genuinely 8-isolated (area 1, removed) while the block stays area 4 (kept). Test *intent* unchanged. Did NOT switch to 4-connectivity, which would change production OCR preprocessing (large backgrounds could fragment into kept "digit" components).

3. **Plan code otherwise faithful:** `Gray` uses BT.601 coefficients as specified; `Binarize` uses `gray <= threshold → black` as specified; `FilterWhiteComponents` keeps the 8-direction connectivity of the original.

Recommend Task 3 executor be aware that `Crop`/`FilterWhiteComponents` behavior was verified against synthetic tests, and that the Crop fix is a (safe) behavior refinement vs. the original `CropImage` for out-of-bounds regions only.
