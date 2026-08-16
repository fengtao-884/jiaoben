using OpenCvSharp;
using OpenCvSharp.Extensions;
using Point = OpenCvSharp.Point;
using Rect = OpenCvSharp.Rect;
using Size = OpenCvSharp.Size;

namespace 脚本
{
    /// <summary>
    /// 模板匹配器：多尺度 CCoeffNormed 模板匹配，返回最佳匹配位置与分数。
    /// 已通过实测验证：干净目标场景匹配分数可达 0.95+，受遮挡/干扰时明显降低。
    /// </summary>
    public class TemplateMatcher : IDisposable
    {
        private readonly Mat _template;   // 灰度模板
        private bool _disposed;

        /// <summary>判定"找到"的分数阈值（0~1，越高越严格）。默认 0.8。</summary>
        public double Threshold { get; set; } = 0.8;

        /// <summary>多尺度扫描范围（卫星站实测命中约 0.8 附近，收窄范围以提速）</summary>
        public double MinScale { get; set; } = 0.5;
        public double MaxScale { get; set; } = 1.2;
        public double ScaleStep { get; set; } = 0.05;

        public TemplateMatcher(string templatePath)
        {
            var img = Cv2.ImRead(templatePath, ImreadModes.Grayscale);
            if (img.Empty())
                throw new FileNotFoundException($"模板文件不存在或无法读取: {templatePath}");
            _template = img;
        }

        public TemplateMatcher(Bitmap template)
        {
            using var mat = BitmapConverter.ToMat(template);
            _template = new Mat();
            Cv2.CvtColor(mat, _template, ColorConversionCodes.BGR2GRAY);
        }

        /// <summary>
        /// 在屏幕截图中多尺度搜索模板，返回最佳匹配（分数最高者）。
        /// </summary>
        public TemplateMatchResult FindBestMatch(Bitmap screen)
        {
            using var mat = BitmapConverter.ToMat(screen);
            using var scene = new Mat();
            Cv2.CvtColor(mat, scene, ColorConversionCodes.BGR2GRAY);
            return FindBestMatch(scene);
        }

        /// <summary>
        /// 在灰度场景图中多尺度搜索模板，返回最佳匹配（分数最高者）。
        /// </summary>
        public TemplateMatchResult FindBestMatch(Mat scene)
        {
            double best = -1;
            Point bestLoc = default;
            double bestScale = 1;

            for (double scale = MinScale; scale <= MaxScale + 1e-9; scale += ScaleStep)
            {
                int tw = (int)Math.Round(_template.Width * scale);
                int th = (int)Math.Round(_template.Height * scale);
                if (tw <= 0 || th <= 0 || tw >= scene.Width || th >= scene.Height)
                    continue;

                using var resized = _template.Resize(new Size(tw, th));
                using var result = new Mat();
                Cv2.MatchTemplate(scene, resized, result, TemplateMatchModes.CCoeffNormed);
                Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out Point maxLoc);
                if (maxVal > best)
                {
                    best = maxVal;
                    bestLoc = maxLoc;
                    bestScale = scale;
                }
            }
            return new TemplateMatchResult(best, bestLoc, bestScale, _template.Width, _template.Height);
        }

