/*
==============================================================================
THIRD-PARTY CA CERTIFICATE DATA ATTRIBUTION
==============================================================================

This component includes CA certificate data obtained from third-party sources.

1. Common CA Database (CCADB)
------------------------------------------------------------------------------
Website:
https://www.ccadb.org/

Data source:
https://ccadb.my.salesforce-sites.com/mozilla/IncludedRootsPEMTxt?TrustBitsInclude=Websites

License:
Community Data License Agreement – Permissive, Version 2.0 (CDLA-2.0 Permissive)

The names and trademarks of CCADB contributors may not be used to endorse or
promote products derived from this data without prior written permission.

License text:
https://cdla.dev/permissive-2-0/


2. Chromium Root Store
------------------------------------------------------------------------------
Source:
Chromium Project

Data source:
https://chromium.googlesource.com/chromium/src/+/main/net/data/ssl/chrome_root_store/root_store.certs?format=TEXT

License:
BSD 3-Clause License

Copyright:
Copyright (c) The Chromium Authors

The Chromium Root Store data is distributed under the BSD 3-Clause License.

License text:
https://opensource.org/licenses/BSD-3-Clause
==============================================================================
*/

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
    const string certdataUrl = "https://ccadb.my.salesforce-sites.com/mozilla/IncludedRootsPEMTxt?TrustBitsInclude=Websites";
    var outputFile = Path.Combine(outputDir, "mozilla_roots_pem");

    Console.WriteLine("Downloading Mozilla certdata.txt...");
    var content = await client.GetStringAsync(certdataUrl);

    var pemMatches = Regex.Matches(content, @"(-----BEGIN CERTIFICATE-----[\s\S]*?-----END CERTIFICATE-----)");
    var pems = new List<string>();
    foreach (Match m in pemMatches)
    {
        pems.Add(m.Groups[1].Value.Trim());
    }

    await File.WriteAllTextAsync(outputFile, string.Join("\n\n", pems));
    Console.WriteLine($"Mozilla roots saved to: {outputFile} ({pems.Count} certificates)");
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