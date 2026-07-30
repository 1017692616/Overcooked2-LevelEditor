using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;
using UnityEditor;
using UnityEngine;


public static class OC2SetupTools
{
    private const string GameFolderName = "Overcooked! 2";
    private const string StreamingAssetsRelativePath = "Overcooked2_Data/StreamingAssets/Windows";
    private const string ProjectStreamingAssetsPath = "Assets/StreamingAssets/Windows";

    [MenuItem("Tools/OC2 Setup/Auto Setup", false, 0)]
    private static void AutoSetup()
    {
        string gameStreamingAssets = FindGameStreamingAssets();
        if (string.IsNullOrEmpty(gameStreamingAssets))
        {
            EditorUtility.DisplayDialog(
                "OC2 Setup",
                "Overcooked! 2 StreamingAssets/Windows was not found.\n\nPlease confirm the Steam game is installed, then run Tools > OC2 Setup > Check Environment.",
                "OK");
            return;
        }

        string linkMessage;
        if (!EnsureStreamingAssetsLink(gameStreamingAssets, out linkMessage))
        {
            EditorUtility.DisplayDialog("OC2 Setup", linkMessage, "OK");
            return;
        }

        AssetDatabase.Refresh();
        string generatorMessage = RunDlcReferenceGenerator(gameStreamingAssets);
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "OC2 Setup Complete",
            linkMessage + "\n\n" + generatorMessage + "\n\nYou can now add DLC recipes in LevelInfoSO.recipes.",
            "OK");
    }

    [MenuItem("Tools/OC2 Setup/Check Environment", false, 1)]
    private static void CheckEnvironment()
    {
        List<string> lines = new List<string>();
        string gameStreamingAssets = FindGameStreamingAssets();
        string projectPath = Path.GetFullPath(ProjectStreamingAssetsPath);

        lines.Add("Project: " + Directory.GetParent(Application.dataPath).FullName);
        lines.Add("Unity: " + Application.unityVersion);
        lines.Add("Game StreamingAssets: " + (string.IsNullOrEmpty(gameStreamingAssets) ? "NOT FOUND" : gameStreamingAssets));
        lines.Add("Project link: " + projectPath + (Directory.Exists(projectPath) ? " (exists)" : " (missing)"));
        lines.Add("Python: " + FindPythonCommand());
        lines.Add("");
        lines.Add("Note: Windows bundles are not committed to Git. Auto Setup only creates a local junction and generates lightweight references.");

        EditorUtility.DisplayDialog("OC2 Environment", string.Join("\n", lines.ToArray()), "OK");
    }

    private static string FindGameStreamingAssets()
    {
        HashSet<string> candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string steamRoot in FindSteamRoots())
        {
            AddGameCandidate(candidates, steamRoot);
            string libraryFolders = Path.Combine(steamRoot, "steamapps/libraryfolders.vdf");
            if (File.Exists(libraryFolders))
            {
                string text = File.ReadAllText(libraryFolders);
                MatchCollection matches = Regex.Matches(text, "\"path\"\\s+\"([^\"]+)\"");
                foreach (Match match in matches)
                {
                    AddGameCandidate(candidates, match.Groups[1].Value.Replace("\\\\", "\\"));
                }
            }
        }

        foreach (string drive in Directory.GetLogicalDrives())
        {
            AddGameCandidate(candidates, drive);
        }

        foreach (string candidate in candidates)
        {
            if (Directory.Exists(candidate))
            {
                return candidate;
            }
        }
        return null;
    }

    private static void AddGameCandidate(HashSet<string> candidates, string steamRoot)
    {
        if (string.IsNullOrEmpty(steamRoot))
        {
            return;
        }
        string gamePath = Path.Combine(steamRoot, "steamapps/common/" + GameFolderName);
        string streamingPath = Path.Combine(gamePath, StreamingAssetsRelativePath);
        candidates.Add(streamingPath);
    }

    private static IEnumerable<string> FindSteamRoots()
    {
        List<string> roots = new List<string>();
        string[] registryPaths =
        {
            @"HKEY_CURRENT_USER\Software\Valve\Steam",
            @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam",
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam",
        };
        foreach (string registryPath in registryPaths)
        {
            try
            {
                object value = Registry.GetValue(registryPath, "SteamPath", null);
                if (value == null)
                {
                    value = Registry.GetValue(registryPath, "InstallPath", null);
                }
                if (value != null)
                {
                    roots.Add(value.ToString().Replace("/", "\\"));
                }
            }
            catch
            {
                // Registry access is optional; common drive locations are also scanned.
            }
        }

        roots.Add(@"C:\Program Files (x86)\Steam");
        roots.Add(@"C:\Program Files\Steam");
        roots.Add(@"D:\SteamLibrary");
        roots.Add(@"E:\SteamLibrary");
        roots.Add(@"F:\SteamLibrary");
        return roots;
    }

    private static bool EnsureStreamingAssetsLink(string sourcePath, out string message)
    {
        string targetPath = Path.GetFullPath(ProjectStreamingAssetsPath);
        sourcePath = Path.GetFullPath(sourcePath);
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath));

        if (Directory.Exists(targetPath))
        {
            DirectoryInfo existing = new DirectoryInfo(targetPath);
            if ((existing.Attributes & FileAttributes.ReparsePoint) != 0)
            {
                message = "Project bundle junction already exists:\n" + targetPath;
                return true;
            }

            message = "A real Windows bundle folder already exists, so it was not overwritten:\n" + targetPath +
                "\n\nBack up and remove that folder first if you want Auto Setup to create a junction.";
            return false;
        }

        ProcessResult result = RunProcess("cmd.exe", "/c mklink /J " + Quote(targetPath) + " " + Quote(sourcePath));
        if (result.ExitCode != 0 || !Directory.Exists(targetPath))
        {
            message = "Failed to create the bundle junction:\n" + result.Output;
            return false;
        }

        message = "Created local bundle junction:\n" + targetPath + "\n-> " + sourcePath;
        return true;
    }

    private static string RunDlcReferenceGenerator(string gameStreamingAssets)
    {
        string python = FindPythonCommand();
        if (string.IsNullOrEmpty(python))
        {
            return "Python was not found, so DLC reference generation was skipped. You can run tools/generate_dlc_assets.py later.";
        }

        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string scriptPath = Path.Combine(projectRoot, "tools/generate_dlc_assets.py");
        ProcessResult result = RunProcess(
            python,
            (python == "py" ? "-3 " : string.Empty) +
            Quote(scriptPath) + " --game-streaming-assets " + Quote(gameStreamingAssets),
            projectRoot);
        if (result.ExitCode != 0)
        {
            return "DLC reference generation failed:\n" + result.Output;
        }

        return "DLC lightweight references were generated under Assets/dlc.\n" + LastLines(result.Output, 4);
    }

    private static string FindPythonCommand()
    {
        ProcessResult python = RunProcess("python", "--version");
        if (python.ExitCode == 0)
        {
            return "python";
        }
        ProcessResult py = RunProcess("py", "-3 --version");
        return py.ExitCode == 0 ? "py" : null;
    }

    private static ProcessResult RunProcess(string fileName, string arguments, string workingDirectory = null)
    {
        try
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = string.IsNullOrEmpty(workingDirectory) ? Directory.GetCurrentDirectory() : workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };
            using (Process process = Process.Start(startInfo))
            {
                string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
                process.WaitForExit();
                return new ProcessResult(process.ExitCode, output);
            }
        }
        catch (Exception error)
        {
            return new ProcessResult(-1, error.Message);
        }
    }

    private static string Quote(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    private static string LastLines(string text, int count)
    {
        string[] lines = text.Replace("\r", "").Split('\n');
        int start = Math.Max(0, lines.Length - count - 1);
        return string.Join("\n", lines, start, lines.Length - start);
    }

    private struct ProcessResult
    {
        public int ExitCode;
        public string Output;

        public ProcessResult(int exitCode, string output)
        {
            ExitCode = exitCode;
            Output = output ?? string.Empty;
        }
    }
}
