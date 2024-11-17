using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;

public class Localization
{
    private Dictionary<string, string> translations;

    private Localization()
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
            translations = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            return true; // 成功读取和解析JSON文件
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load JSON file: {ex.Message}");
            Thread.Sleep(5000);
            Environment.Exit(1);
            return false; // 读取或解析JSON文件失败
        }
    }

    public string Translate(string key)
    {
        return translations != null && translations.TryGetValue(key, out string value) ? value : key;
    }
}
