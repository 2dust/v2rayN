using System.Collections.Generic;
using System.Reflection;
using System.Resources;

namespace v2rayN
{
    public class UIRes
    {
        static ResourceManager res = new ResourceManager("v2rayN.Resx.ResUI", Assembly.GetExecutingAssembly());

        static string LoadString(ResourceManager resMgr, string key)
        {
            string value = resMgr.GetString(key);
            if (value == null)
            {
                throw new KeyNotFoundException($"key: {key}");
            }
            return value;
        }

        public static string I18N(string key)
        {
            return LoadString(res, key);
        }
    }
}
