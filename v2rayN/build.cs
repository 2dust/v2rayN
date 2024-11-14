using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;

class Program
{
    static void Main(string[] args)
    {
        string outputPath = args.Length > 0 ? args[0] : Path.Combine(Directory.GetCurrentDirectory(), "bin", "v2rayN");

        Console.WriteLine("Building...");

        // Publish v2rayN project for Windows
        if (!PublishProject(@"v2rayN\v2rayN.csproj", "win-x64", outputPath, false))
        {
            Environment.Exit(1);
        }

        // Publish v2rayN.Desktop project for Linux
        if (!PublishProject(@"v2rayN.Desktop\v2rayN.Desktop.csproj", "linux-x64", outputPath, true))
        {
            Environment.Exit(1);
        }

        // Remove .pdb files if they exist
        CleanUpPdbFiles(outputPath);

        Console.WriteLine("Build done");

        // List output files
        ListOutputFiles(outputPath);

        // Create zip archive
        CreateZipArchive(outputPath, "v2rayN.zip");

        Environment.Exit(0);
    }

    static bool PublishProject(string projectPath, string runtimeIdentifier, string outputPath, bool selfContained)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"publish \"{projectPath}\" -c Release -r {runtimeIdentifier} --self-contained {selfContained.ToString().ToLower()} -p:PublishReadyToRun=false -p:PublishSingleFile=true -o \"{Path.Combine(outputPath, runtimeIdentifier)}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (var process = Process.Start(startInfo))
        {
            process.WaitForExit();
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            
            if (process.ExitCode != 0)
            {
                Console.WriteLine($"Error publishing {projectPath}: {error}");
                return false;
            }
            
            Console.WriteLine(output);
            return true;
        }
    }

    static void CleanUpPdbFiles(string outputPath)
    {
        string winPdbPath = Path.Combine(outputPath, "win-x64");
        string linuxPdbPath = Path.Combine(outputPath, "linux-x64");

        if (Directory.Exists(winPdbPath))
        {
            foreach (var file in Directory.GetFiles(winPdbPath, "*.pdb"))
            {
                File.Delete(file);
            }
        }

        if (Directory.Exists(linuxPdbPath))
        {
            foreach (var file in Directory.GetFiles(linuxPdbPath, "*.pdb"))
            {
                File.Delete(file);
            }
        }
    }

    static void ListOutputFiles(string outputPath)
    {
        foreach (var dir in Directory.GetDirectories(outputPath))
        {
            Console.WriteLine($"Contents of {dir}:");
            foreach (var file in Directory.GetFiles(dir))
            {
                Console.WriteLine($" - {Path.GetFileName(file)}");
            }
        }
    }

    static void CreateZipArchive(string sourceFolder, string zipFileName)
    {
        string zipFilePath = Path.Combine(Directory.GetCurrentDirectory(), zipFileName);
        
        if (File.Exists(zipFilePath))
        {
            File.Delete(zipFilePath); // Remove existing zip file
        }

        ZipFile.CreateFromDirectory(sourceFolder, zipFilePath);
        
        Console.WriteLine($"Created zip archive: {zipFileName}");
    }
}
