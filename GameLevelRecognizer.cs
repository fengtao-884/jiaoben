using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Tesseract;
using Point = OpenCvSharp.Point;

namespace 脚本
{
    public class GetNumberRecognizer : IDisposable
    {
        private TesseractEngine _tesseractEngine;
        private TesseractEngine _tesseractEngineChinese;
        private readonly LdPlayerCapturer _capturer;
        public GetNumberRecognizer(LdPlayerCapturer ldPlayerCapturer)
        {
            _capturer = ldPlayerCapturer;
            // 初始化Tesseract OCR（放在tessdata文件夹中）
            string tessDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
            _tesseractEngine = new TesseractEngine(tessDataPath, "eng", EngineMode.Default);
            _tesseractEngine.SetVariable("tessedit_char_whitelist", "0123456789%");

            string chiSimPath = Path.Combine(tessDataPath, "chi_sim.traineddata");
            _tesseractEngineChinese = new TesseractEngine(tessDataPath, "chi_sim", EngineMode.Default);

            _tesseractEngineChinese.SetVariable("tessedit_char_blacklist", "");
            _tesseractEngineChinese.SetVariable("textord_min_linesize", "2.5");

            
        }

        public int GetNumber(int x, int y, int width, int height,bool isRemoveNoise = true)
        {
            try
            {
                // 1. 截取整个屏幕
                using (var screen = _capturer.CaptureToBitmap())
                {
                    // 2. 截取指定区域
                    var region = new Rectangle(x, y, width, height);
                    var cropped = CropImage(screen, region);
                    
                    // 3. 预处理图像（提高识别率）
                    var processed = PreprocessImage(cropped, isRemoveNoise);

                    // 4. 使用Tesseract识别数字
                    string text = RecognizeText(processed);
                    text = text.Replace("%", "");
                    // 5. 提取数字
                    if (int.TryParse(text, out int level))
                    {
                        return level;
                    }
                    
                    // 如果直接解析失败，尝试提取数字字符
                    string numbers = new string(text.Where(char.IsDigit).ToArray());
                    if (int.TryParse(numbers, out level))
                    {
                        return level;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"识别等级失败: {ex.Message}");
            }
            
            return -1;
        }
       
        /// <summary>
        /// 截取图像区域
        /// </summary>
        private Bitmap CropImage(Bitmap source, Rectangle region)
        {
            // 确保区域不超出图像边界
            if (region.X < 0) region.X = 0;
            if (region.Y < 0) region.Y = 0;
            if (region.Right > source.Width) region.Width = source.Width - region.X;
            if (region.Bottom > source.Height) region.Height = source.Height - region.Y;
            
            var cropped = new Bitmap(region.Width, region.Height);
            
            using (Graphics g = Graphics.FromImage(cropped))
            {
                g.DrawImage(source, 0, 0, region, GraphicsUnit.Pixel);
            }
            
            return cropped;
        }

        private Bitmap PreprocessImage(Bitmap image,bool isRemoveNoise = true)
        {
            // 1. 先进行二值化
            var binary = SimpleBinarization(image);
         
            if(!isRemoveNoise)
            {
                return binary;
            }
            // 2. 去除小噪点
            var withoutLargeWhite = RemoveLargeWhiteAreas(binary, 100);

            // 3. 去除小白色噪点（数字内部的空洞或小斑点）
            var cleaned = RemoveSmallWhiteNoise(withoutLargeWhite, 30);

            return cleaned;
        }

        /// <summary>
        /// 简单二值化
        /// </summary>
        private Bitmap SimpleBinarization(Bitmap image)
        {
            int[] src = ToArgbArray(image);
            int[] result = new int[src.Length];

            int white = Color.White.ToArgb();
            int black = Color.Black.ToArgb();

            for (int i = 0; i < src.Length; i++)
            {
                int pixel = src[i];
                int r = (pixel >> 16) & 0xFF;
                int g = (pixel >> 8) & 0xFF;
                int b = pixel & 0xFF;

                // 计算灰度值
                int gray = (int)(r * 0.3 + g * 0.59 + b * 0.11);

                // 二值化（简单阈值）
                int threshold = 128;
                if (gray > threshold)
                    result[i] = white;  // 背景
                else
                    result[i] = black;  // 数字
            }

            return FromArgbArray(result, image.Width, image.Height);
        }

        /// <summary>
        /// 去除大面积白色区域（背景），保留小面积白色区域（数字）
        /// </summary>
        /// <param name="binary">二值图像</param>
        /// <param name="maxBackgroundArea">最大背景面积，大于这个面积的白色区域会被移除</param>
        private Bitmap RemoveLargeWhiteAreas(Bitmap binary, int maxBackgroundArea)
        {
            int width = binary.Width;
            int height = binary.Height;

            // 创建结果像素数组，初始化为黑色背景（数字颜色）
            int[] result = new int[width * height];
            Array.Fill(result, Color.Black.ToArgb()); // 黑色作为数字颜色

            int[] src = ToArgbArray(binary);

            // 访问标记数组
            bool[,] visited = new bool[width, height];

            // 8方向连通性检查（更全面的连通性）
            int[] dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
            int[] dy = { -1, -1, -1, 0, 0, 1, 1, 1 };

            int white = Color.White.ToArgb();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // 如果是白色像素（背景或数字）且未访问过
                    if (((src[y * width + x] >> 16) & 0xFF) == 255 && !visited[x, y])
                    {
                        var componentPixels = new List<Point>();
                        var stack = new Stack<Point>();

                        stack.Push(new Point(x, y));
                        visited[x, y] = true;

                        // 查找连通区域
                        while (stack.Count > 0)
                        {
                            Point p = stack.Pop();
                            componentPixels.Add(p);

                            // 检查8个方向
                            for (int i = 0; i < 8; i++)
                            {
                                int nx = p.X + dx[i];
                                int ny = p.Y + dy[i];

                                if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                                {
                                    if (((src[ny * width + nx] >> 16) & 0xFF) == 255 && !visited[nx, ny])
                                    {
                                        visited[nx, ny] = true;
                                        stack.Push(new Point(nx, ny));
                                    }
                                }
                            }
                        }

                        // 如果连通区域面积小于阈值，认为是数字，保留它
                        if (componentPixels.Count <= maxBackgroundArea)
                        {
                            foreach (var pixel in componentPixels)
                            {
                                result[pixel.Y * width + pixel.X] = white;
                            }
                        }
                        // 否则，这是一个大面积白色背景区域，不复制到结果中（保持黑色）
                    }
                }
            }

