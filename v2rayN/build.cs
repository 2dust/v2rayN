// This code is for .NET CLI tool usage with C#

using System;
using System.Diagnostics;
using System.IO;

class Program
{
    static void Main(string[] args)
    {
        string outputPath = @".\bin\v2rayN"; // Default output path

        Console.WriteLine("Building");

        // Publish for Windows
        ExecuteDotNetPublish(@".\v2rayN\v2rayN.csproj", "win-x64", outputPath);
        
        // Publish for Linux
        ExecuteDotNetPublish(@".\v2rayN.Desktop\v2rayN.Desktop.csproj", "linux-x64", outputPath);

        // Check if the last command was successful
        if (Process.GetCurrentProcess().ExitCode != 0)
        {
            Environment.Exit(Process.GetCurrentProcess().ExitCode);
        }

        // Remove PDB files if the directory exists
        if (Directory.Exists(Path.Combine(outputPath, "win-x64")))
        {
            DirectoryInfo winX64Dir = new DirectoryInfo(Path.Combine(outputPath, "win-x64"));
            foreach (FileInfo file in winX64Dir.GetFiles("*.pdb"))
            {
                file.Delete();
            }
        }

        if (Directory.Exists(Path.Combine(outputPath, "linux-x64")))
        {
            DirectoryInfo linuxX64Dir = new DirectoryInfo(Path.Combine(outputPath, "linux-x64"));
            foreach (FileInfo file in linuxX64Dir.GetFiles("*.pdb"))
            {
                file.Delete();
            }
        }

        Console.WriteLine("Build done");

        // List output path contents
        string[] files = Directory.GetFiles(outputPath);
        foreach (string file in files)
        {
            Console.WriteLine(file);
        }

        // Compress output files into a zip file
        Process.Start(new ProcessStartInfo
        {
            FileName = "7z",
            Arguments = $"a v2rayN.zip {outputPath}",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        }).WaitForExit();

        Environment.Exit(0);
    }

    static void ExecuteDotNetPublish(string projectPath, string runtimeIdentifier, string outputPath)
    {
        ProcessStartInfo processStartInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"publish \"{projectPath}\" -c Release -r {runtimeIdentifier} --self-contained false -p:PublishReadyToRun=false -p:PublishSingleFile=true -o \"{Path.Combine(outputPath, runtimeIdentifier)}\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using (Process process = Process.Start(processStartInfo))
        {
            process.WaitForExit();
        }
    }
}
