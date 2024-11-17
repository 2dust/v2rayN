using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;

public class Localization
{
    // 使用 Lazy<T> 实现单例模式，确保 Localization 实例在需要时才会创建
    private static readonly Lazy<Localization> _instance = new(() => new Localization());
    private Dictionary<string, string> translations;

    // 私有构造函数，防止外部实例化
    private Localization()
    {
        // 获取当前系统的完整文化名称，例如：zh-CN, en-US
        string currentLanguage = CultureInfo.CurrentCulture.Name;

        // 如果当前语言不是 "zh-CN" 或 "en-US"，默认使用英语
        if (currentLanguage != "zh-CN" && currentLanguage != "en-US")
        {
            currentLanguage = "en-US";
        }

        // 加载相应语言的 JSON 文件
        string jsonFilePath = $"{currentLanguage}.json";
        if (!LoadTranslations(jsonFilePath))
        {
            // 如果加载失败，则使用默认语言的 JSON 文件
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

    // 加载翻译文件，并将内容解析为字典
    private bool LoadTranslations(string jsonFilePath)
    {
        try
        {
            // 读取 JSON 文件内容
            var json = File.ReadAllText(jsonFilePath);
            // 解析 JSON 内容并转换为字典
            translations = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            return true; // 成功读取并解析 JSON 文件
        }
        catch (Exception ex)
        {
            // 处理读取或解析 JSON 文件时的异常
            Console.WriteLine($"Failed to load JSON file: {ex.Message}");
            Thread.Sleep(5000);
            Environment.Exit(1);
            return false; // 读取或解析 JSON 文件失败
        }
    }

    // 根据键值返回相应的翻译，如果不存在则返回键值本身
    public string Translate(string key)
    {
        return translations != null && translations.TryGetValue(key, out string value) ? value : key;
    }
}
