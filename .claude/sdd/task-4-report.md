# Task 4 Report — ScreenCapturer 抽 BuildCommand + 修 ADB 死锁/超时

Status: **DONE** — 全部测试通过，主项目编译通过，无新增警告。

## What Changed

### `D:\Code\脚本\ScreenCapturer.cs`

1. **新增 `internal string BuildCommand(string arguments)`**（构造器之后）
   - `_deviceSerial != null` 时返回 `$"-s {_deviceSerial} {arguments}"`，否则原样返回。
   - 用 `internal`（非 `private`）以便测试程序集（同源 `<Compile Include>` 链接）访问。
   - 未改动 `_deviceSerial`/`DEVICE_SERIAL`/`DefaultLdAdbPath` 的值。

2. **替换全部内联 serial 三元表达式**
   - `ExecuteAdbCommand`：`Arguments = BuildCommand(arguments)`（计划 Step 4 示例指定的汇聚点，所有经它执行的命令统一加 `-s`）。
   - `StartAppByLauncher`：三元折叠为裸命令 `"shell monkey -p ... 1"`。
   - `CloseApp`：三元折叠为裸命令 `$"shell am force-stop {packageName}"`。
   - `CaptureToBitmap`：`string command = BuildCommand("exec-out screencap -p");`。
   - `Drag`：三元折叠为裸命令 `$"shell input touchscreen swipe ..."`。
   - `ExecuteAdbCommandWithOutput`：`Arguments = BuildCommand(arguments)`（同类型进程包装器，一并统一）。
   - 注意 `Tap` 无三元，经 `ExecuteAdbCommand` 自动获得 serial 前缀（行为变化：现在带 `-s emulator-5554`，符合 Task 6「当前恒走 serial 分支」预期）。

3. **修 ADB 死锁 + 超时**
   - `ExecuteAdbCommand`：`var stderr = process.StandardError.ReadToEndAsync();` 放在 `WaitForExit(3000)` 之前（避免 stderr 缓冲写满死锁）；超时则 `try { process.Kill(); } catch { }` 并抛 `ADB命令超时`；非零退出抛 `ADB命令执行失败: {stderr.Result}`。
   - `ExecuteAdbCommandWithOutput`：并发 `ReadToEndAsync()` 读 stdout/stderr 再 `WaitForExit(3000)`；超时 Kill 并返回空串；非零退出打 `Debug.WriteLine` 返回空串。
   - `CaptureToBitmap`：先 `var stderrTask = process.StandardError.ReadToEndAsync();` 再读 stdout 二进制流；`WaitForExit(3000)` 超时则 Kill 并抛 `截图失败: ADB命令超时`；非零退出抛 `截图失败: {stderrTask.Result}`。**`StandardOutputEncoding = null` 保持不动**（二进制 screencap）。

### `D:\Code\脚本\tests\脚本.Tests\ScreenCapturerTests.cs`（新建）

- `BuildCommand_带设备序列号`：`new ScreenCapturer(@"C:\fake\adb.exe")`，断言 `BuildCommand("shell input tap 1 2") == "-s emulator-5554 shell input tap 1 2"`。
- `CloseApp_空包名_不抛异常`：空包名直接 return，不触发 ADB 调用。
  - **与计划测试代码的一处修正**：计划里写 `c.CloseApp("")` 但 `CloseApp` 定义在 `LdPlayerCapturer`，`ScreenCapturer` 上没有；测试改为 `new LdPlayerCapturer()`（构造仅存路径，空包名返回前不会启动任何进程，安全）。

### csproj

- 无需修改。`ScreenCapturerTests.cs` 位于测试项目目录内，被 SDK 默认 globbing 自动编译（全量运行已发现 2 个新用例，验证通过）。

## RED/GREEN Evidence

- **Baseline（改前）**：`dotnet test` 全量 = 33 通过 / 0 失败。
- **RED 前提**：改前 `grep BuildCommand` 无任何命中（`BuildCommand` 不存在）；`_deviceSerial` 三元分散在 5 处内联。测试引用 `BuildCommand` 在改动前必编译失败。
- **GREEN**：
  - `dotnet test --filter ScreenCapturerTests` → **2 通过 / 0 失败**（构建成功）。
  - `dotnet test` 全量 → **35 通过 / 0 失败**（33 基线 + 2 新增）。
  - `dotnet build 脚本.csproj`（主项目）→ 成功，0 错误。

