/**
 * 该程序使用JSON文件对C#应用程序进行本地化。
 * 程序根据系统当前的语言加载相应的语言文件。
 * 如果当前语言不被支持，则默认使用英语。
 * 
 * 库:
 *  - System.Collections.Generic
 *  - System.Globalization
 *  - System.IO
 *  - Newtonsoft.Json
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
 * 
 * 注意:
 *  - 确保通过NuGet安装了Newtonsoft.Json库。
 */

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

public class Localization
{
    private Dictionary<string, string> translations;

    public Localization()
    {
        // 获取当前系统的完整文化名称 例：zh-CN en-US
        string currentLanguage = CultureInfo.CurrentCulture.Name;

        // 如果当前语言不是"zh-CN"或"en-US"，默认使用英文
        if (currentLanguage != "zh-CN" && currentLanguage != "en-US")
        {
            currentLanguage = "en-US";
        }

        // 加载相应语言的JSON文件
        string jsonFilePath = $"{currentLanguage}.json";
        if (!LoadTranslations(jsonFilePath))
        {
            // 如果加载失败，则使用默认语言
            jsonFilePath = "en-US.json";
            LoadTranslations(jsonFilePath);
        }
    }

    private bool LoadTranslations(string jsonFilePath)
    {
        try
        {
            // 读取JSON文件内容
            var json = File.ReadAllText(jsonFilePath);
            // 解析JSON内容
            translations = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            return true; // 成功读取和解析JSON文件
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load JSON file: {ex.Message}");
            Environment.Exit(1);
            return false; // 读取或解析JSON文件失败
        }
    }

    public string Translate(string key)
    {
        return translations != null && translations.TryGetValue(key, out string value) ? value : key;
    }
}
