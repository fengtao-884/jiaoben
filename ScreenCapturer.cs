using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace 脚本
{
    public class ScreenCapturer
    {
        protected string _adbPath;
        protected string _deviceSerial;
        protected const string DEVICE_SERIAL = "emulator-5554";

        public ScreenCapturer(string adbPath)
        {
            _adbPath = adbPath;
            _deviceSerial = DEVICE_SERIAL;
        }

        /// <summary>
        /// 拼接 ADB 全局参数：设备序列号非空时在最前面加 -s {serial}，否则原样返回。
        /// </summary>
        internal string BuildCommand(string arguments) =>
            // 当前恒走 serial 分支：_deviceSerial 构造即固定 emulator-5554 且永不置空，null 分支仅防御性保留
            _deviceSerial != null ? $"-s {_deviceSerial} {arguments}" : arguments;

        protected void ExecuteAdbCommand(string arguments)
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = _adbPath,
                Arguments = BuildCommand(arguments),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            });
            if (process == null) throw new Exception("无法启动ADB进程");
            // 先并发读 stdout/stderr 再等退出，避免任一缓冲写满导致死锁
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(3000))
            {
                try { process.Kill(); } catch { /* 已退出 */ }
                throw new Exception($"ADB命令超时: {arguments}");
            }
            if (process.ExitCode != 0)
                throw new Exception($"ADB命令执行失败: {stderr.GetAwaiter().GetResult()}");
        }
    }
    public class LdPlayerCapturer : ScreenCapturer
    {
        // 雷电模拟器ADB通常路径
        private static readonly string DefaultLdAdbPath = @"F:\leidian\LDPlayer9\adb.exe";

        public LdPlayerCapturer()
            : base(DefaultLdAdbPath)
        {
        }
        protected string ExecuteAdbCommandWithOutput(string arguments)
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = _adbPath,
                    Arguments = BuildCommand(arguments),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8
                };

                using (var process = Process.Start(processInfo))
                {
                    if (process == null)
                        return string.Empty;

                    // 先并发读 stdout/stderr 再等退出，避免缓冲写满死锁
                    var stdoutTask = process.StandardOutput.ReadToEndAsync();
                    var stderrTask = process.StandardError.ReadToEndAsync();
                    if (!process.WaitForExit(3000))
                    {
                        try { process.Kill(); } catch { /* 已退出 */ }
                        return string.Empty;
                    }
                    string output = stdoutTask.GetAwaiter().GetResult();

                    if (process.ExitCode != 0)
                    {
                        Debug.WriteLine($"ADB命令执行失败: {stderrTask.GetAwaiter().GetResult()}");
                        return string.Empty;
                    }

                    return output;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"执行ADB命令失败: {ex.Message}");
                return string.Empty;
            }
        }
        public void StartAppByLauncher()
        {
          
            try
            {
                // 方法1：使用monkey命令
                string command = "shell monkey -p com.tjhry.zhanjing.hry -c android.intent.category.LAUNCHER 1";

                ExecuteAdbCommand(command);

                // 等待应用启动
                Thread.Sleep(2000);

               
                Debug.WriteLine($"已启动应用: com.tjhry.zhanjing.hry");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"启动应用失败: {ex.Message}");
            }
        }
        public void CloseGameByCommonNames()
        {
            string[] possibleGamePackages = new string[]
            {
        "com.tjhry.zhanjing.hry",          // 战警大国崛起 - 从dumpsys找到的包名
    
            };

            foreach (string package in possibleGamePackages)
            {
                try
                {
                    CloseApp(package);
                    Debug.WriteLine($"已尝试关闭: {package}");
                    // 稍微延迟，避免连续关闭太快
                    System.Threading.Thread.Sleep(100);
                }
                catch
                {
                    // 忽略不存在的包名
                }
            }
        }
        public void CloseApp(string packageName)
        {
            if (string.IsNullOrEmpty(packageName))
                return;

            try
            {
                string command = $"shell am force-stop {packageName}";

                ExecuteAdbCommand(command);
                Debug.WriteLine($"已关闭应用: {packageName}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"关闭应用失败: {ex.Message}");
            }
        }
        /// <summary>
        /// 获取当前活动的雷电模拟器窗口截图（使用Windows API）
        /// 适用于需要获取前台窗口的场景
        /// </summary>
        public Bitmap CaptureActiveWindow()
        {
            // 先激活雷电模拟器窗口
            ActivateLdWindow();

            // 等待窗口渲染完成
            System.Threading.Thread.Sleep(100);

            // 使用ADB截图
            return CaptureToBitmap();
        }

        private void ActivateLdWindow()
        {
            // 使用Windows API激活窗口
            var window = FindWindowByTitleContains("雷电模拟器");
            if (window != IntPtr.Zero)
            {
                SetForegroundWindow(window);
            }
        }

        // Windows API声明
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder strText, int maxCount);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int GetWindowTextLength(IntPtr hWnd);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private IntPtr FindWindowByTitleContains(string titlePart)
        {
            IntPtr foundHandle = IntPtr.Zero;
            EnumWindows((hWnd, lParam) =>
            {
                int size = GetWindowTextLength(hWnd);
                if (size++ > 0)
                {
                    StringBuilder sb = new StringBuilder(size);
                    GetWindowText(hWnd, sb, size);
                    if (sb.ToString().Contains(titlePart))
                    {
                        foundHandle = hWnd;
                        return false; // 找到窗口，停止枚举
                    }
                }
                return true; // 继续枚举
            }, IntPtr.Zero);

            return foundHandle;
        }

        /// <summary>
        /// 直接获取屏幕Bitmap（最推荐，效率最高）
        /// </summary>
        public Bitmap CaptureToBitmap()
        {
            try
            {
                string command = BuildCommand("exec-out screencap -p");

                var processInfo = new ProcessStartInfo
                {
                    FileName = _adbPath,
                    Arguments = command,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = null // 重要：保持二进制原始数据
                };

                using (var process = Process.Start(processInfo))
                {
                    if (process == null)
                        throw new Exception("无法启动ADB进程");

                    using (var memoryStream = new MemoryStream())
                    {
                        // 异步读 stderr 与 stdout，避免任一缓冲写满导致死锁
                        var stderrTask = process.StandardError.ReadToEndAsync();
                        var stdoutTask = process.StandardOutput.BaseStream.CopyToAsync(memoryStream);

                        if (!process.WaitForExit(3000)) // 等待3秒，超时则终止进程
                        {
                            try { process.Kill(); } catch { /* 已退出 */ }
                            throw new Exception("截图失败: ADB命令超时");
                        }

                        stdoutTask.GetAwaiter().GetResult(); // 进程已退出，收尾读取

                        if (process.ExitCode != 0)
                            throw new Exception($"截图失败: {stderrTask.GetAwaiter().GetResult()}");

                        memoryStream.Position = 0;
                        return new Bitmap(memoryStream);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("截图失败", ex);
            }
        }

        public void Tap(int x, int y)
        {
            ExecuteAdbCommand($"shell input tap {x} {y}");
        }
        public void Drag(int startX, int startY, int endX, int endY, int holdTime = 200, int duration = 500)
        {
            try
            {
                // holdTime 已废弃（Android input tap 不支持 -t 长按参数，原命令无效），保留仅为兼容签名
                // 使用"input touchscreen swipe"代替"input swipe"，这样不会触发点击事件
                string swipeCommand = $"shell input touchscreen swipe {startX} {startY} {endX} {endY} {duration}";

                ExecuteAdbCommand(swipeCommand);
            }
            catch (Exception ex)
            {
                throw new Exception("右键拖拽操作失败", ex);
            }
        }
        public void SendF5ToLdPlayer()
        {
            try
            {
                const uint WM_KEYDOWN = 0x0100;
                const uint WM_KEYUP = 0x0101;
                const uint VK_F5 = 0x74;

                // 找到雷电模拟器窗口
                IntPtr mainWindow = FindWindowByTitleContains("雷电模拟器");
                if (mainWindow == IntPtr.Zero)
                {
                    throw new Exception("未找到雷电模拟器窗口");
                }

                // 找到渲染窗口（通常是一个子窗口）
                IntPtr renderWindow = FindWindowEx(mainWindow, IntPtr.Zero, "Qt5QWindowIcon", null);
                if (renderWindow == IntPtr.Zero)
                {
                    renderWindow = FindWindowEx(mainWindow, IntPtr.Zero, "RenderWindow", null);
                }

                IntPtr targetWindow = renderWindow != IntPtr.Zero ? renderWindow : mainWindow;

                // 激活窗口
                SetForegroundWindow(mainWindow);
                Thread.Sleep(100);

                int totalDuration = 2000;
                int clicks = 5; // 按键次数
                int interval = totalDuration / clicks; // 每次按键间隔

                for (int i = 0; i < clicks; i++)
                {
                    // 发送按键按下
                    PostMessage(targetWindow, WM_KEYDOWN, (IntPtr)VK_F5, IntPtr.Zero);

                    // 短暂延迟（模拟按键按下的时间）
                    Thread.Sleep(50);

                    // 发送按键释放
                    PostMessage(targetWindow, WM_KEYUP, (IntPtr)VK_F5, IntPtr.Zero);

                    // 等待到下一次按键
                    if (i < clicks - 1)
                    {
                        Thread.Sleep(interval - 50);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("发送F5键失败", ex);
            }
        }
        public void MoveMouseTo(int x, int y)
        {
            try
            {
                // 设置鼠标位置
                SetCursorPos(x, y);
            }
            catch (Exception ex)
            {
                throw new Exception($"移动鼠标失败: {ex.Message}");
            }
        }
        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int X, int Y);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);
    }
}