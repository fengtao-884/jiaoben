// D:\Code\脚本\ImageProcessing.cs
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace 脚本;

/// <summary>
/// 图像预处理纯函数集合（无状态，供 GetNumberRecognizer 复用，便于单测）。
/// </summary>
internal static class ImageProcessing
{
    /// <summary>BT.601 亮度（截断）。</summary>
    public static int Gray(int r, int g, int b) => (int)(r * 0.299 + g * 0.587 + b * 0.114);

    /// <summary>二值化：灰度 &lt;= threshold 置黑，否则置白。</summary>
    public static Bitmap Binarize(Bitmap image, int threshold)
    {
        int[] src = ToArgbArray(image);
        int[] result = new int[src.Length];
        int white = Color.White.ToArgb();
        int black = Color.Black.ToArgb();
        for (int i = 0; i < src.Length; i++)
        {
            int p = src[i];
            int g = Gray((p >> 16) & 0xFF, (p >> 8) & 0xFF, p & 0xFF);
            result[i] = g <= threshold ? black : white;
        }
        return FromArgbArray(result, image.Width, image.Height);
    }

    /// <summary>
    /// 白色连通域过滤：对每个白色连通域按面积调用 keep，保留的置白，其余置黑。
    /// RemoveLargeWhiteAreas = keep(count => count &lt;= max)；RemoveSmallWhiteNoise = keep(count => count &gt;= min)。
    /// </summary>
    public static Bitmap FilterWhiteComponents(Bitmap binary, Func<int, bool> keep)
    {
        int width = binary.Width, height = binary.Height;
        int[] src = ToArgbArray(binary);
        int[] result = new int[width * height];
        Array.Fill(result, Color.Black.ToArgb());
        bool[,] visited = new bool[width, height];
        int[] dx = { -1, 0, 1, -1, 1, -1, 0, 1 };
        int[] dy = { -1, -1, -1, 0, 0, 1, 1, 1 };
        int white = Color.White.ToArgb();

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                if (((src[y * width + x] >> 16) & 0xFF) == 255 && !visited[x, y])
                {
                    var comp = new List<Point>();
                    var stack = new Stack<Point>();
                    stack.Push(new Point(x, y));
                    visited[x, y] = true;
                    while (stack.Count > 0)
                    {
                        Point p = stack.Pop();
                        comp.Add(p);
                        for (int i = 0; i < 8; i++)
                        {
                            int nx = p.X + dx[i], ny = p.Y + dy[i];
                            if (nx >= 0 && nx < width && ny >= 0 && ny < height &&
                                ((src[ny * width + nx] >> 16) & 0xFF) == 255 && !visited[nx, ny])
                            {
                                visited[nx, ny] = true;
                                stack.Push(new Point(nx, ny));
                            }
                        }
                    }
                    if (keep(comp.Count))
                        foreach (var px in comp)
                            result[px.Y * width + px.X] = white;
                }
            }

        return FromArgbArray(result, width, height);
    }

    /// <summary>裁剪指定区域（越界自动收边；用 Clone 保像素，避免 DrawImage 插值）。</summary>
    public static Bitmap Crop(Bitmap source, Rectangle region)
    {
        // 负起点：左/上越界时收进边界，宽度/高度相应减小（越界自动收边）
        if (region.X < 0)
        {
            region.Width += region.X;
            region.X = 0;
        }
        if (region.Y < 0)
        {
            region.Height += region.Y;
            region.Y = 0;
        }
        if (region.Right > source.Width) region.Width = source.Width - region.X;
        if (region.Bottom > source.Height) region.Height = source.Height - region.Y;
        if (region.Width <= 0 || region.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(region), "裁剪区域为空");
        return source.Clone(region, PixelFormat.Format32bppArgb);
    }

    /// <summary>
    /// 统计指定区域内白色像素（RGB均&gt;190）的 4 连通域数量（面积 &gt;= minArea 的才算）。
    /// 用于"资源归零"判据：数字行区域归零时只有单个"0"字符（连通域 1-2 个），
    /// 有值时为多位数字（连通域 7+ 个）。实测（取证图）：归零 3 个、有值 16-19 个。
    /// </summary>
    public static int CountWhiteComponents(Bitmap source, Rectangle region, int minArea = 4)
    {
        // 区域边界裁剪（越界自动收边）
        if (region.X < 0) { region.Width += region.X; region.X = 0; }
        if (region.Y < 0) { region.Height += region.Y; region.Y = 0; }
        if (region.Right > source.Width) region.Width = source.Width - region.X;
        if (region.Bottom > source.Height) region.Height = source.Height - region.Y;
        if (region.Width <= 0 || region.Height <= 0) return 0;

        int w = region.Width, h = region.Height;
        var set = new HashSet<int>();
        var data = source.LockBits(region, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            int stride = data.Stride;
            byte[] row = new byte[w * 4];
            for (int y = 0; y < h; y++)
            {
                Marshal.Copy(data.Scan0 + y * stride, row, 0, row.Length);
                for (int x = 0; x < w; x++)
                {
                    int b = row[x * 4], g = row[x * 4 + 1], r = row[x * 4 + 2];
                    if (r > 190 && g > 190 && b > 190)
                        set.Add(y * w + x);
                }
            }
        }
        finally { source.UnlockBits(data); }

        // 4 连通 BFS 计数
        var visited = new HashSet<int>();
        int count = 0;
        foreach (int seed in set)
        {
            if (visited.Contains(seed)) continue;
            var stack = new Stack<int>();
            stack.Push(seed);
            visited.Add(seed);
            int area = 0;
            while (stack.Count > 0)
            {
                int p = stack.Pop();
                area++;
                int x = p % w, y = p / w;
                if (x > 0 && set.Contains(p - 1) && !visited.Contains(p - 1)) { visited.Add(p - 1); stack.Push(p - 1); }
                if (x < w - 1 && set.Contains(p + 1) && !visited.Contains(p + 1)) { visited.Add(p + 1); stack.Push(p + 1); }
                if (y > 0 && set.Contains(p - w) && !visited.Contains(p - w)) { visited.Add(p - w); stack.Push(p - w); }
                if (y < h - 1 && set.Contains(p + w) && !visited.Contains(p + w)) { visited.Add(p + w); stack.Push(p + w); }
            }
            if (area >= minArea) count++;
        }
        return count;
    }

    public static int[] ToArgbArray(Bitmap bmp)
    {
        int width = bmp.Width, height = bmp.Height;
        int[] pixels = new int[width * height];
        if (bmp.PixelFormat == PixelFormat.Format32bppArgb)
            LockBitsIntoArray(bmp, pixels, width, height);
        else
            using (var argb = bmp.Clone(new Rectangle(0, 0, width, height), PixelFormat.Format32bppArgb))
                LockBitsIntoArray(argb, pixels, width, height);
        return pixels;
    }

    public static Bitmap FromArgbArray(int[] pixels, int width, int height)
    {
        var result = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        var data = result.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            int stride = data.Stride, offset = 0;
            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(pixels, offset, data.Scan0 + y * stride, width);
                offset += width;
            }
        }
        finally { result.UnlockBits(data); }
        return result;
    }

    private static void LockBitsIntoArray(Bitmap bmp, int[] pixels, int width, int height)
    {
        var data = bmp.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            int stride = data.Stride, offset = 0;
            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(data.Scan0 + y * stride, pixels, offset, width);
                offset += width;
            }
        }
        finally { bmp.UnlockBits(data); }
    }
}
