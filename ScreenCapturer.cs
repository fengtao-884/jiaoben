using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using static System.Windows.Forms.AxHost;

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
        protected void ExecuteAdbCommand(string arguments)
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = _adbPath,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using (var process = Process.Start(processInfo))
            {
                if (process == null)
                    throw new Exception("无法启动ADB进程");

                process.WaitForExit(3000);

                if (process.ExitCode != 0)
                {
                    string error = process.StandardError.ReadToEnd();
                    throw new Exception($"ADB命令执行失败: {error}");
                }
            }
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
                    Arguments = arguments,
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

                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit(3000);

                    if (process.ExitCode != 0)
                    {
                        string error = process.StandardError.ReadToEnd();
                        Debug.WriteLine($"ADB命令执行失败: {error}");
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
                string command = _deviceSerial != null
                    ? $"-s {_deviceSerial} shell monkey -p com.tjhry.zhanjing.hry -c android.intent.category.LAUNCHER 1"
                    : $"shell monkey -p com.tjhry.zhanjing.hry -c android.intent.category.LAUNCHER 1";

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
                string command = _deviceSerial != null
                    ? $"-s {_deviceSerial} shell am force-stop {packageName}"
                    : $"shell am force-stop {packageName}";

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
                string command = _deviceSerial != null
                    ? $"-s {_deviceSerial} exec-out screencap -p"
                    : "exec-out screencap -p";

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

                    // 读取二进制流
                    using (var memoryStream = new MemoryStream())
                    {
                        byte[] buffer = new byte[4096];
                        int bytesRead;

                        // 从标准输出读取二进制数据
                        while ((bytesRead = process.StandardOutput.BaseStream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            memoryStream.Write(buffer, 0, bytesRead);
                        }

                        process.WaitForExit(3000); // 等待3秒

                        if (process.ExitCode != 0)
                        {
                            string error = process.StandardError.ReadToEnd();
                            throw new Exception($"截图失败: {error}");
                        }

                        // 从内存流创建Bitmap
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
                // 发送右键长按命令
                string rightDownCommand = _deviceSerial != null
                    ? $"-s {_deviceSerial} shell input tap -t {holdTime} {startX} {startY}"
                    : $"shell input tap -t {holdTime} {startX} {startY}";

                // 实际上Android ADB不直接支持右键点击，我们需要使用一个替代方案
                // 使用"input touchscreen swipe"代替"input swipe"，这样不会触发点击事件
                string swipeCommand = _deviceSerial != null
                    ? $"-s {_deviceSerial} shell input touchscreen swipe {startX} {startY} {endX} {endY} {duration}"
                    : $"shell input touchscreen swipe {startX} {startY} {endX} {endY} {duration}";

                ExecuteAdbCommand(rightDownCommand);
                Thread.Sleep(holdTime);
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