using System.Diagnostics;

namespace 脚本.Flows
{
    /// <summary>
    /// 战斗子流程：勾选英雄逐个部署 → 等待部署就绪 →
    /// 轮询"资源归零 / 战斗胜利 100 / 90 秒超时"三种结束条件 → 点撤退/返回。
    /// 结束条件优先级：资源归零（提前结束）＞ 胜利 ＞ 超时兜底。
    /// </summary>
    public static class BattleFlow
    {
        /// <summary>战斗轮询兜底超时（资源一直没抢完的强敌，防止无限战斗）</summary>
        private const int MaxBattleMs = 90 * 1000;

        public static bool Run(GameFlowContext ctx, Func<int, bool> heroChecked, int heroSlotCount)
        {
            DeployHeroes(ctx, heroChecked, heroSlotCount);

            // 全部英雄下完后等待，再开始归零/胜利检测（等英雄部署动画就绪）
            if (!ctx.Sleep(5000, 5000)) return false;

            return WaitBattleEnd(ctx);
        }

        /// <summary>按 UI 勾选顺序逐个下英雄：点英雄槽位 → 点出战按钮</summary>
        private static void DeployHeroes(GameFlowContext ctx, Func<int, bool> heroChecked, int heroSlotCount)
        {
            for (int i = 0; i < Math.Min(heroSlotCount, Locationinformation.HeroPosition.Length); i++)
            {
                if (ctx.ShouldStop) return;
                if (!heroChecked(i)) continue;
                ctx.Tap(Locationinformation.HeroPosition[i], 10, 10);
                ctx.Sleep(200, 300);
                ctx.Tap(Locationinformation.Hero, 100, 40);
            }
        }

        /// <summary>战斗结束条件轮询：每轮一次截图，归零检测与胜利检测共用同一帧</summary>
        private static bool WaitBattleEnd(GameFlowContext ctx)
        {
            var startTime = DateTime.Now;
            while (ctx.ShouldStop == false)
            {
                if ((DateTime.Now - startTime).TotalMilliseconds >= MaxBattleMs)
                {
                    Retreat(ctx, "战斗超时（90秒）资源未抢完，已撤退");
                    return true;   // 超时属于正常结束路径，不算流程失败
                }
                if (!ctx.Sleep(1000, 2000)) return false;

                // 一次截图复用：归零检测与胜利检测共用同一帧（省一次全屏截图）
                using (var screen = ctx.Capture())
                {
                    if (IsBattleResourceZero(ctx, screen))
                    {
                        Retreat(ctx, "资源已抢空，提前撤退");
                        return true;
                    }
                    int victory = ctx.Recognizer.GetNumber(
                        screen,
                        Locationinformation.VictoryArea.x,
                        Locationinformation.VictoryArea.y,
                        Locationinformation.VictoryArea.width,
                        Locationinformation.VictoryArea.height,
                        isRemoveNoise: false);
                    if (victory == 100)
                    {
                        Debug.WriteLine("战斗完成 100，已撤退");
                        break;
                    }
                    Debug.WriteLine($"victoryStatus:{victory}");
                }
            }
            if (ctx.ShouldStop) return false;

            Debug.WriteLine("返回基地");
            ctx.Tap(Locationinformation.Return, 10, 10);
            return ctx.Sleep(1200, 1800);
        }

        private static void Retreat(GameFlowContext ctx, string reason)
        {
            ctx.Tap(Locationinformation.Retreat, 5, 5);
            ctx.Sleep(1000, 2000);
            Debug.WriteLine(reason);
        }

        /// <summary>
        /// 资源归零判据（8 张运行取证图实测校准）：
        /// ① 标题模板只在屏幕左上角区域搜索（面板固定位置），位置约束下阈值可安全降到 0.6；
        /// ② 双窄带 + 数字字符尺寸窗口计数：1~3 个判归零；为 0 说明面板不在画面，绝不判归零。
        /// </summary>
        internal static bool IsBattleResourceZero(GameFlowContext ctx, Bitmap screen)
        {
            const int maxDigitChars = 3;
            const double titleThreshold = 0.6;
            var searchRoi = new OpenCvSharp.Rect(0, 0, 420, 260);
            const int roiDx = 10, roiDy = 98, roiW = 300, roiH = 160;
            int[] bandCentersY = { 30, 120 };

            using (var mat = OpenCvSharp.Extensions.BitmapConverter.ToMat(screen))
            using (var gray = new OpenCvSharp.Mat())
            {
                OpenCvSharp.Cv2.CvtColor(mat, gray, OpenCvSharp.ColorConversionCodes.BGR2GRAY);
                var title = ctx.ResTitleMatcher.FindBestMatchInRoi(gray, searchRoi, ctx.ResTitleMatcher.MinScale, ctx.ResTitleMatcher.MaxScale);
                if (title.Score < titleThreshold)
                {
                    Debug.WriteLine($"资源面板标题定位失败(分数{title.Score:F2})");
                    SaveTitleFailEvidence(screen);
                    return false;
                }

                var digitRoi = new System.Drawing.Rectangle(title.Location.X + roiDx, title.Location.Y + roiDy, roiW, roiH);
                int digitChars = ImageProcessing.CountDigitCharsInBands(screen, digitRoi, bandCentersY);
                Debug.WriteLine($"资源归零检测: 数字字符块={digitChars} (需 1~{maxDigitChars})");
                // 真归零必有 2 个"0"字符（允许干扰吞掉 1 个，下限取 1）；
                // 字符块为 0 说明面板实际不在画面（假定位/面板出屏），绝不能判归零
                return digitChars >= 1 && digitChars <= maxDigitChars;
            }
        }

        private static DateTime _lastTitleFailSave = DateTime.MinValue;

        /// <summary>标题定位失败时保存当前画面（10 秒节流），供分析失败原因</summary>
        private static void SaveTitleFailEvidence(Bitmap screen)
        {
            if ((DateTime.Now - _lastTitleFailSave).TotalSeconds < 10) return;
            _lastTitleFailSave = DateTime.Now;
            try
            {
                string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Screenshots");
                Directory.CreateDirectory(folder);
                screen.Save(Path.Combine(folder, $"titlefail_{DateTime.Now:HHmmss}.png"));
                Debug.WriteLine("已保存标题定位失败取证图");
            }
            catch { /* 取证失败不影响主流程 */ }
        }
    }
}
