# Task 3 Report — GetNumberRecognizer 委托工具类 + 修 Bitmap 泄漏

**Status:** DONE

## What Changed

Modified only `D:\Code\脚本\GameLevelRecognizer.cs`:

1. **`GetNumber(Bitmap, ...)`** — replaced inline `%`-strip + double `int.TryParse` with `TextParsing.ParseNumber(RecognizeText(processed))`; `cropped`/`processed` now in `using` (previously leaked Bitmaps). Call site `PreprocessImage(cropped, isRemoveNoise)` updated for `static` method.

2. **`GetNumberDark(Bitmap, ...)`** — `DarkBinarize(cropped, darkThreshold)` → `ImageProcessing.Binarize(cropped, darkThreshold)`; parsing → `TextParsing.ParseNumber`. Kept `using`, 4x enlargement, and `SingleLine` mode. Behavior change (boundary): `gray < threshold` → `gray <= threshold` (pixels at exactly `gray == threshold` now turn black instead of white).

3. **`GetText(Bitmap, ...)`** — `cropped`/`binary`/`pix` in `using`; `BinaryImage(cropped, threshold)` → `ImageProcessing.Binarize(cropped, threshold)`; private `RemoveSpaces` → `TextParsing.RemoveSpaces`. Merged the `useChinese` if/else to use shared `pix` (both branches recognized from the same binary; English branch unchanged logic).

4. **`PreprocessImage`** — rewritten `private static`, delegates to `ImageProcessing.Binarize(image, 128)` + `FilterWhiteComponents(count => count <= 100)` + `FilterWhiteComponents(count => count >= 30)`. Intermediate `binary` disposed via `using (binary)`; `withoutLargeWhite` disposed via `using var`; returned result is a fresh Bitmap owned by the caller. This is a semantic-preserving equivalent extraction of the old `SimpleBinarization` + `RemoveLargeWhiteAreas(100)` + `RemoveSmallWhiteNoise(30)` chain (gray coefficients unified 0.3/0.59/0.11 → 0.299/0.587/0.114; boundary `gray > threshold` → `gray <= threshold`).

## Private Methods Deleted

- `DarkBinarize`
- `CropImage`
- `SimpleBinarization`
- `RemoveLargeWhiteAreas`
- `RemoveSmallWhiteNoise`
- `BinaryImage`
- `RemoveSpaces`
- `ToArgbArray`
- `FromArgbArray`
- `LockBitsIntoArray`

## Usings Removed

- `using System.Runtime.InteropServices;` — `Marshal.Copy` only lived in deleted methods; verified no remaining reference.
- `using Point = OpenCvSharp.Point;` — OpenCvSharp `Point` only used by deleted connected-component methods; verified no remaining reference.
- Also removed now-dead `using System.Drawing.Imaging;` (`PixelFormat`/`ImageLockMode` only in deleted methods) and `using System.Text.RegularExpressions;` (`Regex` only in deleted private `RemoveSpaces`). Verified via grep: no `Marshal`/`PixelFormat`/`Regex`/`OpenCvSharp` references remain.

## Unchanged

Ctor, `_chineseLock`/`ChineseEngine`, `RecognizeText`, `Dispose`, and the single-arg `GetNumber`/`GetNumberDark`/`GetText` screen-capture overloads. Public signatures (`GetNumber`/`GetNumberDark`/`GetText` × 2 overloads each) unchanged.

## Test Results

- **Full suite:** `dotnet test "D:\Code\脚本\tests\脚本.Tests\脚本.Tests.csproj"` → **33 passed, 0 failed, 0 skipped** (3 s).
- **OCR regression (`GameLevelRecognizerTests`):** all 6 `GetNumberDark` tests against real screenshots (zero/有值 time-line and control frames via Tesseract) PASS. The `gray <= threshold` boundary change did not alter any OCR result.
- **Main project:** `dotnet build "D:\Code\脚本\脚本.csproj"` → **0 errors** (2 pre-existing CS8625 warnings in `ScreenCapturer.cs:318/321`, unrelated to this change).

## Files Changed

- Modified: `D:\Code\脚本\GameLevelRecognizer.cs`

## Concerns

None functional. Notes for the record:
- `ImageProcessing.Crop` throws `ArgumentOutOfRangeException` on an empty region (old `CropImage` would let `Clone` throw); both are swallowed by the existing `try/catch` in `GetNumber`/`GetNumberDark`/`GetText`, so public behavior is preserved.
- Gray-coefficient unification and the `== threshold` boundary flip slightly change binarized pixels; covered and confirmed green by the real-screenshot `GetNumberDark` regression tests. Per plan Step 7, no further per-frame re-verification was needed since all tests pass unchanged.
- No git repo (per global constraints) — no commit performed.
