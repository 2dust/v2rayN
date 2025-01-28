using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ServiceLib.Common
{
	public class YamlUtils
	{
		private static readonly string _tag = "YamlUtils";

		#region YAML

		/// <summary>
		/// 反序列化成对象
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
		/// 序列化
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
			var deserializer = new DeserializerBuilder()
				.WithNamingConvention(PascalCaseNamingConvention.Instance)
				.Build();
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
}
