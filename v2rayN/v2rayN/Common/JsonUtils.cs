using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;

namespace v2rayN
{
    internal class JsonUtils
    {
        /// <summary>
        /// DeepCopy
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static T DeepCopy<T>(T obj)
        {
            return Deserialize<T>(Serialize(obj, false))!;
        }

        /// <summary>
        /// Deserialize to object
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="strJson"></param>
        /// <returns></returns>
        public static T? Deserialize<T>(string? strJson)
        {
            try
            {
                if (string.IsNullOrEmpty(strJson))
                {
                    return default;
                }
                return JsonConvert.DeserializeObject<T>(strJson);
            }
            catch
            {
                return default;
            }
        }

        /// <summary>
        /// parse
        /// </summary>
        /// <param name="strJson"></param>
        /// <returns></returns>
        public static JObject? ParseJson(string strJson)
        {
            try
            {
                if (string.IsNullOrEmpty(strJson))
                {
                    return null;
                }

                return JObject.Parse(strJson);
            }
            catch
            {
                //SaveLog(ex.Message, ex);
                return null;
            }
        }

        /// <summary>
        /// Serialize Object to Json string
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="indented"></param>
        /// <returns></returns>
        public static string Serialize(object? obj, bool indented = true)
        {
            string result = string.Empty;
            try
            {
                if (obj == null)
                {
                    return result;
                }
                result = JsonConvert.SerializeObject(obj,
                                          indented ? Formatting.Indented : Formatting.None,
                                          new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
            }
            return result;
        }

        /// <summary>
        /// Save as json file
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="filePath"></param>
        /// <param name="nullValue"></param>
        /// <returns></returns>
        public static int ToFile(object? obj, string filePath, bool nullValue = true)
        {
            int result;
            try
            {
                using StreamWriter file = File.CreateText(filePath);
                var serializer = new JsonSerializer()
                {
                    Formatting = Formatting.Indented,
                    NullValueHandling = nullValue ? NullValueHandling.Include : NullValueHandling.Ignore
                };
                serializer.Serialize(file, obj);
                result = 0;
            }
            catch (Exception ex)
            {
                Logging.SaveLog(ex.Message, ex);
                result = -1;
            }
            return result;
        }
    }
}