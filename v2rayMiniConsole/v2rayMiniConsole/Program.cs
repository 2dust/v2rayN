// See https://aka.ms/new-console-template for more information
using System.Runtime.InteropServices;
using v2rayMiniConsole;
using v2rayMiniConsole.Resx;
using v2rayN;
using v2rayN.Enums;
using v2rayN.Handler;

partial class Program
{
    private static bool _exitProgram = false;
    // 导入 SetConsoleCtrlHandler 函数的定义
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleCtrlHandler(HandlerRoutine handler, bool add);

    // 委托类型，与 HandlerRoutine 函数签名匹配
    private delegate bool HandlerRoutine(CtrlTypes ctrlType);

    // 枚举类型，用于表示控制事件类型
    private enum CtrlTypes
    {
        CTRL_C_EVENT = 0,
        CTRL_BREAK_EVENT,
        CTRL_CLOSE_EVENT,
        CTRL_LOGOFF_EVENT = 5,
        CTRL_SHUTDOWN_EVENT
    }

    // 自定义的控制台事件处理程序
    private static bool ConsoleEventHandler(CtrlTypes ctrlType)
    {
        switch (ctrlType)
        {
            case CtrlTypes.CTRL_C_EVENT:
            case CtrlTypes.CTRL_BREAK_EVENT:
            case CtrlTypes.CTRL_CLOSE_EVENT:
                // 在这里执行关闭前的动作
                Console.WriteLine("Console is closing. Executing cleanup actions...");
                _exitProgram = true;
                MainTask.Instance.BroadcastExit(false);                
                return true;
            case CtrlTypes.CTRL_LOGOFF_EVENT:
            case CtrlTypes.CTRL_SHUTDOWN_EVENT:
                _exitProgram = true;
                MainTask.Instance.BroadcastExit(true);
                return true;
            // 其他控制事件处理...
            default:
                return false;
        }
    }
    static void Main(string[] args)
    {
        // 注册控制台事件处理程序
        if (!SetConsoleCtrlHandler(ConsoleEventHandler, true))
        {
            Console.WriteLine("Error registering Console Ctrl Handler");
        }

        MainTask.Instance.SetListenerType(LazyConfig.Instance.GetConfig().sysProxyType);
        StartupInfo();
        string inputBuffer = ""; // 用于存储非回车键的输入
        
        Console.Write(">");
        while (!_exitProgram)
        {   
            ConsoleKeyInfo keyInfo = Console.ReadKey(true); // 读取第一个按键，不显示在屏幕上

            if (!RunningObjects.Instance.IsMessageOn() && keyInfo.Key == ConsoleKey.Backspace && inputBuffer.Length > 0)
            {
                // 如果按下退格键且输入缓冲区不为空，则删除最后一个字符
                inputBuffer = inputBuffer.Substring(0, inputBuffer.Length - 1);

                // 清除退格键之后的字符（如果有的话）
                Console.Write("\b \b"); // \b 是退格符，后面跟一个空格覆盖原字符，再退格一次
            }
            else if (keyInfo.Modifiers == ConsoleModifiers.Control && keyInfo.Key == ConsoleKey.T)
            {
                // Ctrl + T 被按下
                HandleCtrlT();
            }
            else if (!RunningObjects.Instance.IsMessageOn() && keyInfo.Key == ConsoleKey.Enter)
            {
                // 如果是回车键，则处理之前的输入（如果有的话）
                string input = inputBuffer.Trim().ToLower();
                inputBuffer = ""; // 重置输入缓冲区
                Console.WriteLine();
                Console.WriteLine();
                // 使用switch语句来处理不同的输入
                switch (input)
                {
                    case "h":
                        Console.Write(ResUI.HelpInfo.Replace("\\t", "\t").Replace("\\n", "\n"));
                        break;
                    case "q":
                        _exitProgram = true;
                        Console.WriteLine("bye bye");
                        MainTask.Instance.BroadcastExit(false);                        
                        return; // 退出循环
                    case "set_language":
                        MainTask.Instance.SetLanguage();
                        break;
                    case "switch_server":
                        MainTask.Instance.SwitchServer();
                        break;
                    case "show_current_subs":
                        MainTask.Instance.ShowCurrentSubscriptions();
                        break;
                    case "add_subscription":
                        MainTask.Instance.AddSubscription();
                        break;
                    case "remove_subscription":
                        MainTask.Instance.RemoveSubscription();
                        break;
                    case "change_proxy_mode":
                        MainTask.Instance.ChangeSystemProxyMode();
                        break;
                    case "change_routing":
                        MainTask.Instance.Change_Routing();
                        break;
                    default:
                        if (!string.IsNullOrEmpty(input))
                        {
                            Console.WriteLine($"你输入了：{input}");
                        }
                        break;
                }
                if (!_exitProgram)
                {
                    Console.WriteLine();
                    Console.Write(">");
                }
            }
            else if (!RunningObjects.Instance.IsMessageOn() && keyInfo.KeyChar != '\0' && !char.IsControl(keyInfo.KeyChar)) // 排除控制字符
            {
                // 如果不是Enter键或Ctrl + T，则将字符添加到输入缓冲区
                inputBuffer += keyInfo.KeyChar;

                // 可以选择性地回显非特殊字符（如果需要的话）
                Console.Write(keyInfo.KeyChar);
            }
        }
    }

    static void StartupInfo()
    {
        Console.WriteLine($"  {Utils.GetVersion(false)} - {ResUI.StartupInfo}");
        Console.WriteLine();
    }

    static void HandleCtrlT()
    {
        // 在这里处理 Ctrl+M 被按下的逻辑
        RunningObjects.Instance.ToggleMessage();
    }
}