## Warnings

- 仅 2 条**既有** CS8625，位于 `SendF5ToLdPlayer` 的 `FindWindowEx(..., null)`（ScreenCapturer.cs 325/328 行），与本次改动无关；**未新增任何警告**。

## Files Changed

- `D:\Code\脚本\ScreenCapturer.cs`（修改）
- `D:\Code\脚本\tests\脚本.Tests\ScreenCapturerTests.cs`（新建）

## Concerns / Notes

1. **双前缀防护**：`ExecuteAdbCommand`/`ExecuteAdbCommandWithOutput` 内部统一 `BuildCommand`，调用方必须传裸命令，否则会重复加 `-s`。已核对全代码库，除这 4 处（StartAppByLauncher/CloseApp/Tap/Drag）外无其它 `ExecuteAdbCommand` 调用方，均传裸命令。
2. **`Tap` 行为变化**：改前 `Tap` 不带 serial，改后经 `BuildCommand` 恒带 `-s emulator-5554`。符合计划 Task 6 对 `_deviceSerial` 恒非空分支的预期，属有意集中化。
3. **`ExecuteAdbCommandWithOutput` 当前无调用方**（grep 确认），改动为潜在修复 + 一致性，无回归面。
4. **计划测试代码 `CloseApp` 实例类型有误**，已修正为 `LdPlayerCapturer`（详见上）。
5. `CaptureToBitmap` 二进制读 stdout 保持同步 `BaseStream.Read` 循环 + stderr 异步，语义不变；仅顺序/超时处理变化。

## Fix (review Important #1)

**Change made** — `D:\Code\脚本\ScreenCapturer.cs`, `CaptureToBitmap()`:

原实现对 stdout 采用**同步阻塞读取循环**（`while (bytesRead = StandardOutput.BaseStream.Read(...))`），若 adb 挂起且 stdout 保持打开，该循环会无限阻塞，导致其后的 `WaitForExit(3000)` / `process.Kill()` 永不执行——正是该方法想要防御的超时路径失效。

改为**异步排空**：
- 删除同步 `Read` 循环及不再使用的局部变量 `byte[] buffer` / `int bytesRead`。
- 用 `var stdoutTask = process.StandardOutput.BaseStream.CopyToAsync(memoryStream);` 异步将二进制 stdout 拷贝进 `MemoryStream`；stderr 仍以 `ReadToEndAsync()` 异步读取。两个流在等待退出期间持续排空，任一缓冲写满都不会死锁。
- `WaitForExit(3000)` 现在可真正触达：超时即 `process.Kill()` 并抛 `截图失败: ADB命令超时`。
- 进程正常退出后 `stdoutTask.GetAwaiter().GetResult()` 收尾读取，再按 `ExitCode` 判断、`stderrTask.Result` 取错误信息、`memoryStream.Position = 0` 后 `new Bitmap(memoryStream)` 返回。
- 外层 `try/catch`、`process == null` 守卫、`processInfo`（含 `StandardOutputEncoding = null`，保证二进制 screencap 原样）均保持不变。

**Covering tests** — `ScreenCapturerTests`（`tests\脚本.Tests\ScreenCapturerTests.cs`）：`BuildCommand_带设备序列号`、`CloseApp_空包名_不抛异常`。

**Commands run** — both from `D:\Code\脚本`:

1. `dotnet test "D:\Code\脚本\tests\脚本.Tests\脚本.Tests.csproj" --filter ScreenCapturerTests`
   → `已通过! - 失败: 0，通过: 2，已跳过: 0，总计: 2`
2. `dotnet test "D:\Code\脚本\tests\脚本.Tests\脚本.Tests.csproj"`
   → `已通过! - 失败: 0，通过: 35，已跳过: 0，总计: 35`

No new warnings (only the 2 pre-existing CS8625 in `SendF5ToLdPlayer`).
