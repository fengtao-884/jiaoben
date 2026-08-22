using System.Diagnostics;

namespace 脚本.Flows
{
    /// <summary>
    /// 各流程共享的步骤工厂：把"拖拽基地 → 定位卫星站 → 点寻找敌人 → 过滤大卡拉米"等
    /// 在多个流程中重复出现的段落收敛为可复用步骤。
    /// </summary>
    public static class CommonSteps
    {
        /// <summary>拖拽基地 UI（实测 F5 可省，但需拖拽后卫星站才在画面中）</summary>
        public static GameStep DragBaseUi(int sleepMin = 250, int sleepMax = 450) => new(
            "拖拽基地UI",
            ctx =>
            {
                ctx.Timed("拖拽", () => ctx.Drag(
                    (Locationinformation.BaseUIDrag.startX, Locationinformation.BaseUIDrag.startY),
                    (Locationinformation.BaseUIDrag.endX, Locationinformation.BaseUIDrag.endY)));
                Debug.WriteLine("拖拽基地UI");
                return ctx.Sleep(sleepMin, sleepMax);
            });

        /// <summary>模板识别卫星站并点击（含位置记忆快路径 + F5+拖拽兜底），失败返回 false</summary>
        public static GameStep FindAndTapSatellite() => new(
            "识别卫星站",
            ctx => FindAndTapSatelliteCore(ctx),
            maxRetries: 1);

        /// <summary>点击"寻找敌人"按钮并等待敌人加载</summary>
        public static GameStep FindEnemy(int loadMin, int loadMax) => new(
            "寻找敌人",
            ctx =>
            {
                ctx.Tap(Locationinformation.FindEnemy, 15, 15);
                Debug.WriteLine("点击寻找敌人按钮");
                return ctx.Sleep(loadMin, loadMax);
            });

        /// <summary>检测敌人名称是否为"大卡拉米"，是则重新找敌（卫星站→寻找敌人）；不是则直接成功</summary>
        public static GameStep SkipBigEnemyIfPresent() => new(
            "过滤大卡拉米",
            ctx =>
            {
                string name = ctx.Recognizer.GetText(
                    Locationinformation.Name.startX,
                    Locationinformation.Name.startY,
                    Locationinformation.Name.w,
                    Locationinformation.Name.h);
                if (name != "大卡拉米") return true;

                Debug.WriteLine("检测到大卡拉米，重新寻找敌人...");
                return FindAndTapSatelliteCore(ctx)
                    && ctx.RunTapFindEnemy(1200, 1500);
            },
            maxRetries: 1);

        /// <summary>卫星站识别+点击核心逻辑（供步骤与过滤大卡拉米复用；含快路径与 F5 兜底）</summary>
        internal static bool FindAndTapSatelliteCore(GameFlowContext ctx)
        {
            // 快路径：记忆位置有效且未到校验轮 → 直接点击（省截图与匹配）
            if (ctx.SatellitePos is { } pos && ctx.SatelliteCheckCountdown > 0)
            {
                var swTap = System.Diagnostics.Stopwatch.StartNew();
                ctx.SatelliteCheckCountdown--;
                ctx.Capturer.Tap(pos.x, pos.y);
                Debug.WriteLine($"[耗时] 快路径点击: {swTap.ElapsedMilliseconds}ms");
                return true;
            }

            // 识别路径：连续尝试数次（天线旋转/画面抖动单帧可能失手）
            if (TryFindAndTap(ctx))
            {
                ctx.SatelliteCheckCountdown = SatelliteVerifyInterval;
                return true;
            }

            // 兜底：画面可能不在基地界面，执行 F5 + 拖拽后再试一轮
            Debug.WriteLine("卫星站直接识别失败，执行 F5+拖拽兜底");
            ctx.Capturer.MoveMouseTo(950, 500);
            ctx.Capturer.SendF5ToLdPlayer();
            ctx.Sleep(1000, 1500);
            ctx.Drag(
                (Locationinformation.BaseUIDrag.startX, Locationinformation.BaseUIDrag.startY),
                (Locationinformation.BaseUIDrag.endX, Locationinformation.BaseUIDrag.endY));
            ctx.Sleep(250, 450);
            if (TryFindAndTap(ctx))
            {
                ctx.SatelliteCheckCountdown = SatelliteVerifyInterval;
                return true;
            }
            return false;
        }

        private const int SatelliteVerifyInterval = 5;

        /// <summary>截图 → 模板匹配 → 命中即点击并记录记忆位置</summary>
        private static bool TryFindAndTap(GameFlowContext ctx)
        {
            const int maxAttempts = 3;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                var swShot = System.Diagnostics.Stopwatch.StartNew();
                using var screen = ctx.Capture();
                Debug.WriteLine($"[耗时] 截图: {swShot.ElapsedMilliseconds}ms");

                swShot.Restart();
                using var mat = OpenCvSharp.Extensions.BitmapConverter.ToMat(screen);
                using var gray = new OpenCvSharp.Mat();
                OpenCvSharp.Cv2.CvtColor(mat, gray, OpenCvSharp.ColorConversionCodes.BGR2GRAY);

                TemplateMatchResult result;
                if (ctx.SatellitePos is { } lastPos)
                {
                    // 优先在记忆位置附近 ROI 匹配（快）；ROI 分数不足再全图匹配
                    const int roiSize = 320;
                    int rx = Math.Max(0, lastPos.x - roiSize / 2);
                    int ry = Math.Max(0, lastPos.y - roiSize / 2);
                    int rw = Math.Min(roiSize, gray.Width - rx);
                    int rh = Math.Min(roiSize, gray.Height - ry);
                    result = ctx.SatelliteMatcher.FindBestMatchInRoi(gray, new OpenCvSharp.Rect(rx, ry, rw, rh), 0.9, 1.3);
                    if (result.Score < ctx.SatelliteMatcher.Threshold)
                        result = ctx.SatelliteMatcher.FindBestMatch(gray);
                }
                else
                {
                    result = ctx.SatelliteMatcher.FindBestMatch(gray);
                }
                Debug.WriteLine($"[耗时] 匹配: {swShot.ElapsedMilliseconds}ms");

                if (result.Score >= ctx.SatelliteMatcher.Threshold)
                {
                    swShot.Restart();
                    ctx.Capturer.Tap(result.Center.X, result.Center.Y);
                    Debug.WriteLine($"[耗时] 点击: {swShot.ElapsedMilliseconds}ms");
                    ctx.SatellitePos = (result.Center.X, result.Center.Y);
                    Debug.WriteLine($"卫星站识别成功: 分数{result.Score:F2} 尺度{result.Scale:F2} 位置({result.Center.X},{result.Center.Y})");
                    return true;
                }
                Debug.WriteLine($"卫星站识别失败(分数{result.Score:F2})，重试 {attempt}/{maxAttempts}");
                ctx.Sleep(400, 700);
            }
            return false;
        }

        /// <summary>辅助扩展：点寻找敌人并等待（供内部复用）</summary>
        internal static bool RunTapFindEnemy(this GameFlowContext ctx, int minMs, int maxMs)
        {
            ctx.Tap(Locationinformation.FindEnemy, 15, 15);
            Debug.WriteLine("点击寻找敌人按钮");
            return ctx.Sleep(minMs, maxMs);
        }

        /// <summary>内部复用入口：卫星站识别+点击核心逻辑（供大卡拉米重找循环调用）</summary>
        internal static bool TestFindAndTapSatellite(GameFlowContext ctx) => FindAndTapSatelliteCore(ctx);
    }
}
