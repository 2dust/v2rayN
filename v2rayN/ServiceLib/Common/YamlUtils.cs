using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ServiceLib.Common;

public class YamlUtils
{
    private static readonly string _tag = "YamlUtils";

    #region YAML

    /// <summary>
    /// Deserialize
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="str"></param>
    /// <returns></returns>
    public static T FromYaml<T>(string str)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(PascalCaseNamingConvention.Instance)
            .Build();
        try
        {
            var obj = deserializer.Deserialize<T>(str);
            return obj;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            return deserializer.Deserialize<T>("");
        }
    }

    /// <summary>
    /// Serialize
    /// </summary>
    /// <param name="obj"></param>
    /// <returns></returns>
    public static string ToYaml(object? obj)
    {
        var result = string.Empty;
        if (obj == null)
        {
            return result;
        }
        var serializer = new SerializerBuilder()
                .WithNamingConvention(HyphenatedNamingConvention.Instance)
                .Build();

        try
        {
            result = serializer.Serialize(obj);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
        }
        return result;
    }

    public static string? PreprocessYaml(string str)
    {
        try
        {
            var mergingParser = new MergingParser(new Parser(new StringReader(str)));
            var obj = new DeserializerBuilder().Build().Deserialize(mergingParser);
            return ToYaml(obj);
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            return null;
        }
    }

    #endregion YAML
}
