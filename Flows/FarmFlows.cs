using System.Diagnostics;

namespace 脚本.Flows
{
    /// <summary>
    /// 刷资源流程（按等级筛选敌人）：
    /// 拖拽基地 → 识别卫星站并点击 → 寻找敌人 → 过滤大卡拉米 →
    /// 循环{ OCR 等级 → 达标开打 / 不达标找下一个 } → 战斗。
    /// </summary>
    public static class FarmByLevelFlow
    {
        public static bool Run(GameFlowContext ctx, int runCount, int maxLevel)
        {
            for (int round = 1; round <= runCount; round++)
            {
                if (ctx.ShouldStop) return false;
                Debug.WriteLine($"===== 刷资源 第 {round}/{runCount} 轮 =====");

                // 找到一个等级达标的敌人（含前置拖拽/卫星站/过滤大卡拉米）
                var found = FindQualifiedEnemyByLevel(ctx, maxLevel);
                if (!found) continue;

                // 开打：下英雄 + 战斗轮询 + 返回基地
                BattleFlow.Run(ctx, ctx.HeroChecked, Locationinformation.HeroPosition.Length);
            }
            return true;
        }

        /// <summary>循环翻页直到敌人等级达标；找不到（卫星站失败等）返回 false 跳过本轮</summary>
        private static bool FindQualifiedEnemyByLevel(GameFlowContext ctx, int maxLevel)
        {
            // 前置：拖拽 → 卫星站 → 寻找敌人 → 过滤大卡拉米
            var prep = new[]
            {
                CommonSteps.DragBaseUi(250, 450),
                CommonSteps.FindAndTapSatellite(),
                CommonSteps.FindEnemy(800, 1200),
                CommonSteps.SkipBigEnemyIfPresent(),
            };
            if (!GameFlowRunner.Run(ctx, prep)) return false;

            while (!ctx.ShouldStop)
            {
                int level = ctx.Recognizer.GetNumber(
                    Locationinformation.LevelArea.x,
                    Locationinformation.LevelArea.y,
                    Locationinformation.LevelArea.width,
                    Locationinformation.LevelArea.height);
                Debug.WriteLine($"识别敌人等级: {level} (上限 {maxLevel})");

                if (level > 0 && level < maxLevel)
                {
                    Debug.WriteLine($"找到合适敌人！等级: {level}");
                    return true;
                }

                Debug.WriteLine("敌人等级过高，寻找下一个...");
                ctx.Tap(Locationinformation.NextEnemy, 5, 5);
                if (!ctx.Sleep(1500, 2200)) return false;
            }
            return false;
        }
    }

    /// <summary>
    /// 打人机资源流程（资源区间 + 机枪数量双重判定）：
    /// 拖拽基地 → 卫星站 → 寻找敌人 → 大卡拉米重找循环 →
    /// 循环{ 截图复用帧：OCR 资源区间 → 机枪数量 → 达标开打 / 否则 Next } → 战斗。
    /// </summary>
    public static class FarmByLootFlow
    {
        private const int RequiredGunCount = 8;
        private const double GunMatchThreshold = 0.7;
        private const int LootUnit = 17000;   // 资源数值按该单位取整判定

        public static bool Run(GameFlowContext ctx, int runCount, int resMin, int resMax)
        {
            int succeed = 0;
            for (int round = 1; round <= runCount; round++)
            {
                if (ctx.ShouldStop) break;
                Debug.WriteLine($"===== 打资源 第 {round}/{runCount} 轮 =====");

                bool found = FindQualifiedEnemyByLoot(ctx, resMin, resMax);
                if (found)
                {
                    BattleFlow.Run(ctx, ctx.HeroChecked, Locationinformation.HeroPosition.Length);
                    succeed++;
                }
                string title = $"已打{succeed}次";
                ctx.ReadUi<object>(() => { FlowUiBridge.SetTitle?.Invoke(title); return null!; });
            }
            return true;
        }

