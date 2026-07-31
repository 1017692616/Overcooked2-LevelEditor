using LevelEditor;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;


public static class CreateAssetBundles
{
    private const string GameFolderName = "Overcooked! 2";
    private const string GameExeName = "Overcooked2.exe";
    private const string GameProcessName = "Overcooked2";
    private const string LevelsRelativePath = "BepInEx/plugins/OC2DIYLevel/levels";

    [MenuItem("Tools/Build AssetBundles", false, 100)]
    static void BuildAllAssetBundles()
    {
        BuildAllAssetBundlesInternal(BuildAssetBundleOptions.None);
    }

    [MenuItem("Tools/Build AssetBundles (ForceRebuild)", false, 101)]
    static void BuildAllAssetBundlesForceRebuild()
    {
        BuildAllAssetBundlesInternal(BuildAssetBundleOptions.ForceRebuildAssetBundle);
    }

    static void BuildAllAssetBundlesInternal(BuildAssetBundleOptions options)
    {
        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (!TargetSceneSaveValidator.CheckPrepareForBuilding(activeScene))
            return;
        if (!ValidateCurrentLevelSceneContents(activeScene))
            return;

        List<AssetBundleBuild> builds = new List<AssetBundleBuild>();
        foreach (string bundleName in AssetDatabase.GetAllAssetBundleNames())
        {
            if (IsLocalDlcBundle(bundleName))
            {
                continue;
            }
            AddBundleBuild(builds, bundleName);
        }

        string assetBundleDirectory = "Assets/AssetBundles";
        if (!Directory.Exists(assetBundleDirectory))
        {
            Directory.CreateDirectory(assetBundleDirectory);
        }

        BuildPipeline.BuildAssetBundles(
            assetBundleDirectory,
            builds.ToArray(),
            options,
            BuildTarget.StandaloneWindows);
    }

    [MenuItem("Tools/Build Current Level AssetBundles", false, 90)]
    static void BuildCurrentLevelAssetBundles()
    {
        BuildCurrentLevelAssetBundlesInternal(BuildAssetBundleOptions.None);
    }

    [MenuItem("Tools/Build Current Level AssetBundles (ForceRebuild)", false, 91)]
    static void BuildCurrentLevelAssetBundlesForceRebuild()
    {
        BuildCurrentLevelAssetBundlesInternal(BuildAssetBundleOptions.ForceRebuildAssetBundle);
    }

    static void BuildCurrentLevelAssetBundlesInternal(BuildAssetBundleOptions options)
    {
        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (!TargetSceneSaveValidator.CheckPrepareForBuilding(activeScene))
            return;

        if (string.IsNullOrEmpty(activeScene.path))
        {
            EditorUtility.DisplayDialog("Build Current Level", "Please save the current scene before building.", "OK");
            return;
        }

        if (!ValidateCurrentLevelSceneContents(activeScene))
        {
            return;
        }

        AssetImporter sceneImporter = AssetImporter.GetAtPath(activeScene.path);
        if (sceneImporter == null || string.IsNullOrEmpty(sceneImporter.assetBundleName))
        {
            EditorUtility.DisplayDialog(
                "Build Current Level",
                "The current scene has no AssetBundle name. Select the scene asset and set its AssetBundle name first.",
                "OK");
            return;
        }

        string sceneBundleName = sceneImporter.assetBundleName;
        int slashIndex = sceneBundleName.LastIndexOf('/');
        if (slashIndex <= 0)
        {
            EditorUtility.DisplayDialog(
                "Build Current Level",
                "The current scene AssetBundle name should look like level_name/scene_name.",
                "OK");
            return;
        }

        string levelBundlePrefix = sceneBundleName.Substring(0, slashIndex);
        NormalizeCurrentLevelDlcReferences(levelBundlePrefix);
        List<AssetBundleBuild> builds = new List<AssetBundleBuild>();
        AddBundleBuild(builds, sceneBundleName);

        foreach (string bundleName in AssetDatabase.GetAllAssetBundleNames())
        {
            if (bundleName.StartsWith(levelBundlePrefix + "/info_", System.StringComparison.OrdinalIgnoreCase))
            {
                AddBundleBuild(builds, bundleName);
            }
            else if (bundleName.StartsWith(levelBundlePrefix + "/", System.StringComparison.OrdinalIgnoreCase) &&
                     IsNonSceneBundle(bundleName))
            {
                if (IsLocalDlcBundle(bundleName))
                {
                    continue;
                }
                AddBundleBuild(builds, bundleName);
            }
        }

        if (builds.Count <= 1)
        {
            Debug.LogWarning("No info_* bundle found for " + levelBundlePrefix + ". The level may not appear in-game without its info bundle.");
        }

        string assetBundleDirectory = "Assets/AssetBundles";
        if (!Directory.Exists(assetBundleDirectory))
        {
            Directory.CreateDirectory(assetBundleDirectory);
        }

        AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
            assetBundleDirectory,
            builds.ToArray(),
            options,
            BuildTarget.StandaloneWindows);

