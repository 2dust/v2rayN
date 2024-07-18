using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace v2rayN;

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
      if (string.IsNullOrWhiteSpace(strJson))
      {
        return default;
      }
      return JsonSerializer.Deserialize<T>(strJson);
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
      var options = new JsonSerializerOptions
      {
        WriteIndented = indented ? true : false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
      };
      result = JsonSerializer.Serialize(obj, options);
    }
    catch (Exception ex)
    {
      Logging.SaveLog(ex.Message, ex);
    }
    return result;
  }

  /// <summary>
  /// SerializeToNode
  /// </summary>
  /// <param name="obj"></param>
  /// <returns></returns>
  public static JsonNode? SerializeToNode(object? obj) => JsonSerializer.SerializeToNode(obj);

  /// <summary>
  /// Save as json file
  /// </summary>
  /// <param name="obj"></param>
  /// <param name="filePath"></param>
  /// <param name="nullValue"></param>
  /// <returns></returns>
  public static int ToFile(object? obj, string? filePath, bool nullValue = true)
  {
    if (filePath is null)
    {
      return -1;
    }
    try
    {
      using FileStream file = File.Create(filePath);

      var options = new JsonSerializerOptions
      {
        WriteIndented = true,
        DefaultIgnoreCondition = nullValue ? JsonIgnoreCondition.Never : JsonIgnoreCondition.WhenWritingNull
      };

      JsonSerializer.Serialize(file, obj, options);
      return 0;
    }
    catch (Exception ex)
    {
      Logging.SaveLog(ex.Message, ex);
      return -1;
    }
  }
}