        /// <summary>循环翻页直到"资源区间达标 且 机枪≥8"；返回是否找到</summary>
        private static bool FindQualifiedEnemyByLoot(GameFlowContext ctx, int resMin, int resMax)
        {
            // 前置：拖拽 → 卫星站 → 寻找敌人
            if (!GameFlowRunner.Run(ctx, new[]
            {
                CommonSteps.DragBaseUi(300, 500),
                CommonSteps.FindAndTapSatellite(),
                CommonSteps.FindEnemy(2000, 3000),
            })) return false;

            // 大卡拉米重找循环（名称仍为大卡拉米则重新定位卫星站再找）
            while (!ctx.ShouldStop &&
                   ctx.Recognizer.GetText(
                       Locationinformation.Name.startX,
                       Locationinformation.Name.startY,
                       Locationinformation.Name.w,
                       Locationinformation.Name.h) == "大卡拉米")
            {
                Debug.WriteLine("检测到大卡拉米，重新寻找敌人...");
                ctx.Timed("拖拽", () => ctx.Drag(
                    (Locationinformation.BaseUIDrag.startX, Locationinformation.BaseUIDrag.startY),
                    (Locationinformation.BaseUIDrag.endX, Locationinformation.BaseUIDrag.endY)));
                if (!ctx.Sleep(800, 1500)) return false;
                if (!CommonSteps.TestFindAndTapSatellite(ctx))
                {
                    Debug.WriteLine("卫星站识别失败，重新等待敌人加载");
                    return false;
                }
                ctx.Tap(Locationinformation.FindEnemy, 15, 15);
                Debug.WriteLine("点击寻找敌人按钮");
                if (!ctx.Sleep(1200, 1500)) return false;
            }

            while (!ctx.ShouldStop)
            {
                // 一次截图复用：资源 OCR 与机枪检测共用同一帧（省一次全屏截图）
                using var screen = ctx.Capture();
                var input = ctx.Recognizer.GetText(screen, 80, 220, 200, 40);
                string cleaned = input.Replace(",", "").Replace(".", "");
                if (!int.TryParse(cleaned, out int res))
                {
                    Debug.WriteLine($"没找到合适敌人！ 找下一个，资源：{res}");
                    if (!NextEnemy(ctx, 1500, 1800)) return false;
                    continue;
                }

                bool inRange = res % LootUnit == 0 && res / LootUnit > resMin && res / LootUnit < resMax;
                if (!inRange)
                {
                    Debug.WriteLine($"没找到合适敌人！资源：{res}");
                    if (!NextEnemy(ctx, 1200, 1600)) return false;
                    continue;
                }

                if (!IsGunCountEnough(ctx, screen))
                {
                    Debug.WriteLine($"资源达标但机枪不足，找下一个（资源{res}）");
                    if (!NextEnemy(ctx, 1200, 1600)) return false;
                    continue;
                }

                // 双重达标 → 拖出敌人开打
                ctx.Timed("拖出敌人", () => ctx.Drag(
                    (Locationinformation.EnemyUIDrag.startX, Locationinformation.EnemyUIDrag.startY),
                    (Locationinformation.EnemyUIDrag.endX, Locationinformation.EnemyUIDrag.endY),
                    jitterX: 100, endJitterWide: true));
                Debug.WriteLine($"找到合适敌人！等级,资源：{res}");
                return true;
            }
            return false;
        }

        /// <summary>点 Next 找下一个敌人并等待加载；被停止时返回 false</summary>
        private static bool NextEnemy(GameFlowContext ctx, int minMs, int maxMs)
        {
            ctx.Tap(Locationinformation.NextEnemy, 5, 5);
            return ctx.Sleep(minMs, maxMs);
        }

        /// <summary>机枪数量检测（多实例模板计数 ≥ 阈值）</summary>
        internal static bool IsGunCountEnough(GameFlowContext ctx, Bitmap screen)
        {
            using var mat = OpenCvSharp.Extensions.BitmapConverter.ToMat(screen);
            using var gray = new OpenCvSharp.Mat();
            OpenCvSharp.Cv2.CvtColor(mat, gray, OpenCvSharp.ColorConversionCodes.BGR2GRAY);
            var matches = ctx.GunMatcher.FindAllMatches(gray, GunMatchThreshold, 0.8, 1.2);
            Debug.WriteLine($"机枪检测: {matches.Count} 个 (需≥{RequiredGunCount})");
            return matches.Count >= RequiredGunCount;
        }
    }

    /// <summary>
    /// 波兰守卫流程：点开始防御 → 轮询 OCR"战斗胜利" → 返回。
    /// </summary>
    public static class GuardFlow
    {
        public static bool Run(GameFlowContext ctx, int runCount)
        {
            for (int i = 1; i <= runCount; i++)
            {
                if (ctx.ShouldStop) return false;
                Debug.WriteLine($"执行{i}");
                ctx.Tap(Locationinformation.开始防御, 2, 2);

                // 轮询等待战斗胜利文字出现（每轮 1~3 秒）
                bool won = false;
                while (!ctx.ShouldStop)
                {
                    ctx.Sleep(1000, 3000);
                    string str = ctx.Recognizer.GetText(
                        Locationinformation.战斗胜利.x,
                        Locationinformation.战斗胜利.y,
                        Locationinformation.战斗胜利.width,
                        Locationinformation.战斗胜利.height);
                    if (str == "战斗胜利") { won = true; break; }
                }
                if (!won) return false;

                ctx.Tap(Locationinformation.Return, 2, 2);
                if (!ctx.Sleep(2000, 3000)) return false;
            }
            return true;
        }
    }

    /// <summary>
    /// 复仇X 流程：开始防御 → 拖动战场 → 直接进入战斗（无敌人筛选）。
    /// </summary>
    public static class RevengeFlow
    {
        public static bool Run(GameFlowContext ctx, int runCount)
        {
            for (int i = 1; i <= runCount; i++)
            {
                if (ctx.ShouldStop) return false;
                Debug.WriteLine($"执行{i}");
                ctx.Tap(Locationinformation.开始防御, 2, 2);
                if (!ctx.Sleep(1500, 2200)) return false;
                ctx.Drag(
                    (Locationinformation.EnemyUIDrag.startX, Locationinformation.EnemyUIDrag.startY),
                    (Locationinformation.EnemyUIDrag.endX, Locationinformation.EnemyUIDrag.endY),
                    jitterX: 100, endJitterWide: true);
                BattleFlow.Run(ctx, ctx.HeroChecked, Locationinformation.HeroPosition.Length);
                if (!ctx.Sleep(1500, 2200)) return false;
            }
            return true;
        }
    }
}