        if (manifest == null)
        {
            EditorUtility.DisplayDialog(
                "Build Current Level",
                "AssetBundle build failed. The game level folder was not changed.",
                "OK");
            return;
        }

        string installMessage = InstallBuiltLevel(levelBundlePrefix, builds.Select(x => x.assetBundleName));
        EditorUtility.DisplayDialog(
            "Build Current Level",
            "Built " + builds.Count + " bundle(s) for " + levelBundlePrefix + " into Assets/AssetBundles/" + levelBundlePrefix + ".\n\n" + installMessage,
            "OK");
    }

    static void AddBundleBuild(List<AssetBundleBuild> builds, string bundleName)
    {
        if (builds.Any(x => x.assetBundleName == bundleName))
        {
            return;
        }

        string[] assetNames = AssetDatabase.GetAssetPathsFromAssetBundle(bundleName);
        if (assetNames == null || assetNames.Length == 0)
        {
            return;
        }

        string[] expandedAssetNames = ExpandAssetNames(assetNames);
        if (expandedAssetNames.Length == 0)
        {
            return;
        }

        builds.Add(new AssetBundleBuild
        {
            assetBundleName = bundleName,
            assetNames = expandedAssetNames
        });
    }

    static string[] ExpandAssetNames(string[] assetNames)
    {
        HashSet<string> expandedPaths = new HashSet<string>();
        foreach (string assetName in assetNames)
        {
            if (AssetDatabase.IsValidFolder(assetName))
            {
                foreach (string guid in AssetDatabase.FindAssets("", new[] { assetName }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!AssetDatabase.IsValidFolder(path))
                    {
                        expandedPaths.Add(path);
                    }
                }
            }
            else
            {
                expandedPaths.Add(assetName);
            }
        }
        return expandedPaths.ToArray();
    }

    static bool IsNonSceneBundle(string bundleName)
    {
        string[] assetNames = AssetDatabase.GetAssetPathsFromAssetBundle(bundleName);
        if (assetNames == null || assetNames.Length == 0)
        {
            return false;
        }

        return !assetNames.Any(x => x.EndsWith(".unity", System.StringComparison.OrdinalIgnoreCase));
    }

    static bool IsLocalDlcBundle(string bundleName)
    {
        return !string.IsNullOrEmpty(bundleName) &&
            bundleName.EndsWith("/dlc_assets", StringComparison.OrdinalIgnoreCase);
    }

    static bool ValidateCurrentLevelSceneContents(Scene scene)
    {
        if (string.IsNullOrEmpty(scene.path) || !File.Exists(scene.path))
        {
            return true;
        }

        int lineCount = 0;
        int chefObjectCount = 0;
        int playerObjectCount = 0;
        bool hasVoiceChat = false;

        foreach (string line in File.ReadLines(scene.path))
        {
            lineCount++;
            string trimmed = line.Trim();
            if (trimmed.StartsWith("m_Name: Chef_", StringComparison.OrdinalIgnoreCase))
            {
                chefObjectCount++;
            }
            else if (trimmed.Equals("m_Name: player", StringComparison.OrdinalIgnoreCase) ||
                     trimmed.StartsWith("m_Name: Player_", StringComparison.OrdinalIgnoreCase))
            {
                playerObjectCount++;
            }
            else if (trimmed.Equals("m_Name: VoiceChat", StringComparison.OrdinalIgnoreCase))
            {
                hasVoiceChat = true;
            }
        }

        if (lineCount > 20000 && (chefObjectCount > 20 || playerObjectCount > 4 || hasVoiceChat))
        {
            string message =
                "This scene appears to contain imported Overcooked runtime objects instead of only DIY level objects.\n\n" +
                "Detected:\n" +
                "- Scene file lines: " + lineCount + "\n" +
                "- Chef objects: " + chefObjectCount + "\n" +
                "- Player objects: " + playerObjectCount + "\n" +
                "- VoiceChat HUD: " + (hasVoiceChat ? "yes" : "no") + "\n\n" +
                "Building this scene can duplicate and unload shared game resources, which may make the original game levels lose objects after this custom level is loaded.\n\n" +
                "Please rebuild the level from a clean DIY template and import only the level layout/pseudo-prefab data.";
            EditorUtility.DisplayDialog("Unsafe Level Scene", message, "OK");
            Debug.LogError(message);
            return false;
        }

        return true;
    }

    static void NormalizeCurrentLevelDlcReferences(string levelBundlePrefix)
    {
        LevelEditorStub.LevelInfoSO levelInfo = FindCurrentLevelInfoForScene();
        if (levelInfo == null)
        {
            return;
        }

        bool changed = false;
        changed |= NormalizePseudoPrefabs(levelInfo.recipes, true);
        changed |= NormalizePseudoPrefabs(levelInfo.dlcRecipeMatchListSOs, false);
        changed |= NormalizePseudoPrefabs(levelInfo.dlcCookingStepSOs, false);

        if (levelInfo.dependencies != null)
        {
            string localDependency = levelBundlePrefix + "/dlc_assets";
            int before = levelInfo.dependencies.Length;
            levelInfo.dependencies = levelInfo.dependencies
                .Where(x => !string.Equals(x, localDependency, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            changed |= levelInfo.dependencies.Length != before;
        }

        if (changed)
        {
            EditorUtility.SetDirty(levelInfo);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }

    static bool NormalizePseudoPrefabs(ScriptableObject[] assets, bool isRecipe)
    {
        bool changed = false;
        if (assets == null)
        {
            return false;
        }

        for (int i = 0; i < assets.Length; i++)
        {
            LevelEditorStub.PseudoPrefabSO asset = assets[i] as LevelEditorStub.PseudoPrefabSO;
            if (asset == null || !IsLocalDlcAsset(asset))
            {
                continue;
            }

            string resolvedPath = ResolveGameDlcAssetPath(asset, isRecipe);
            if (string.IsNullOrEmpty(resolvedPath))
            {
                Debug.LogWarning("Could not map local DLC asset to game reference: " + asset.prefabName);
                continue;
            }

            LevelEditorStub.PseudoPrefabSO replacement = AssetDatabase.LoadAssetAtPath<LevelEditorStub.PseudoPrefabSO>(resolvedPath);
            if (replacement == null || replacement == asset)
            {
                continue;
            }

            assets[i] = replacement;
            changed = true;
        }

        return changed;
    }

    static bool NormalizePseudoPrefabs(LevelEditorStub.PseudoPrefabSO[] assets, bool isRecipe)
    {
        bool changed = false;
        if (assets == null)
        {
            return false;
        }

        for (int i = 0; i < assets.Length; i++)
        {
            LevelEditorStub.PseudoPrefabSO asset = assets[i];
            if (asset == null || !IsLocalDlcAsset(asset))
            {
                continue;
            }

            string resolvedPath = ResolveGameDlcAssetPath(asset, isRecipe);
            if (string.IsNullOrEmpty(resolvedPath))
            {
                Debug.LogWarning("Could not map local DLC asset to game reference: " + asset.prefabName);
                continue;
            }

            LevelEditorStub.PseudoPrefabSO replacement = AssetDatabase.LoadAssetAtPath<LevelEditorStub.PseudoPrefabSO>(resolvedPath);
            if (replacement == null || replacement == asset)
            {
                continue;
            }

            assets[i] = replacement;
            changed = true;
        }

        return changed;
    }

    static bool IsLocalDlcAsset(LevelEditorStub.PseudoPrefabSO asset)
    {
        return asset != null &&
            !string.IsNullOrEmpty(asset.bundleName) &&
            asset.bundleName.EndsWith("/dlc_assets", StringComparison.OrdinalIgnoreCase);
    }

    static string ResolveGameDlcAssetPath(LevelEditorStub.PseudoPrefabSO source, bool isRecipe)
    {
        string prefabName = source != null ? source.prefabName : null;
        if (string.IsNullOrEmpty(prefabName))
        {
            return null;
        }

        string[] matches = AssetDatabase.FindAssets(prefabName, new[] { "Assets/dlc" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => path.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            .Where(path => isRecipe || !path.Contains("/Recipes/"))
            .OrderBy(path => IsPreferredDlcReferencePath(path, source.assetPath, isRecipe) ? 0 : 1)
            .ToArray();

        foreach (string path in matches)
        {
            LevelEditorStub.PseudoPrefabSO prefab = AssetDatabase.LoadAssetAtPath<LevelEditorStub.PseudoPrefabSO>(path);
            if (prefab != null && string.Equals(prefab.prefabName, prefabName, StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }
        }

        return null;
    }

    static bool IsPreferredDlcReferencePath(string candidatePath, string sourcePath, bool isRecipe)
    {
        if (isRecipe)
        {
            return candidatePath.Contains("/Recipes/");
        }

        if (!string.IsNullOrEmpty(sourcePath) && sourcePath.ToLowerInvariant().Contains("cookingstep"))
        {
            return candidatePath.Contains("/CookingSteps/");
        }

        if (!string.IsNullOrEmpty(sourcePath) && sourcePath.ToLowerInvariant().Contains("recipematchlist"))
        {
            return candidatePath.Contains("/RecipeMatchLists/");
        }

        return candidatePath.Contains("/RecipeMatchLists/") || candidatePath.Contains("/CookingSteps/");
    }

    static LevelEditorStub.LevelInfoSO FindCurrentLevelInfoForScene()
    {
        Scene activeScene = EditorSceneManager.GetActiveScene();
        string activeSceneName = Path.GetFileNameWithoutExtension(activeScene.path);
        if (string.IsNullOrEmpty(activeSceneName))
        {
            return null;
        }

        string[] candidates = AssetDatabase.FindAssets("t:LevelInfoSO", new[] { "Assets/LevelSets" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .ToArray();

        foreach (string path in candidates)
        {
            LevelEditorStub.LevelInfoSO levelInfo = AssetDatabase.LoadAssetAtPath<LevelEditorStub.LevelInfoSO>(path);
            if (levelInfo != null && string.Equals(levelInfo.sceneName, activeSceneName, StringComparison.OrdinalIgnoreCase))
            {
                return levelInfo;
            }
        }

        return null;
    }

    static string InstallBuiltLevel(string levelBundlePrefix, IEnumerable<string> builtBundleNames)
    {
        string sourceDirectory = Path.GetFullPath(Path.Combine("Assets/AssetBundles", levelBundlePrefix));
        if (!Directory.Exists(sourceDirectory))
        {
            return "Auto install skipped: build output folder was not found:\n" + sourceDirectory;
        }

        string gameDirectory = FindGameDirectory();
        if (string.IsNullOrEmpty(gameDirectory))
        {
            return "Auto install skipped: Overcooked! 2 was not found. Copy the folder manually to BepInEx/plugins/OC2DIYLevel/levels.";
        }

        string targetDirectory = Path.Combine(Path.Combine(gameDirectory, LevelsRelativePath), levelBundlePrefix);
        HashSet<string> installFileNames = GetInstallFileNames(levelBundlePrefix, builtBundleNames);
        bool restartedGame = false;
        try
        {
            ReplaceDirectory(sourceDirectory, targetDirectory, installFileNames);
        }
        catch (Exception firstError)
        {
            bool closedGame = CloseRunningGame();
            restartedGame = closedGame;
            if (!closedGame)
            {
                return "Auto install failed:\n" + firstError.Message;
            }

            try
            {
                ReplaceDirectory(sourceDirectory, targetDirectory, installFileNames);
            }
            catch (Exception secondError)
            {
                return "Auto install failed after closing the game:\n" + secondError.Message;
            }
        }

        string message = "Installed to:\n" + targetDirectory;
        if (restartedGame)
        {
            message += "\n\nThe running game was closed because the old level files were locked.";
            message += RestartGame(gameDirectory);
        }
        return message;
    }

    static HashSet<string> GetInstallFileNames(string levelBundlePrefix, IEnumerable<string> builtBundleNames)
    {
        HashSet<string> fileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string bundleName in builtBundleNames)
        {
            if (!bundleName.StartsWith(levelBundlePrefix + "/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            fileNames.Add(bundleName.Substring(levelBundlePrefix.Length + 1));
        }
        return fileNames;
    }

    static void ReplaceDirectory(string sourceDirectory, string targetDirectory, HashSet<string> installFileNames)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetDirectory));
        if (Directory.Exists(targetDirectory))
        {
            Directory.Delete(targetDirectory, true);
        }
        CopyDirectory(sourceDirectory, targetDirectory, installFileNames);
    }

    static void CopyDirectory(string sourceDirectory, string targetDirectory, HashSet<string> installFileNames)
    {
        Directory.CreateDirectory(targetDirectory);
        foreach (string directory in Directory.GetDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string relativeDirectory = directory.Substring(sourceDirectory.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            Directory.CreateDirectory(Path.Combine(targetDirectory, relativeDirectory));
        }

        foreach (string file in Directory.GetFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            string relativeFile = file.Substring(sourceDirectory.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (ShouldSkipInstalledFile(relativeFile, installFileNames))
            {
                continue;
            }

            string targetFile = Path.Combine(targetDirectory, relativeFile);
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile));
            File.Copy(file, targetFile, true);
        }
    }

    static bool ShouldSkipInstalledFile(string relativeFilePath, HashSet<string> installFileNames)
    {
        string extension = Path.GetExtension(relativeFilePath);
        if (string.Equals(extension, ".manifest", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".meta", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        string normalizedFilePath = relativeFilePath.Replace("\\", "/");
        return !installFileNames.Contains(normalizedFilePath);
    }

    static bool CloseRunningGame()
    {
        bool closedAny = false;
        foreach (System.Diagnostics.Process process in System.Diagnostics.Process.GetProcessesByName(GameProcessName))
        {
            closedAny = true;
            try
            {
                if (!process.HasExited && process.CloseMainWindow())
                {
                    process.WaitForExit(10000);
                }
                if (!process.HasExited)
                {
                    process.Kill();
                    process.WaitForExit(10000);
                }
            }
            catch (Exception error)
            {
                Debug.LogWarning("Failed to close " + GameProcessName + ": " + error.Message);
            }
        }
        return closedAny;
    }

    static string RestartGame(string gameDirectory)
    {
        string exePath = Path.Combine(gameDirectory, GameExeName);
        if (!File.Exists(exePath))
        {
            return "\nGame restart skipped: executable was not found:\n" + exePath;
        }

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = gameDirectory,
            });
            return "\nGame restarted.";
        }
        catch (Exception error)
        {
            return "\nGame restart failed:\n" + error.Message;
        }
    }

    static string FindGameDirectory()
    {
        foreach (string steamRoot in FindSteamRoots())
        {
            string candidate = Path.Combine(steamRoot, "steamapps/common/" + GameFolderName);
            if (File.Exists(Path.Combine(candidate, GameExeName)))
            {
                return candidate;
            }

            string libraryFolders = Path.Combine(steamRoot, "steamapps/libraryfolders.vdf");
            if (File.Exists(libraryFolders))
            {
                string text = File.ReadAllText(libraryFolders);
                foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(text, "\\\"path\\\"\\s+\\\"([^\\\"]+)\\\""))
                {
                    candidate = Path.Combine(match.Groups[1].Value.Replace("\\\\", "\\"), "steamapps/common/" + GameFolderName);
                    if (File.Exists(Path.Combine(candidate, GameExeName)))
                    {
                        return candidate;
                    }
                }
            }
        }

        foreach (string drive in Directory.GetLogicalDrives())
        {
            string candidate = Path.Combine(drive, "SteamLibrary/steamapps/common/" + GameFolderName);
            if (File.Exists(Path.Combine(candidate, GameExeName)))
            {
                return candidate;
            }
        }
        return null;
    }

    static IEnumerable<string> FindSteamRoots()
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
            }
        }

        roots.Add(@"C:\Program Files (x86)\Steam");
        roots.Add(@"C:\Program Files\Steam");
        roots.Add(@"D:\SteamLibrary");
        roots.Add(@"E:\SteamLibrary");
        roots.Add(@"F:\SteamLibrary");
        return roots;
    }
    [MenuItem("Tools/Reload Pseudo Assets", false, 10)]
    static void ReloadPseudoAssets()
    {
        PseudoPrefabManager.Instance.DeInit();
        PseudoPrefabManager.Instance.Init();
    }

    [MenuItem("Tools/Toggle Prepare For Building", false, 11)]
    static void TogglePrepareForBuilding()
    {
        PseudoPrefabManager.Instance.prepareForBuilding = !PseudoPrefabManager.Instance.prepareForBuilding;
        if (PseudoPrefabManager.Instance.prepareForBuilding)
        {
            PseudoPrefabManager.Instance.DeInit();
        }
        else
        {
            PseudoPrefabManager.Instance.Init();
        }
    }
}
