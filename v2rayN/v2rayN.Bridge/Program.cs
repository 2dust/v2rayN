using System.Text.Json;
using ServiceLib.Handler;
using ServiceLib.Handler.Fmt;
using ServiceLib.Models;

if (args.Length < 2)
{
    Console.WriteLine("Usage: v2rayN.Bridge <command> <argument>");
    Console.WriteLine("Commands: to-uri, parse-sub, to-json");
    return;
}

string command = args[0];
string argument = args[1];

switch (command)
{
    case "to-json":
        ToJson(argument);
        break;
    case "parse-sub":
        ParseSub(argument);
        break;
    case "to-uri":
        ToUri(argument);
        break;
    default:
        Console.WriteLine("Unknown command");
        break;
}

static void ToJson(string shareLink)
{
    string msg;
    var item = FmtHandler.ResolveConfig(shareLink, out msg);

    if (item != null)
    {
        var json = JsonSerializer.Serialize(
            item,
            new JsonSerializerOptions { WriteIndented = true }
        );
        Console.WriteLine(json);
    }
    else
    {
        Console.WriteLine($"ERROR: {msg}");
    }
}

static void ParseSub(string configText)
{
    // For parsing subscription content or config text
    // Split by newlines and parse each line as a potential link
    string msg;
    var lines = configText.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
    var items = new List<ProfileItem>();

    foreach (var line in lines)
    {
        var trimmed = line.Trim();
        if (!string.IsNullOrEmpty(trimmed))
        {
            var item = FmtHandler.ResolveConfig(trimmed, out msg);
            if (item != null)
            {
                items.Add(item);
            }
        }
    }

    var json = JsonSerializer.Serialize(
        items,
        new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        }
    );
    Console.WriteLine(json);
}

static void ToUri(string jsonItem)
{
    try
    {
        var options = new JsonSerializerOptions();
        var item = JsonSerializer.Deserialize<ProfileItem>(jsonItem, options);

        if (item != null)
        {
            var uri = FmtHandler.GetShareUri(item);
            Console.WriteLine(uri ?? "Failed to generate URI");
        }
        else
        {
            Console.WriteLine("ERROR: Invalid ProfileItem JSON");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR: {ex.Message}");
    }
}
