using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace v2rayN.Handler
{
    /// <summary>
    /// 路由规则文件处理类
    /// </summary>
    class RoutingRuleHandler
    {
        /// <summary>
        /// Parse Pac to v2ray rule
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        public static List<string> ParsePac(string filePath)
        {
            string result = Utils.LoadResource(filePath);
            if (Utils.IsNullOrEmpty(result))
            {
                return null;
            }

            //取得rule
            string pattern = @"(?is)(?<=\[)(.*)(?=\])";
            Regex rgx = new Regex(pattern);
            result = rgx.Match(result).Value;
            int index = result.IndexOf("];");
            result = result.Substring(0, index);
            if (Utils.IsNullOrEmpty(result))
            {
                return null;
            }

            string[] arrPac = result.Split(',');
            List<string> lstPac = new List<string>();
            foreach (string str in arrPac)
            {
                //处理有效值
                if (Utils.IsNullOrEmpty(str)
                    || str.Length <= 3)
                {
                    continue;
                }
                string value = str.Replace("\",", "").Replace("\"", "").Replace(",", "").Replace("\r\n", "").Replace(" ", "");
                lstPac.Add(value);
            }

            return lstPac;
        }

    }
}
