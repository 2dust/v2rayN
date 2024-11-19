using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace AmazTool
{
    public class LocalizationHelper
    {
        private static Dictionary<string, string> _languageResources = [];

        static LocalizationHelper()
        {
            // 加载语言资源
            LoadLanguageResources();
        }

        private static void LoadLanguageResources()
        {
            try
            {
                var currentLanguage = CultureInfo.CurrentCulture.Name;
                if (currentLanguage != "zh-CN" && currentLanguage != "en-US")
                {
                    currentLanguage = "en-US";
                }

                var resourceName = $"AmazTool.Assets.{currentLanguage}.json";
                var assembly = Assembly.GetExecutingAssembly();

                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null) return;

                using StreamReader reader = new(stream);
                var json = reader.ReadToEnd();
                if (!string.IsNullOrEmpty(json))
                {
                    _languageResources = JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Failed to read language resource file: {ex.Message}");
            }
            catch (JsonException ex)
            {
                Console.WriteLine($"Failed to parse JSON data: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected error occurred: {ex.Message}");
            }
        }

        public static string GetLocalizedValue(string key)
        {
            return _languageResources.TryGetValue(key, out var translation) ? translation : key;
        }
    }
}