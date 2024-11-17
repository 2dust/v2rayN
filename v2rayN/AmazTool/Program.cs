/**
 * 该程序使用JSON文件对C#应用程序进行本地化。
 * 程序根据系统当前的语言加载相应的语言文件。
 * 如果当前语言不被支持，则默认使用英语。
 * 
 * 库:
 *  - System.Collections.Generic
 *  - System.Globalization
 *  - System.IO
 *  - System.Text.Json
 * 
 * 用法:
 *  - 为每种支持的语言创建JSON文件（例如，en.json，zh.json）。
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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;

namespace AmazTool
{
    public class Localization
    {
        private static readonly Lazy<Localization> _instance = new Lazy<Localization>(() => new Localization());
        private Dictionary<string, string> translations;

        // 私有构造函数，防止外部实例化
        private Localization()
        {
            string currentLanguage = CultureInfo.CurrentCulture.Name;

            if (currentLanguage != "zh-CN" && currentLanguage != "en-US")
            {
                currentLanguage = "en-US";
            }

            string jsonFilePath = $"{currentLanguage}.json";
            if (!LoadTranslations(jsonFilePath))
            {
                jsonFilePath = "en-US.json";
                LoadTranslations(jsonFilePath);
            }
        }

        // 公有静态属性用于获取单例实例
        public static Localization Instance
        {
            get
            {
                return _instance.Value;
            }
        }

        private bool LoadTranslations(string jsonFilePath)
        {
            try
            {
                var json = File.ReadAllText(jsonFilePath);
                translations = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load JSON file: {ex.Message}");
                Thread.Sleep(5000);
                Environment.Exit(1);
                return false;
            }
        }

        public string Translate(string key)
        {
            return translations != null && translations.TryGetValue(key, out string value) ? value : key;
        }
    }

    internal static class Program
    {

        [STAThread]
        public static void Main(string[] args)
        {
            Localization localization = Localization.Instance;

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
