using Xunit;
using 脚本.Flows;

namespace 脚本.Tests
{
    /// <summary>
    /// 流程框架单元测试：用合成步骤验证 Runner 的重试/停止/顺序语义，
    /// 以及 Context 的停止传播——全部不依赖游戏与 ADB。
    /// </summary>
    public class GameFlowTests
    {
        /// <summary>构造测试用上下文：真实轻量依赖（不触碰设备）、isRunning 可控、无 UI</summary>
        private static (GameFlowContext ctx, Func<bool> setStopped) MakeCtx()
        {
            bool running = true;
            var capturer = new LdPlayerCapturer();
            var ctx = new GameFlowContext(
                capturer: capturer,
                recognizer: new GetNumberRecognizer(capturer),
                satelliteMatcher: new TemplateMatcher(Path.Combine(TestPaths.Templates, "satellite_base.png")),
                gunMatcher: new TemplateMatcher(Path.Combine(TestPaths.Templates, "gun.png")),
                resTitleMatcher: new TemplateMatcher(Path.Combine(TestPaths.Templates, "res_title.png")),
                random: new Random(12345),
                isRunning: () => running);
            return (ctx, () => running = false);
        }

        [Fact]
        public void 全部成功_流程返回真且按顺序执行()
        {
            var (ctx, _) = MakeCtx();
            var executed = new List<string>();
            var steps = new[]
            {
                new GameStep("步骤A", _ => { executed.Add("A"); return true; }),
                new GameStep("步骤B", _ => { executed.Add("B"); return true; }),
            };

            Assert.True(GameFlowRunner.Run(ctx, steps));
            Assert.Equal(new[] { "A", "B" }, executed);
        }

        [Fact]
        public void 步骤失败_后续不再执行_流程返回假()
        {
            var (ctx, _) = MakeCtx();
            var laterRan = false;
            var steps = new[]
            {
                new GameStep("失败步骤", _ => false),
                new GameStep("后续步骤", _ => { laterRan = true; return true; }),
            };

            Assert.False(GameFlowRunner.Run(ctx, steps));
            Assert.False(laterRan);
        }

        [Fact]
        public void 失败重试_达到上限后返回假()
        {
            var (ctx, _) = MakeCtx();
            int attempts = 0;
            var step = new GameStep("总是失败", _ => { attempts++; return false; }, maxRetries: 3);

            Assert.False(GameFlowRunner.Run(ctx, new[] { step }));
            Assert.Equal(3, attempts);
        }

        [Fact]
        public void 失败后重试成功_流程继续()
        {
            var (ctx, _) = MakeCtx();
            int attempts = 0;
            var steps = new[]
            {
                new GameStep("第二次才成功", _ => ++attempts >= 2, maxRetries: 3),
                new GameStep("收尾", _ => true),
            };

            Assert.True(GameFlowRunner.Run(ctx, steps));
            Assert.Equal(2, attempts);
        }

        [Fact]
        public void 步骤抛异常_按失败处理不崩溃()
        {
            var (ctx, _) = MakeCtx();
            var step = new GameStep("会炸的步骤", _ => throw new InvalidOperationException("boom"), maxRetries: 2);

            Assert.False(GameFlowRunner.Run(ctx, new[] { step }));
        }

        [Fact]
        public void 用户停止_后续步骤不再执行()
        {
            var (ctx, stop) = MakeCtx();
            var laterRan = false;
            var steps = new[]
            {
                new GameStep("触发停止", _ => { stop(); return true; }),
                new GameStep("不应执行", _ => { laterRan = true; return true; }),
            };

            Assert.False(GameFlowRunner.Run(ctx, steps));
            Assert.False(laterRan);
        }

        [Fact]
        public void Sleep_停止请求时提前中断并返回假()
        {
            var (ctx, stop) = MakeCtx();
            // 排一个 5 秒等待，200ms 后置停 → 应远早于 5 秒返回 false
            var done = new ManualResetEventSlim(false);
            bool result = true;
            var thread = new Thread(() => { result = ctx.Sleep(5000, 5000); done.Set(); });
            thread.Start();
            Thread.Sleep(200);
            stop();
            Assert.True(done.Wait(2000), "Sleep 应在停止后 2 秒内提前返回");
            Assert.False(result);
        }

        [Fact]
        public void ReadUi_无UI注入时直接求值()
        {
            var (ctx, _) = MakeCtx();
            Assert.Equal(42, ctx.ReadUi(() => 42));
        }

        [Fact]
        public void 构造空引用_抛参数异常()
        {
            bool running = true;
            Assert.Throws<ArgumentNullException>(() => new GameFlowContext(
                capturer: null!, recognizer: null!,
                satelliteMatcher: null!, gunMatcher: null!, resTitleMatcher: null!,
                random: new Random(), isRunning: () => running));
        }
    }
}