        /// <summary>
        /// 在指定 ROI 区域内多尺度搜索模板（利用已知位置附近搜索，大幅提速）。
        /// 返回坐标为场景图坐标系。
        /// </summary>
        public TemplateMatchResult FindBestMatchInRoi(Mat scene, Rect roi, double minScale, double maxScale)
        {
            using var roiScene = new Mat(scene, roi);
            double best = -1;
            Point bestLoc = default;
            double bestScale = 1;

            for (double scale = minScale; scale <= maxScale + 1e-9; scale += ScaleStep)
            {
                int tw = (int)Math.Round(_template.Width * scale);
                int th = (int)Math.Round(_template.Height * scale);
                if (tw <= 0 || th <= 0 || tw >= roiScene.Width || th >= roiScene.Height)
                    continue;

                using var resized = _template.Resize(new Size(tw, th));
                using var result = new Mat();
                Cv2.MatchTemplate(roiScene, resized, result, TemplateMatchModes.CCoeffNormed);
                Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out Point maxLoc);
                if (maxVal > best)
                {
                    best = maxVal;
                    bestLoc = new Point(maxLoc.X + roi.X, maxLoc.Y + roi.Y);
                    bestScale = scale;
                }
            }
            return new TemplateMatchResult(best, bestLoc, bestScale, _template.Width, _template.Height);
        }

        /// <summary>
        /// 多实例匹配：找出场景图中所有分数 &gt;= scoreThreshold 的模板实例（阈值过滤 + NMS 去重）。
        /// 用于"计数"类需求（如识别基地中目标建筑/单位的数量）。
        /// 返回实例列表（坐标为场景图坐标系）。
        /// </summary>
        public List<TemplateMatchResult> FindAllMatches(Mat scene, double scoreThreshold, double minScale, double maxScale)
        {
            // 1. 多尺度扫描，记录最佳尺度（目标尺寸变化不大时范围可收窄以提速）
            double best = -1;
            double bestScale = 1;
            for (double scale = minScale; scale <= maxScale + 1e-9; scale += ScaleStep)
            {
                int tw = (int)Math.Round(_template.Width * scale);
                int th = (int)Math.Round(_template.Height * scale);
                if (tw <= 0 || th <= 0 || tw >= scene.Width || th >= scene.Height) continue;
                using var resized = _template.Resize(new Size(tw, th));
                using var result = new Mat();
                Cv2.MatchTemplate(scene, resized, result, TemplateMatchModes.CCoeffNormed);
                Cv2.MinMaxLoc(result, out _, out double maxVal, out _, out _);
                if (maxVal > best) { best = maxVal; bestScale = scale; }
            }

            // 2. 最佳尺度下收集所有高分点
            int btw = (int)Math.Round(_template.Width * bestScale);
            int bth = (int)Math.Round(_template.Height * bestScale);
            using var bestResized = _template.Resize(new Size(btw, bth));
            using var bestResult = new Mat();
            Cv2.MatchTemplate(scene, bestResized, bestResult, TemplateMatchModes.CCoeffNormed);

            var candidates = new List<(Point p, float score)>();
            for (int y = 0; y < bestResult.Rows; y++)
                for (int x = 0; x < bestResult.Cols; x++)
                {
                    float v = bestResult.At<float>(y, x);
                    if (v >= scoreThreshold)
                        candidates.Add((new Point(x, y), v));
                }

            // 3. NMS：按分数降序，抑制重叠实例
            var kept = new List<(Point p, float score)>();
            foreach (var c in candidates.OrderByDescending(c => c.score))
            {
                if (kept.All(k => Math.Abs(k.p.X - c.p.X) > btw * 0.5 || Math.Abs(k.p.Y - c.p.Y) > bth * 0.5))
                    kept.Add(c);
            }

            return kept.Select(k => new TemplateMatchResult(k.score, k.p, bestScale, _template.Width, _template.Height)).ToList();
        }

        /// <summary>屏幕中是否存在可靠匹配（分数 >= Threshold）</summary>
        public bool IsFound(Bitmap screen) => FindBestMatch(screen).Score >= Threshold;

        public void Dispose()
        {
            if (!_disposed)
            {
                _template.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// 模板匹配结果
    /// </summary>
    public readonly struct TemplateMatchResult
    {
        /// <summary>匹配分数（CCoeffNormed，0~1，越高越可靠）</summary>
        public double Score { get; }

        /// <summary>匹配框左上角坐标（场景图坐标系）</summary>
        public Point Location { get; }

        /// <summary>命中的缩放比例</summary>
        public double Scale { get; }

        /// <summary>模板原始宽高</summary>
        public int TemplateWidth { get; }
        public int TemplateHeight { get; }

        /// <summary>按当前尺度计算的匹配框</summary>
        public Rect Bounds => new(Location.X, Location.Y,
            (int)Math.Round(TemplateWidth * Scale), (int)Math.Round(TemplateHeight * Scale));

        /// <summary>匹配框中心点（用于点击等操作）</summary>
        public Point Center => new(Location.X + Bounds.Width / 2, Location.Y + Bounds.Height / 2);

        public TemplateMatchResult(double score, Point location, double scale, int templateWidth, int templateHeight)
        {
            Score = score;
            Location = location;
            Scale = scale;
            TemplateWidth = templateWidth;
            TemplateHeight = templateHeight;
        }
    }
}
