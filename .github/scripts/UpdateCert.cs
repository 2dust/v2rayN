using System;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;

var client = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };

var outputDir = args.Length > 0 
    ? args[0] 
    : Path.Combine(Environment.CurrentDirectory, "v2rayN", "ServiceLib", "Sample");

Directory.CreateDirectory(outputDir);

Console.WriteLine("=== Mozilla + Chrome Root Store PEM Converter ===\n");
Console.WriteLine($"Output directory: {outputDir}\n");

// Process Mozilla
await ProcessMozillaAsync(outputDir);

// Process Chrome
await ProcessChromeAsync(outputDir);

Console.WriteLine("\nAll done!");

async Task ProcessMozillaAsync(string outputDir)
{
    const string certdataUrl = "https://raw.githubusercontent.com/mozilla-firefox/firefox/refs/heads/release/security/nss/lib/ckfw/builtins/certdata.txt";
    var outputFile = Path.Combine(outputDir, "mozilla_roots_pem");

    Console.WriteLine("Downloading Mozilla certdata.txt...");
    var content = await client.GetStringAsync(certdataUrl);

    Console.WriteLine("Parsing MULTILINE_OCTAL to PEM...");
    var pems = ParseMozillaCertData(content);

    await File.WriteAllTextAsync(outputFile, string.Join("\n\n", pems));
    Console.WriteLine($"Mozilla roots saved to: {outputFile} ({pems.Count} certificates)");
}

List<string> ParseMozillaCertData(string text)
{
    var pems = new List<string>();

    var matches = Regex.Matches(text, 
        @"CKA_VALUE MULTILINE_OCTAL\s*([\s\S]*?)\s*END", 
        RegexOptions.Multiline);

    foreach (Match m in matches)
    {
        var octalBlock = m.Groups[1].Value.Trim();
        var der = OctalToBytes(octalBlock);
        if (der.Length > 0)
        {
            var pem = ConvertToPem(der);
            pems.Add(pem);
        }
    }
    return pems;
}

byte[] OctalToBytes(string octalBlock)
{
    var bytes = new List<byte>();
    var matches = Regex.Matches(octalBlock, @"\\(\d{1,3})");
    foreach (Match m in matches)
    {
        bytes.Add((byte)Convert.ToInt32(m.Groups[1].Value, 8));
    }
    return bytes.ToArray();
}

string ConvertToPem(byte[] der)
{
    var base64 = Convert.ToBase64String(der);
    var sb = new StringBuilder();
    sb.Append("-----BEGIN CERTIFICATE-----\n");
    
    for (int i = 0; i < base64.Length; i += 64)
    {
        var length = Math.Min(64, base64.Length - i);
        sb.Append(base64.Substring(i, length));
        sb.Append("\n");
    }
    
    sb.Append("-----END CERTIFICATE-----\n");
    return sb.ToString().TrimEnd();
}

async Task ProcessChromeAsync(string outputDir)
{
    const string chromeCertsUrl = "https://chromium.googlesource.com/chromium/src/+/main/net/data/ssl/chrome_root_store/root_store.certs?format=TEXT";
    var chromeOutput = Path.Combine(outputDir, "chrome_roots_pem");

    Console.WriteLine("Downloading Chrome root_store.certs...");
    var base64Content = await client.GetStringAsync(chromeCertsUrl);
    var decoded = Convert.FromBase64String(base64Content.Replace("\n", ""));

    var text = Encoding.UTF8.GetString(decoded);
    
    var pemMatches = Regex.Matches(text, @"(-----BEGIN CERTIFICATE-----[\s\S]*?-----END CERTIFICATE-----)");
    var pems = new List<string>();
    foreach (Match m in pemMatches)
    {
        pems.Add(m.Groups[1].Value.Trim());
    }

    await File.WriteAllTextAsync(chromeOutput, string.Join("\n\n", pems));
    Console.WriteLine($"Chrome roots saved to: {chromeOutput} ({pems.Count} certificates)");
}