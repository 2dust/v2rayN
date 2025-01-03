using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace ServiceLib.Common
{
    public class JsonUtils
    {
        private static readonly string _tag = "JsonUtils";

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
                if (string.IsNullOrWhiteSpace(strJson))
                {
                    return default;
                }
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                return JsonSerializer.Deserialize<T>(strJson, options);
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
        public static JsonNode? ParseJson(string strJson)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(strJson))
                {
                    return null;
                }
                return JsonNode.Parse(strJson);
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
        /// <param name="nullValue"></param>
        /// <returns></returns>
        public static string Serialize(object? obj, bool indented = true, bool nullValue = false)
        {
            var result = string.Empty;
            try
            {
                if (obj == null)
                {
                    return result;
                }
                var options = new JsonSerializerOptions
                {
                    WriteIndented = indented,
                    DefaultIgnoreCondition = nullValue ? JsonIgnoreCondition.Never : JsonIgnoreCondition.WhenWritingNull
                };
                result = JsonSerializer.Serialize(obj, options);
            }
            catch (Exception ex)
            {
                Logging.SaveLog(_tag, ex);
            }
            return result;
        }

        /// <summary>
        /// SerializeToNode
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static JsonNode? SerializeToNode(object? obj) => JsonSerializer.SerializeToNode(obj);
    }
}