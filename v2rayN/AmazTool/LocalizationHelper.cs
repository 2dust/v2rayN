using System.Collections.Generic;
using System.Globalization;

namespace AmazTool
{
    public class LocalizationHelper
    {
        /// <summary>
        /// 获取系统当前语言的本地化字符串
        /// </summary>
        /// <param name="key">要翻译的关键字</param>
        /// <returns>对应语言的本地化字符串，如果没有找到则返回关键字</returns>
        public static string GetLocalizedValue(string key)
        {
            // 定义支持的语言
            HashSet<string> supportedLanguages = ["zh", "en"];

            // 获取当前系统语言的 ISO 两字母代码
            string currentLanguage = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;

            // 如果当前语言不在支持的语言列表中，默认使用英文
            if (!supportedLanguages.Contains(currentLanguage))
            {
                currentLanguage = "en";
            }

            // 尝试获取对应语言的翻译
            if (languageResources.TryGetValue(key, out var translations))
            {
                if (translations.TryGetValue(currentLanguage, out var translation))
                {
                    return translation;
                }
            }

            // 如果未找到翻译，返回关键字本身
            return key;
        }

        /// <summary>
        /// 存储不同语言的本地化资源
        /// </summary>
        public static Dictionary<string, Dictionary<string, string>> languageResources = new()
        {
            {
                "Guidelines", new Dictionary<string, string>
                {
                    { "en", "Please run it from the main application." },
                    { "zh", "请从主应用运行！" }
                }
            },
            {
                "Upgrade_File_Not_Found", new Dictionary<string, string>
                {
                    { "en", "Upgrade failed, file not found." },
                    { "zh", "升级失败，文件不存在！" }
                }
            },
            {
                "In_Progress", new Dictionary<string, string>
                {
                    { "en", "In progress, please wait..." },
                    { "zh", "正在进行中，请等待..." }
                }
            },
            {
                "Try_Terminate_Process", new Dictionary<string, string>
                {
                    { "en", "Try to terminate the v2rayN process." },
                    { "zh", "尝试结束 v2rayN 进程..." }
                }
            },
            {
                "Failed_Terminate_Process", new Dictionary<string, string>
                {
                    { "en", "Failed to terminate the v2rayN.Close it manually,or the upgrade may fail." },
                    { "zh", "请手动关闭正在运行的v2rayN，否则可能升级失败。" }
                }
            },
            {
                "Start_Unzipping", new Dictionary<string, string>
                {
                    { "en", "Start extracting the update package." },
                    { "zh", "开始解压缩更新包..." }
                }
            },
            {
                "Success_Unzipping", new Dictionary<string, string>
                {
                    { "en", "Successfully extracted the update package!" },
                    { "zh", "解压缩更新包成功！" }
                }
            },
            {
                "Failed_Unzipping", new Dictionary<string, string>
                {
                    { "en", "Failed to extract the update package!" },
                    { "zh", "解压缩更新包失败！" }
                }
            },
            {
                "Failed_Upgrade", new Dictionary<string, string>
                {
                    { "en", "Upgrade failed!" },
                    { "zh", "升级失败！" }
                }
            },
            {
                "Success_Upgrade", new Dictionary<string, string>
                {
                    { "en", "Upgrade success!" },
                    { "zh", "升级成功！" }
                }
            },
            {
                "Information", new Dictionary<string, string>
                {
                    { "en", "Information" },
                    { "zh", "提示" }
                }
            },
            {
                "Restart_v2rayN", new Dictionary<string, string>
                {
                    { "en", "Start v2rayN, please wait..." },
                    { "zh", "正在重启，请等待..." }
                }
            }
        };
    }
}
