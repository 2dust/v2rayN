﻿using System.Globalization;
using System.Text.Json;

namespace AmazTool
{
    public class LocalizationHelper
    {
        private static Dictionary<string, string> languageResources = new();

        static LocalizationHelper()
        {
            // 加载语言资源
            LoadLanguageResources();
        }

        /// <summary>
        /// 加载外部 JSON 文件中的语言资源
        /// </summary>
        private static void LoadLanguageResources()
        {
            try
            {
                string currentLanguage = CultureInfo.CurrentCulture.Name;
                if (currentLanguage != "zh-CN" && currentLanguage != "en-US")
                {
                    currentLanguage = "en-US";
                }

                string jsonFilePath = $"{currentLanguage}.json";
                if (!File.Exists(jsonFilePath))
                {
                    jsonFilePath = "en-US.json";
                }

                var json = File.ReadAllText(jsonFilePath);
                if (!string.IsNullOrEmpty(json))
                {
                    languageResources = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load language resources: {ex.Message}");
                languageResources = []; // 初始化为空字典
            }
        }

        /// <summary>
        /// 获取系统当前语言的本地化字符串
        /// </summary>
        /// <param name="key">要翻译的关键字</param>
        /// <returns>对应语言的本地化字符串，如果没有找到则返回关键字</returns>
        public static string GetLocalizedValue(string key)
        {
            if (languageResources != null && languageResources.TryGetValue(key, out var translation))
            {
                return translation;
            }

            return key;
        }
    }
}
