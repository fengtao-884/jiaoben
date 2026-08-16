using System.Diagnostics;
using Tesseract;

namespace 脚本
{
    public class GetNumberRecognizer : IDisposable
    {
        private TesseractEngine _tesseractEngine;
        private TesseractEngine? _tesseractEngineChinese;
        private readonly LdPlayerCapturer _capturer;
        public GetNumberRecognizer(LdPlayerCapturer ldPlayerCapturer)
        {
            _capturer = ldPlayerCapturer;
            // 初始化Tesseract OCR（放在tessdata文件夹中）
            string tessDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
            _tesseractEngine = new TesseractEngine(tessDataPath, "eng", EngineMode.Default);
            _tesseractEngine.SetVariable("tessedit_char_whitelist", "0123456789%");
            // 注意：chi_sim 引擎改为懒加载——实测同一进程同时创建 eng + chi_sim 两个引擎，
            // 会破坏 eng 引擎的 SingleLine 模式识别（OCR 返回空）。用到中文识别时才创建。
        }

        private readonly object _chineseLock = new object();

        /// <summary>
        /// 中文引擎（懒加载：首次使用中文识别时创建，避免与英文引擎共存导致识别失效）
        /// </summary>
        private TesseractEngine ChineseEngine
        {
            get
            {
                if (_tesseractEngineChinese == null)
                {
                    lock (_chineseLock)
                    {
                        if (_tesseractEngineChinese == null)
                        {
                            string tessDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
                            _tesseractEngineChinese = new TesseractEngine(tessDataPath, "chi_sim", EngineMode.Default);
                            _tesseractEngineChinese.SetVariable("tessedit_char_blacklist", "");
                            _tesseractEngineChinese.SetVariable("textord_min_linesize", "2.5");
                        }
                    }
                }
                return _tesseractEngineChinese;
            }
        }

        public int GetNumber(int x, int y, int width, int height, bool isRemoveNoise = true)
        {
            using var screen = _capturer.CaptureToBitmap();
            return GetNumber(screen, x, y, width, height, isRemoveNoise);
        }

        /// <summary>
        /// 在给定截图上识别指定区域的数字（复用截图，避免重复截屏）
        /// </summary>
        public int GetNumber(Bitmap screen, int x, int y, int width, int height, bool isRemoveNoise = true)
        {
            try
            {
                using var cropped = ImageProcessing.Crop(screen, new Rectangle(x, y, width, height));
                using var processed = PreprocessImage(cropped, isRemoveNoise);
                return TextParsing.ParseNumber(RecognizeText(processed));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"识别等级失败: {ex.Message}");
            }
            return -1;
        }

        /// <summary>
        /// 预处理图像：BT.601 二值化（阈值 128）后可选做白色连通域过滤
        /// （移除 >100 像素的大块白色背景、移除 &lt;30 像素的小噪点）。
        /// </summary>
        private static Bitmap PreprocessImage(Bitmap image, bool isRemoveNoise = true)
        {
            var binary = ImageProcessing.Binarize(image, 128);
            if (!isRemoveNoise) return binary;
            using (binary)
            {
                using var withoutLargeWhite = ImageProcessing.FilterWhiteComponents(binary, count => count <= 100);
                return ImageProcessing.FilterWhiteComponents(withoutLargeWhite, count => count >= 30);
            }
        }

        /// <summary>
        /// 使用Tesseract识别文字（psm 可指定页面分割模式，单行数字建议 SingleLine）
        /// </summary>
        private string RecognizeText(Bitmap image, PageSegMode psm = PageSegMode.Auto)
        {
            try
            {
                using (var pix = PixConverter.ToPix(image))
                using (var page = _tesseractEngine.Process(pix, psm))
                {
                    string text = page.GetText();
                    return text.Trim(); // 去除空格和换行
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"OCR识别失败: {ex.Message}");
                return "";
            }
        }

        public void Dispose()
        {
            _tesseractEngine?.Dispose();
            _tesseractEngineChinese?.Dispose();
        }
        public string GetText(int x, int y, int width, int height, bool useChinese = true, int threshold = 128)
        {
            using var screen = _capturer.CaptureToBitmap();
            return GetText(screen, x, y, width, height, useChinese, threshold);
        }

        /// <summary>
        /// 在给定截图上识别指定区域的文字（复用截图，避免重复截屏）
        /// </summary>
        public string GetText(Bitmap screen, int x, int y, int width, int height, bool useChinese = true, int threshold = 128)
        {
            try
            {
                using var cropped = ImageProcessing.Crop(screen, new Rectangle(x, y, width, height));
                using var binary = ImageProcessing.Binarize(cropped, threshold);
                using var pix = PixConverter.ToPix(binary);
                if (useChinese)
                {
                    using var page = ChineseEngine.Process(pix);
                    return TextParsing.RemoveSpaces(page.GetText().Trim());
                }
                using var pageEn = _tesseractEngine.Process(pix);
                return pageEn.GetText().Trim();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"识别文字失败: {ex.Message}");
                return string.Empty;
            }
        }
    }
}
