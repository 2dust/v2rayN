namespace ServiceLib.Tests.Common;

public class JsonUtilsMetadataTests
{
    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task Serialize_PreservesIndentationNullsAndUnicode(bool indented, bool nullValue)
    {
        var item = new WebDavItem { Url = "https://example.com/русский", UserName = "tester" };

        var json = JsonUtils.Serialize(item, indented, nullValue);
        var node = JsonUtils.ParseJson(json)!.AsObject();

        await node["Url"]!.GetValue<string>().Should().BeEqualTo(item.Url);
        await json.Contains("русский").Should().BeTrue();
        await json.Contains(Environment.NewLine).Should().BeEqualTo(indented);
        await node.ContainsKey("Password").Should().BeEqualTo(nullValue);
        await node.ContainsKey("DirName").Should().BeEqualTo(nullValue);
        await JsonUtils.Deserialize<WebDavItem>(json)!.UserName.Should().BeEqualTo("tester");
    }

    [Test]
    public async Task Deserialize_AcceptsCommentsAndCaseInsensitiveNames()
    {
        const string json = "{/* existing configuration */ \"uRl\":\"https://example.com\",\"USERNAME\":\"tester\"}";
        var item = JsonUtils.Deserialize<WebDavItem>(json)!;

        await item.Url.Should().BeEqualTo("https://example.com");
        await item.UserName.Should().BeEqualTo("tester");
        await JsonUtils.Deserialize<WebDavItem>("invalid").Should().BeNull();
        await JsonUtils.Deserialize<WebDavItem>(" ").Should().BeNull();
    }

    [Test]
    public async Task NodeAndCamelCaseSerialization_UseTheRequestedOptions()
    {
        var item = new WebDavItem { Url = "https://example.com" };
        var node = JsonUtils.SerializeToNode(item, nullValue: true)!.AsObject();
        var camelCase = JsonUtils.ParseJson(JsonUtils.SerializeCamelCase(item))!.AsObject();

        await node.ContainsKey("Password").Should().BeTrue();
        await JsonUtils.SerializeToNode(item)!.AsObject().ContainsKey("Password").Should().BeFalse();
        await camelCase["url"]!.GetValue<string>().Should().BeEqualTo(item.Url);
        await camelCase.ContainsKey("Url").Should().BeFalse();
        await camelCase.ContainsKey("password").Should().BeFalse();

        var values = JsonUtils.SerializeToArray(["first", "second"])!;
        await values.Count.Should().BeEqualTo(2);
        await values[0]!.GetValue<string>().Should().BeEqualTo("first");
        await values[1]!.GetValue<string>().Should().BeEqualTo("second");
        await JsonUtils.SerializeToArray(null).Should().BeNull();
    }

    [Test]
    public async Task DeepCopy_KeepsProfileDataAndCreatesAnIndependentModel()
    {
        var item = new ProfileItem { IndexId = "profile", Address = "example.com", Port = 443, Remarks = "Original" };
        var copy = JsonUtils.DeepCopy(item)!;

        await copy.IndexId.Should().BeEqualTo(item.IndexId);
        await copy.Address.Should().BeEqualTo(item.Address);
        await copy.Port.Should().BeEqualTo(443);
        copy.Remarks = "Changed";
        await item.Remarks.Should().BeEqualTo("Original");
    }

    [Test]
    public async Task UnsupportedTypes_PreserveTheExistingFailureResults()
    {
        await JsonUtils.Serialize(new UnsupportedValue()).Should().BeEqualTo(string.Empty);
        await JsonUtils.SerializeCamelCase(new UnsupportedValue()).Should().BeEqualTo(string.Empty);
        await JsonUtils.Deserialize<UnsupportedValue>("{\"Name\":\"value\"}").Should().BeNull();
    }

    private sealed class UnsupportedValue
    {
        public string Name { get; set; } = "value";
    }
}
