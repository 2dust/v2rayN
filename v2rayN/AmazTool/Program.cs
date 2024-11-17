/**
 * 使用JSON文件对C#应用程序进行本地化。
 * 程序根据系统当前的语言加载相应的语言文件。
 * 如果当前语言不被支持，则默认使用英语。
 * 
 * 用法:
 *  - 为每种支持的语言创建JSON文件（例如，en-US.json，zh-CN.json）。
 *  - 将JSON文件放置程序同目录中。
 *  - 运行程序，它将根据系统当前的语言加载翻译。
 *  - 调用方式： localization.Translate("Try_Terminate_Process") //返回一个 string 字符串
 * 示例JSON文件（en.json）：
 * {
 *     "Restart_v2rayN": "Start v2rayN, please wait...",
 *     "Guidelines": "Please run it from the main application."
 * }
 * 
 * 示例JSON文件（zh.json）：
 * {
 *     "Restart_v2rayN": "正在重启，请等待...",
 *     "Guidelines": "请从主应用运行！"
 * }
 */

using System;
using System.Threading;

namespace AmazTool
{
    internal static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        private static void Main(string[] args)
        {
            var localization = new Localization();

            if (args.Length == 0)
            {
                // 不能直接打开更新程序
                Console.WriteLine(localization.Translate("Guidelines"));
                Thread.Sleep(5000);
                return;
            }

            // 解析并拼接命令行参数以获取文件名
            var fileName = Uri.UnescapeDataString(string.Join(" ", args));
            // 调用升级方法进行文件处理
            UpgradeApp.Upgrade(fileName);
        }
    }
}
