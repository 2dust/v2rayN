using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace ServiceLib.Common;

public class YamlUtils
{
    private static readonly string _tag = "YamlUtils";

    #region YAML

    /// <summary>
    /// Deserialize
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    public static YamlMappingNode FromYaml(string str)
    {
        //var deserializer = new DeserializerBuilder()
        //    .WithNamingConvention(PascalCaseNamingConvention.Instance)
        //    .Build();
        //try
        //{
        //    var obj = deserializer.Deserialize<T>(str);
        //    return obj;
        //}
        //catch (Exception ex)
        //{
        //    Logging.SaveLog(_tag, ex);
        //    return deserializer.Deserialize<T>("");
        //}
        try
        {
            var yaml = new YamlStream();
            using var reader = new StringReader(str);
            yaml.Load(reader);
            var rootNode = (YamlMappingNode)yaml.Documents[0].RootNode;
            return rootNode;
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            return new YamlMappingNode();
        }
    }

    /// <summary>
    /// Serialize
    /// </summary>
    /// <param name="node"></param>
    /// <returns></returns>
    public static string ToYaml(YamlMappingNode? node)
    {
        var result = string.Empty;
        if (node == null)
        {
            return result;
        }
        try
        {
            var doc = new YamlDocument(node);
            var stream = new YamlStream(doc);
            using var writer = new StringWriter();
            stream.Save(writer, assignAnchors: false);
            result = writer.ToString();
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
            using var reader = new StringReader(str);
            var parser = new Parser(reader);
            var mergingParser = new MergingParser(parser);
            var yamlStream = new YamlStream();
            yamlStream.Load(mergingParser);
            using var writer = new StringWriter();
            yamlStream.Save(writer, assignAnchors: false);

            return writer.ToString();
        }
        catch (Exception ex)
        {
            Logging.SaveLog(_tag, ex);
            return null;
        }
    }

    #endregion YAML
}