            return FromArgbArray(result, width, height);
        }
        private Bitmap RemoveSmallWhiteNoise(Bitmap image, int minNoiseArea)
        {
            int width = image.Width;
            int height = image.Height;

            // 创建结果像素数组，初始化为黑色背景
            int[] result = new int[width * height];
            Array.Fill(result, Color.Black.ToArgb());

            int[] src = ToArgbArray(image);

            // 访问标记数组
            bool[,] visited = new bool[width, height];

            // 8方向连通性检查
            int[] dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
            int[] dy = { -1, -1, -1, 0, 0, 1, 1, 1 };

            int white = Color.White.ToArgb();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // 如果是白色像素（数字或噪点）且未访问过
                    if (((src[y * width + x] >> 16) & 0xFF) == 255 && !visited[x, y])
                    {
                        var componentPixels = new List<Point>();
                        var stack = new Stack<Point>();

                        stack.Push(new Point(x, y));
                        visited[x, y] = true;

                        // 查找连通区域
                        while (stack.Count > 0)
                        {
                            Point p = stack.Pop();
                            componentPixels.Add(p);

                            // 检查8个方向
                            for (int i = 0; i < 8; i++)
                            {
                                int nx = p.X + dx[i];
                                int ny = p.Y + dy[i];

                                if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                                {
                                    if (((src[ny * width + nx] >> 16) & 0xFF) == 255 && !visited[nx, ny])
                                    {
                                        visited[nx, ny] = true;
                                        stack.Push(new Point(nx, ny));
                                    }
                                }
                            }
                        }

                        // 如果连通区域面积大于等于阈值，认为是数字部分，保留它
                        if (componentPixels.Count >= minNoiseArea)
                        {
                            foreach (var pixel in componentPixels)
                            {
                                result[pixel.Y * width + pixel.X] = white;
                            }
                        }
                        // 否则，这是一个小白色噪点，不复制到结果中（保持黑色）
                    }
                }
            }

            return FromArgbArray(result, width, height);
        }
        /// <summary>
        /// 使用Tesseract识别文字
        /// </summary>
        private string RecognizeText(Bitmap image)
        {
            try
            {
                using (var pix = PixConverter.ToPix(image))
                using (var page = _tesseractEngine.Process(pix))
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
            try
            {
                // 1. 截取整个屏幕
                using (var screen = _capturer.CaptureToBitmap())
                {
                    // 2. 截取指定区域
                    var region = new Rectangle(x, y, width, height);
                    var cropped = CropImage(screen, region);

                    // 3. 二值化处理
                    var binary = BinaryImage(cropped, threshold);

                    // 4. 使用Tesseract识别文字
                    using (var pix = PixConverter.ToPix(binary))
                    {
                        if (useChinese && _tesseractEngineChinese != null)
                        {
                            using (var page = _tesseractEngineChinese.Process(pix))
                            {
                                string text = page.GetText();
                                return RemoveSpaces(text.Trim());
                            }
                        }
                        else
                        {
                            using (var page = _tesseractEngine.Process(pix))
                            {
                                string text = page.GetText();
                                return text.Trim();
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"识别文字失败: {ex.Message}");
                return string.Empty;
            }
        }
        private string RemoveSpaces(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;
            return Regex.Replace(text, @"\s+", "");
        }
        /// <summary>
        /// 简单的二值化方法
        /// </summary>
        private Bitmap BinaryImage(Bitmap image, int threshold)
        {
            int[] src = ToArgbArray(image);
            int[] result = new int[src.Length];

            int white = Color.White.ToArgb();
            int black = Color.Black.ToArgb();

            for (int i = 0; i < src.Length; i++)
            {
                int pixel = src[i];
                int r = (pixel >> 16) & 0xFF;
                int g = (pixel >> 8) & 0xFF;
                int b = pixel & 0xFF;

                // 计算灰度值
                int gray = (int)(r * 0.299 + g * 0.587 + b * 0.114);

                // 二值化
                if (gray > threshold)
                    result[i] = white;  // 背景
                else
                    result[i] = black;  // 文字
            }

            return FromArgbArray(result, image.Width, image.Height);
        }

        /// <summary>
        /// 将位图像素按行优先顺序读取为 ARGB int 数组
        /// （等价于逐像素 GetPixel，字节序 AARRGGBB）
        /// </summary>
        private static int[] ToArgbArray(Bitmap bmp)
        {
            int width = bmp.Width;
            int height = bmp.Height;
            int[] pixels = new int[width * height];

            if (bmp.PixelFormat == PixelFormat.Format32bppArgb)
            {
                LockBitsIntoArray(bmp, pixels, width, height);
            }
            else
            {
                // 先统一为 32bppArgb 格式，保证每像素 4 字节（B,G,R,A）
                using (var argb = bmp.Clone(new Rectangle(0, 0, width, height), PixelFormat.Format32bppArgb))
                {
                    LockBitsIntoArray(argb, pixels, width, height);
                }
            }

            return pixels;
        }

        /// <summary>
        /// 将 ARGB int 数组按行优先顺序写回新创建的 32bppArgb 位图
        /// </summary>
        private static Bitmap FromArgbArray(int[] pixels, int width, int height)
        {
            var result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            var data = result.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = data.Stride;
                int offset = 0;
                for (int y = 0; y < height; y++)
                {
                    Marshal.Copy(pixels, offset, data.Scan0 + y * stride, width);
                    offset += width;
                }
            }
            finally
            {
                result.UnlockBits(data);
            }

            return result;
        }

        private static void LockBitsIntoArray(Bitmap bmp, int[] pixels, int width, int height)
        {
            var data = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = data.Stride;
                int offset = 0;
                for (int y = 0; y < height; y++)
                {
                    Marshal.Copy(data.Scan0 + y * stride, pixels, offset, width);
                    offset += width;
                }
            }
            finally
            {
                bmp.UnlockBits(data);
            }
        }
    }
}