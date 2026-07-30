using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using LevelEditorStub;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class OC2DlcAssetTools
{
    private const string DefaultAssetRipperProject = @"F:\ModTools\AssetRipper\Exports\OC2_Export\ExportedProject";
    private static readonly Regex GuidRegex = new Regex(@"guid:\s*([0-9a-fA-F]{32})", RegexOptions.Compiled);

    private static readonly string[] Dlc09RecipeNames =
    {
        "DLC09_BeefPotatoCarrotBroccoliRoast",
        "DLC09_BeefPotatoCarrotRoast",
        "DLC09_ChickenPotatoCarrotBroccoliRoast",
        "DLC09_ChickenPotatoCarrotRoast",
        "DLC09_ChristmasPudding",
        "DLC09_ChristmasPuddingWithOrange",
        "DLC09_HotChocolate",
        "DLC09_HotChocolateCream",
        "DLC09_HotChocolateMallow",
        "DLC09_HotChocolateMallowCream",
        "DLC09_Pancake_Chocolate",
        "DLC09_Pancake_Plain",
        "DLC09_Pancake_Strawberry"
    };

    [MenuItem("Tools/OC2 DLC/Import DLC09 Assets For Current Level", false, 0)]
    public static void ImportDlc09AssetsForCurrentLevel()
    {
        string exportAssetsRoot = Path.Combine(DefaultAssetRipperProject, "Assets").Replace("\\", "/");
        if (!Directory.Exists(exportAssetsRoot))
        {
            EditorUtility.DisplayDialog(
                "Import DLC09 Assets",
                "AssetRipper exported Assets folder was not found:\n" + exportAssetsRoot,
                "OK");
            return;
        }

        string levelPrefix = GetCurrentLevelBundlePrefix();
        if (string.IsNullOrEmpty(levelPrefix))
        {
            return;
        }

        string targetRoot = "Assets/LevelSets/" + levelPrefix + "/dlc_assets_local";
        EnsureFolder("Assets/LevelSets/" + levelPrefix, "dlc_assets_local");

        Dictionary<string, string> exportGuidMap = BuildGuidMap(exportAssetsRoot);
        Dictionary<string, string> projectGuidMap = BuildGuidMap(Application.dataPath.Replace("\\", "/"));
        HashSet<string> copiedAssetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        List<string> seedAssetPaths = new List<string>();
        seedAssetPaths.Add("Assets/MonoBehaviour/TheRecipeMatchList.asset");
        seedAssetPaths.Add("Assets/MonoBehaviour/DLC09_RoastingTray.asset");
        foreach (string recipeName in Dlc09RecipeNames)
        {
            seedAssetPaths.Add("Assets/MonoBehaviour/" + recipeName + ".asset");
        }

        foreach (string seedAssetPath in seedAssetPaths)
        {
            CopyAssetAndDependencies(
                exportAssetsRoot,
                seedAssetPath,
                targetRoot,
                exportGuidMap,
                projectGuidMap,
                copiedAssetPaths);
        }

        AssetDatabase.Refresh();
        string dlcBundleName = levelPrefix + "/dlc_assets";
        MarkFolderAssetsAsBundle(targetRoot, dlcBundleName);
        CreateDlc09PseudoPrefabReferences(targetRoot, dlcBundleName);
        AddDependencyToCurrentLevelInfo(levelPrefix + "/dlc_assets");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "Import DLC09 Assets",
            "Imported DLC09 local assets for " + levelPrefix + ".\n\n" +
            "Bundle: " + dlcBundleName + "\n" +
            "Copied/updated assets: " + copiedAssetPaths.Count + "\n\n" +
            "Use the PseudoPrefabSORecipe files under:\n" +
            targetRoot + "/References/Recipes",
            "OK");
    }

    private static string GetCurrentLevelBundlePrefix()
    {
        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(activeScene.path))
        {
            EditorUtility.DisplayDialog("Import DLC Assets", "Please save and open the target level scene first.", "OK");
            return null;
        }

        AssetImporter sceneImporter = AssetImporter.GetAtPath(activeScene.path);
        if (sceneImporter == null || string.IsNullOrEmpty(sceneImporter.assetBundleName))
        {
            EditorUtility.DisplayDialog(
                "Import DLC Assets",
                "The current scene has no AssetBundle name. It should look like level_name/scene_name.",
                "OK");
            return null;
        }

        int slashIndex = sceneImporter.assetBundleName.LastIndexOf('/');
        if (slashIndex <= 0)
        {
            EditorUtility.DisplayDialog(
                "Import DLC Assets",
                "The current scene AssetBundle name should look like level_name/scene_name.",
                "OK");
            return null;
        }

        return sceneImporter.assetBundleName.Substring(0, slashIndex);
    }

    private static Dictionary<string, string> BuildGuidMap(string root)
    {
        Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string metaPath in Directory.GetFiles(root, "*.meta", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(metaPath);
            Match match = GuidRegex.Match(text);
            if (!match.Success)
            {
                continue;
            }

            string assetPath = metaPath.Substring(0, metaPath.Length - ".meta".Length).Replace("\\", "/");
            if (!File.Exists(assetPath) && !Directory.Exists(assetPath))
            {
                continue;
            }

            string unityPath = ToUnityAssetPath(assetPath);
            if (!string.IsNullOrEmpty(unityPath) && !result.ContainsKey(match.Groups[1].Value))
            {
                result.Add(match.Groups[1].Value, unityPath);
            }
        }
        return result;
    }

    private static string ToUnityAssetPath(string path)
    {
        path = path.Replace("\\", "/");
        int index = path.IndexOf("/Assets/", StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            return path.Substring(index + 1);
        }
        if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }
        return null;
    }

    private static void CopyAssetAndDependencies(
        string exportAssetsRoot,
        string sourceAssetPath,
        string targetRoot,
        Dictionary<string, string> exportGuidMap,
        Dictionary<string, string> projectGuidMap,
        HashSet<string> copiedAssetPaths)
    {
        Queue<string> pending = new Queue<string>();
        HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        pending.Enqueue(sourceAssetPath);

        while (pending.Count > 0)
        {
            string current = pending.Dequeue().Replace("\\", "/");
            if (!visited.Add(current))
            {
                continue;
            }
            if (ShouldSkipCopiedAsset(current))
            {
                continue;
            }

            string sourceFullPath = Path.Combine(Directory.GetParent(exportAssetsRoot).FullName, current).Replace("\\", "/");
            if (!File.Exists(sourceFullPath))
            {
                continue;
            }

            string copiedPath = CopyAssetFile(sourceFullPath, targetRoot);
            copiedAssetPaths.Add(copiedPath);

            string text;
            if (!TryReadText(sourceFullPath, out text))
            {
                continue;
            }

            foreach (Match match in GuidRegex.Matches(text))
            {
                string guid = match.Groups[1].Value;
                string projectAssetPath;
                if (projectGuidMap.TryGetValue(guid, out projectAssetPath) &&
                    !projectAssetPath.StartsWith(targetRoot + "/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string exportAssetPath;
                if (exportGuidMap.TryGetValue(guid, out exportAssetPath) && !ShouldSkipCopiedAsset(exportAssetPath))
                {
                    pending.Enqueue(exportAssetPath);
                }
            }
        }
    }

    private static bool ShouldSkipCopiedAsset(string assetPath)
    {
        string path = assetPath.Replace("\\", "/").ToLowerInvariant();
        return path.StartsWith("assets/scripts/") ||
               path.StartsWith("assets/plugins/") ||
               path.StartsWith("assets/editor/") ||
               path.StartsWith("assets/scenes/") ||
               path.EndsWith(".cs") ||
               path.EndsWith(".dll");
    }

    private static string CopyAssetFile(string sourceFullPath, string targetRoot)
    {
        string sourceAssetPath = ToUnityAssetPath(sourceFullPath);
        string relative = sourceAssetPath.Substring("Assets/".Length);
        string targetAssetPath = targetRoot + "/Imported/" + relative;
        string targetFullPath = Path.GetFullPath(targetAssetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(targetFullPath));

        File.Copy(sourceFullPath, targetFullPath, true);
        string sourceMetaPath = sourceFullPath + ".meta";
        if (File.Exists(sourceMetaPath))
        {
            File.Copy(sourceMetaPath, targetFullPath + ".meta", true);
        }

        return targetAssetPath.Replace("\\", "/");
    }

    private static bool TryReadText(string path, out string text)
    {
        text = null;
        string extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension != ".asset" && extension != ".prefab" && extension != ".mat" &&
            extension != ".controller" && extension != ".anim" && extension != ".rendertexture")
        {
            return false;
        }

        try
        {
            text = File.ReadAllText(path);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void MarkFolderAssetsAsBundle(string folderPath, string bundleName)
    {
        foreach (string guid in AssetDatabase.FindAssets("", new[] { folderPath }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetDatabase.IsValidFolder(path))
            {
                continue;
            }

            AssetImporter importer = AssetImporter.GetAtPath(path);
            if (importer != null)
            {
                importer.assetBundleName = bundleName;
            }
        }
    }

    private static void CreateDlc09PseudoPrefabReferences(string targetRoot, string bundleName)
    {
        string referencesRoot = targetRoot + "/References";
        EnsureFolder(targetRoot, "References");
        EnsureFolder(referencesRoot, "Recipes");
        EnsureFolder(referencesRoot, "RecipeMatchLists");
        EnsureFolder(referencesRoot, "CookingSteps");

        CreatePseudoPrefab(
            referencesRoot + "/RecipeMatchLists/dlc09_recipematchlist.asset",
            "dlc09_recipematchlist",
            bundleName,
            targetRoot + "/Imported/MonoBehaviour/TheRecipeMatchList.asset",
            false,
            0);

        CreatePseudoPrefab(
            referencesRoot + "/CookingSteps/dlc09_roastingtray.asset",
            "dlc09_roastingtray",
            bundleName,
            targetRoot + "/Imported/MonoBehaviour/DLC09_RoastingTray.asset",
            false,
            0);

        foreach (string recipeName in Dlc09RecipeNames)
        {
            CreatePseudoPrefab(
                referencesRoot + "/Recipes/" + ToLowerAssetName(recipeName) + ".asset",
                ToLowerAssetName(recipeName),
                bundleName,
                targetRoot + "/Imported/MonoBehaviour/" + recipeName + ".asset",
                true,
                GuessScore(recipeName));
        }

        MarkFolderAssetsAsBundle(referencesRoot, bundleName);
    }

    private static void CreatePseudoPrefab(
        string path,
        string name,
        string bundleName,
        string assetPath,
        bool recipe,
        int score)
    {
        PseudoPrefabSO pseudoPrefab = AssetDatabase.LoadAssetAtPath<PseudoPrefabSO>(path);
        if (pseudoPrefab == null)
        {
            pseudoPrefab = recipe
                ? ScriptableObject.CreateInstance<PseudoPrefabSORecipe>()
                : ScriptableObject.CreateInstance<PseudoPrefabSO>();
            AssetDatabase.CreateAsset(pseudoPrefab, path);
        }

        pseudoPrefab.name = name;
        pseudoPrefab.prefabName = name;
        pseudoPrefab.bundleName = bundleName;
        pseudoPrefab.assetPath = assetPath;

        PseudoPrefabSORecipe recipeRef = pseudoPrefab as PseudoPrefabSORecipe;
        if (recipeRef != null)
        {
            recipeRef.score = score;
        }

        EditorUtility.SetDirty(pseudoPrefab);
    }

    private static string ToLowerAssetName(string name)
    {
        return name.ToLowerInvariant();
    }

    private static int GuessScore(string recipeName)
    {
        string name = recipeName.ToLowerInvariant();
        if (name.Contains("broccoli") || name.Contains("mallowcream"))
        {
            return 80;
        }
        if (name.Contains("cream") || name.Contains("orange") || name.Contains("strawberry") ||
            name.Contains("chocolate") || name.Contains("carrot"))
        {
            return 60;
        }
        return 40;
    }

    private static void AddDependencyToCurrentLevelInfo(string dependency)
    {
        string[] candidates = AssetDatabase.FindAssets("t:LevelInfoSO", new[] { "Assets/LevelSets" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .ToArray();

        foreach (string path in candidates)
        {
            LevelInfoSO levelInfo = AssetDatabase.LoadAssetAtPath<LevelInfoSO>(path);
            if (levelInfo == null)
            {
                continue;
            }

            string scenePath = SceneManager.GetActiveScene().path;
            string activeSceneName = Path.GetFileNameWithoutExtension(scenePath);
            if (!string.Equals(levelInfo.sceneName, activeSceneName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            List<string> dependencies = levelInfo.dependencies != null
                ? levelInfo.dependencies.ToList()
                : new List<string>();
            if (!dependencies.Contains(dependency))
            {
                dependencies.Add(dependency);
                levelInfo.dependencies = dependencies.ToArray();
                EditorUtility.SetDirty(levelInfo);
            }
        }
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }
}
