using LevelEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;


public static class CreateAssetBundles
{
    [MenuItem("Tools/Build AssetBundles", false, 100)]
    static void BuildAllAssetBundles()
    {
        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (!TargetSceneSaveValidator.CheckPrepareForBuilding(activeScene))
            return;

        string assetBundleDirectory = "Assets/AssetBundles";
        if (!Directory.Exists(assetBundleDirectory))
        {
            Directory.CreateDirectory(assetBundleDirectory);
        }
        BuildPipeline.BuildAssetBundles(assetBundleDirectory,
                                        BuildAssetBundleOptions.None,
                                        BuildTarget.StandaloneWindows);
    }

    [MenuItem("Tools/Build AssetBundles (ForceRebuild)", false, 101)]
    static void BuildAllAssetBundlesForceRebuild()
    {
        Scene activeScene = EditorSceneManager.GetActiveScene();
        if (!TargetSceneSaveValidator.CheckPrepareForBuilding(activeScene))
            return;

        string assetBundleDirectory = "Assets/AssetBundles";
        if (!Directory.Exists(assetBundleDirectory))
        {
            Directory.CreateDirectory(assetBundleDirectory);
        }
        BuildPipeline.BuildAssetBundles(assetBundleDirectory,
                                        BuildAssetBundleOptions.ForceRebuildAssetBundle,
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
        List<AssetBundleBuild> builds = new List<AssetBundleBuild>();
        AddBundleBuild(builds, sceneBundleName);

        foreach (string bundleName in AssetDatabase.GetAllAssetBundleNames())
        {
            if (bundleName.StartsWith(levelBundlePrefix + "/info_", System.StringComparison.OrdinalIgnoreCase))
            {
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

        BuildPipeline.BuildAssetBundles(
            assetBundleDirectory,
            builds.ToArray(),
            options,
            BuildTarget.StandaloneWindows);

        EditorUtility.DisplayDialog(
            "Build Current Level",
            "Built " + builds.Count + " bundle(s) for " + levelBundlePrefix + " into Assets/AssetBundles/" + levelBundlePrefix + ".",
            "OK");
    }

    static void AddBundleBuild(List<AssetBundleBuild> builds, string bundleName)
    {
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
