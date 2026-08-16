using System.Diagnostics;
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

                    // 4. 动态生成文件名并保存到指定文件夹
                    //string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    //string folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Screenshots");
                    //if (!Directory.Exists(folderPath))
                    //{
                    //    Directory.CreateDirectory(folderPath);
                    //}
                    //string filePath = Path.Combine(folderPath, $"processed_{timestamp}.png");
                    //processed.Save(filePath);

                    // 5. 使用Tesseract识别数字
                    string text = RecognizeText(processed);
                    text = text.Replace("%", "");
                    // 6. 提取数字
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
            var processed = new Bitmap(image.Width, image.Height);

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    Color pixel = image.GetPixel(x, y);

                    // 计算灰度值
                    int gray = (int)(pixel.R * 0.3 + pixel.G * 0.59 + pixel.B * 0.11);

                    // 二值化（简单阈值）
                    int threshold = 128;
                    if (gray > threshold)
                        processed.SetPixel(x, y, Color.White);  // 背景
                    else
                        processed.SetPixel(x, y, Color.Black);  // 数字
                }
            }

            return processed;
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

            // 创建结果图像，初始化为黑色背景（数字颜色）
            var result = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.Clear(Color.Black); // 黑色作为数字颜色
            }

            // 访问标记数组
            bool[,] visited = new bool[width, height];

            // 8方向连通性检查（更全面的连通性）
            int[] dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
            int[] dy = { -1, -1, -1, 0, 0, 1, 1, 1 };

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // 如果是白色像素（背景或数字）且未访问过
                    if (binary.GetPixel(x, y).R == 255 && !visited[x, y])
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
                                    if (binary.GetPixel(nx, ny).R == 255 && !visited[nx, ny])
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
                                result.SetPixel(pixel.X, pixel.Y, Color.White);
                            }
                        }
                        // 否则，这是一个大面积白色背景区域，不复制到结果中（保持黑色）
                    }
                }
            }

            return result;
        }
        private Bitmap RemoveSmallWhiteNoise(Bitmap image, int minNoiseArea)
        {
            int width = image.Width;
            int height = image.Height;

            // 创建结果图像，初始化为黑色背景
            var result = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(result))
            {
                g.Clear(Color.Black);
            }

            // 访问标记数组
            bool[,] visited = new bool[width, height];

            // 8方向连通性检查
            int[] dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
            int[] dy = { -1, -1, -1, 0, 0, 1, 1, 1 };

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // 如果是白色像素（数字或噪点）且未访问过
                    if (image.GetPixel(x, y).R == 255 && !visited[x, y])
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
                                    if (image.GetPixel(nx, ny).R == 255 && !visited[nx, ny])
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
                                result.SetPixel(pixel.X, pixel.Y, Color.White);
                            }
                        }
                        // 否则，这是一个小白色噪点，不复制到结果中（保持黑色）
                    }
                }
            }

            return result;
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
        }
        public void CaptureRegion((int x, int y) position, int width, int height)
        {
            try
            {
                // 1. 截取整个屏幕
                using (var screen = _capturer.CaptureToBitmap())
                {
                    // 2. 截取指定区域
                    var region = new Rectangle(position.x, position.y, width, height);
                    var cropped = CropImage(screen, region);

                    cropped.Save("333.png");
                }
            }
            catch (Exception ex)
            {
                
            }
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

                    // 4. 保存处理后的图像用于调试
                    //string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                    //string folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TextScreenshots");
                    //if (!Directory.Exists(folderPath))
                    //{
                    //    Directory.CreateDirectory(folderPath);
                    //}
                    //string filePath = Path.Combine(folderPath, $"text_{timestamp}.png");
                    //binary.Save(filePath);

                    // 5. 使用Tesseract识别文字
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
            var result = new Bitmap(image.Width, image.Height);

            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    Color pixel = image.GetPixel(x, y);

                    // 计算灰度值
                    int gray = (int)(pixel.R * 0.299 + pixel.G * 0.587 + pixel.B * 0.114);

                    // 二值化
                    if (gray > threshold)
                        result.SetPixel(x, y, Color.White);  // 背景
                    else
                        result.SetPixel(x, y, Color.Black);  // 文字

                }
            }

            return result;
        }
        //private Bitmap BinaryImage(Bitmap image, int threshold)
        //{
        //    var result = new Bitmap(image.Width, image.Height);

        //    // 白色检测阈值（可调整）
        //    int whiteMinValue = 230;  // 最小白色值，可调

        //    for (int y = 0; y < image.Height; y++)
        //    {
        //        for (int x = 0; x < image.Width; x++)
        //        {
        //            Color pixel = image.GetPixel(x, y);

        //            // 直接检测白色像素
        //            bool isWhite = pixel.R >= whiteMinValue &&
        //                          pixel.G >= whiteMinValue &&
        //                          pixel.B >= whiteMinValue;

        //            if (isWhite)
        //            {
        //                result.SetPixel(x, y, Color.Black);  // 白色数字设为黑色
        //            }
        //            else
        //            {
        //                result.SetPixel(x, y, Color.White);  // 背景设为白色
        //            }
        //        }
        //    }

        //    return result;
        //}
    }
}