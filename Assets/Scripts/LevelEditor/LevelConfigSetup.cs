using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using LevelEditorStub;


namespace LevelEditor
{
    public static class LevelConfigSetup {
        private struct AutoDlcRef
        {
            public string Name;
            public string BundleName;
            public string AssetPath;

            public AutoDlcRef(string name, string bundleName, string assetPath)
            {
                Name = name;
                BundleName = bundleName;
                AssetPath = assetPath;
            }
        }

        private static readonly AutoDlcRef[] AutoDlcRecipeMatchListRefs =
        {
            new AutoDlcRef("dlc02_recipematchlist", "bundle162", "assets/downloadablecontent/dlc02/dlc_assets/data/recipes/dlc02_recipematchlist.asset"),
            new AutoDlcRef("dlc03_recipematchlist", "bundle208", "assets/downloadablecontent/dlc03/dlc_assets/data/recipes/dlc03_recipematchlist.asset"),
            new AutoDlcRef("dlc04_recipematchlist", "bundle225", "assets/downloadablecontent/dlc04/dlc_assets/data/recipes/dlc04_recipematchlist.asset"),
            new AutoDlcRef("dlc05_recipematchlist", "bundle247", "assets/downloadablecontent/dlc05/dlc_assets/data/recipes/dlc05_recipematchlist.asset"),
            new AutoDlcRef("dlc07_recipematchlist", "bundle293", "assets/downloadablecontent/dlc07/dlc_assets/data/recipes/dlc07_recipematchlist.asset"),
            new AutoDlcRef("dlc08_recipematchlist", "bundle354", "assets/downloadablecontent/dlc08/dlc_assets/data/recipes/dlc08_recipematchlist.asset"),
            new AutoDlcRef("dlc09_recipematchlist", "bundle404", "assets/downloadablecontent/dlc09/dlc_assets/data/recipes/dlc09_recipematchlist.asset"),
            new AutoDlcRef("dlc10_recipematchlist", "bundle419", "assets/downloadablecontent/dlc10/dlc_assets/data/recipes/dlc10_recipematchlist.asset"),
            new AutoDlcRef("dlc11_recipematchlist", "bundle427", "assets/downloadablecontent/dlc11/dlc_assets/data/recipes/dlc11_recipematchlist.asset"),
            new AutoDlcRef("dlc13_recipematchlist", "bundle448", "assets/downloadablecontent/dlc13/assets/data/recipes/dlc13_recipematchlist.asset"),
        };

        private static readonly AutoDlcRef[] AutoDlcCookingStepRefs =
        {
            new AutoDlcRef("dlc02_blender", "bundle162", "assets/downloadablecontent/dlc02/dlc_assets/data/orderdefinitions/cookingstepdata/blender.asset"),
            new AutoDlcRef("dlc04_hotpot", "bundle225", "assets/downloadablecontent/dlc04/dlc_assets/data/recipes/cookingstepdata/hotpot.asset"),
            new AutoDlcRef("dlc05_griddlepan", "bundle247", "assets/downloadablecontent/dlc05/dlc_assets/data/recipes/cookingstepdata/griddlepan.asset"),
            new AutoDlcRef("dlc05_toastingfork", "bundle247", "assets/downloadablecontent/dlc05/dlc_assets/data/recipes/cookingstepdata/toastingfork.asset"),
            new AutoDlcRef("dlc07_roastingtray", "bundle293", "assets/downloadablecontent/dlc07/dlc_assets/data/recipes/cookingstepdata/roastingtray.asset"),
            new AutoDlcRef("dlc09_roastingtray", "bundle404", "assets/downloadablecontent/dlc09/dlc_assets/data/recipes/cookingstepdata/dlc09_roastingtray.asset"),
            new AutoDlcRef("dlc10_hotpot", "bundle419", "assets/downloadablecontent/dlc10/dlc_assets/data/recipes/cookingstepdata/dlc10_hotpot.asset"),
        };

        private static PseudoPrefabSO[] s_autoDlcRecipeMatchLists;
        private static PseudoPrefabSO[] s_autoDlcCookingSteps;

        public static CampaignLevelConfig SetupConfig(PseudoPrefabSO configTemplateSO, LevelInfoSO config, int playerCount)
        {
            CampaignLevelConfig configTemplate = PseudoPrefabManager.LoadAsset<CampaignLevelConfig>(configTemplateSO);
            configTemplate = ScriptableObject.Instantiate(configTemplate);

            LevelConfigSetupPerPlayerCountSO configPerPlayerCount = GetConfigPerPlayerCount(config, playerCount);
            configTemplate.name = string.Format("{0}_{1}p", config.name, playerCount.ToString());
            configTemplate.m_orderLifetime = configPerPlayerCount.orderLifeTime;
            configTemplate.m_timeBetweenOrders = configPerPlayerCount.timeBetweenOrders;
            configTemplate.m_plateReturnTime = configPerPlayerCount.plateReturnTime;
            configTemplate.m_survivalConfig.m_timeMultiplier = configPerPlayerCount.survivalTimeMultiplier;
            configTemplate.m_objectives = new LevelObjectiveBase[0];
            configTemplate.m_disableDynamicParenting = config.disableDynamicParenting;
            configTemplate.m_rounds[0].m_roundTimer = configPerPlayerCount.roundTime;

            RecipeList recipeList = configTemplate.m_rounds[0].m_recipes;
            recipeList = ScriptableObject.Instantiate(recipeList);
            recipeList.name = config.name;
            int takeNum = config.debugRecipeCount == 0 ? config.recipes.Length : config.debugRecipeCount;
            recipeList.m_recipes = config.recipes
                .Take(takeNum)
                .Select(x => RecipeHelper.GetRecipe(x))
                .ToArray();
            configTemplate.m_rounds[0].m_recipes = recipeList;

            bool hasCustomRecipes = config.recipes != null && config.recipes.Any(x => x is CustomRecipeSO);
            bool hasOptionalRecipes = config.optionalRecipeMatchListItems != null &&
                config.optionalRecipeMatchListItems.Length > 0;
            PseudoPrefabSO[] dlcRecipeMatchListSOs = MergeDlcRefs(config.dlcRecipeMatchListSOs, GetAutoDlcRecipeMatchLists());
            PseudoPrefabSO[] dlcCookingStepSOs = MergeDlcRefs(config.dlcCookingStepSOs, GetAutoDlcCookingSteps());
            bool hasDlcRecipeMatchLists = dlcRecipeMatchListSOs.Length > 0;
            bool hasDlcCookingSteps = dlcCookingStepSOs.Length > 0;
            if (hasCustomRecipes || hasOptionalRecipes || hasDlcRecipeMatchLists || hasDlcCookingSteps)
            {
                RecipeMatchList theRecipeMatchList = configTemplate.m_recipeMatchingList;
                RecipeMatchList newRecipeMatchList = ScriptableObject.CreateInstance<RecipeMatchList>();
                newRecipeMatchList.name = "RecipeMatchList_" + config.name;
                List<RecipeMatchList> includedRecipeMatchLists = new List<RecipeMatchList>
                {
                    theRecipeMatchList
                };
                if (hasDlcRecipeMatchLists)
                {
                    foreach (PseudoPrefabSO recipeMatchListSO in dlcRecipeMatchListSOs)
                    {
                        if (recipeMatchListSO == null) continue;
                        RecipeMatchList recipeMatchList = PseudoPrefabManager.LoadAsset<RecipeMatchList>(recipeMatchListSO);
                        if (recipeMatchList != null)
                        {
                            includedRecipeMatchLists.Add(recipeMatchList);
                        }
                    }
                }
                newRecipeMatchList.m_includeLists = includedRecipeMatchLists.ToArray();
                List<CookingStepData> cookingSteps = new List<CookingStepData>();
                if (hasDlcCookingSteps)
                {
                    foreach (PseudoPrefabSO cookingStepSO in dlcCookingStepSOs)
                    {
                        if (cookingStepSO == null) continue;
                        CookingStepData cookingStep = PseudoPrefabManager.LoadAsset<CookingStepData>(cookingStepSO);
                        if (cookingStep != null)
                        {
                            cookingSteps.Add(cookingStep);
                        }
                    }
                }
                newRecipeMatchList.m_cookingSteps = cookingSteps.ToArray();

                List<OrderDefinitionNode> newRecipes = new List<OrderDefinitionNode>();
                if (hasOptionalRecipes)
                {
                    newRecipes = config.optionalRecipeMatchListItems
                        .Select(x => RecipeHelper.GetOptionalRecipeNode(x))
                        .ToList();
                }
                if (hasCustomRecipes)
                {
                    for (int i = 0; i < recipeList.m_recipes.Length; i++)
                    {
                        if (!(config.recipes[i] is CustomRecipeSO)) continue;
                        CustomRecipeSO customRecipeSO = (CustomRecipeSO)config.recipes[i];
                        newRecipes.Add(recipeList.m_recipes[i].m_order);
                        if (config.optionalRecipeMatchListItems == null || customRecipeSO.modelSO == null) continue;
                        for (int j = 0; j < config.optionalRecipeMatchListItems.Length; j++)
                        {
                            if (customRecipeSO.modelSO == config.optionalRecipeMatchListItems[j].modelSO)
                            {
                                recipeList.m_recipes[i].m_order.m_platingPrefab = newRecipes[j].m_platingPrefab;
                                break;
                            }
                        }
                    }
                }
                newRecipeMatchList.m_recipes = newRecipes.ToArray();

                configTemplate.m_recipeMatchingList = newRecipeMatchList;
            }

            return configTemplate;
        }

        private static LevelConfigSetupPerPlayerCountSO GetConfigPerPlayerCount(LevelInfoSO config, int playerCount)
        {
            return new LevelConfigSetupPerPlayerCountSO[]
            {
                config.config_1p, config.config_2p, config.config_3p, config.config_4p
            }[playerCount - 1];
        }

        public static SceneDirectoryData SetupSceneDirectoryData(PseudoPrefabSO configTemplateSO, LevelInfoSO config)
        {
            return SetupSceneDirectoryData(configTemplateSO, new LevelInfoSO[] { config });
        }

        public static SceneDirectoryData SetupSceneDirectoryData(PseudoPrefabSO configTemplateSO, LevelSetInfoSO levelSetInfo)
        {
            return SetupSceneDirectoryData(configTemplateSO, levelSetInfo.levelInfos);
        }

        private static SceneDirectoryData SetupSceneDirectoryData(PseudoPrefabSO configTemplateSO, LevelInfoSO[] levelInfos)
        {
            SceneDirectoryData diyLevelSceneDirectoryData = ScriptableObject.CreateInstance<SceneDirectoryData>();
            diyLevelSceneDirectoryData.name = "DIYLevelSceneDirectory";
            List<SceneDirectoryData.SceneDirectoryEntry> entries = new List<SceneDirectoryData.SceneDirectoryEntry>();
            foreach (LevelInfoSO levelInfo in levelInfos)
            {
                SceneDirectoryData.SceneDirectoryEntry entry = new SceneDirectoryData.SceneDirectoryEntry();
                entry.Label = string.Format("\"{0}\"", Localization.GetLanguage() == SupportedLanguages.Chinese ? levelInfo.levelNameZH : levelInfo.levelName);
                entry.LoadScreenOverride = levelInfo.screenshot;
                entry.AvailableInLobby = false;
                entry.SceneVarients = new SceneDirectoryData.PerPlayerCountDirectoryEntry[4];
                for (int i = 0; i < 4; i++)
                {
                    var sceneVarients = new SceneDirectoryData.PerPlayerCountDirectoryEntry();
                    sceneVarients.PlayerCount = i + 1;
                    sceneVarients.LevelConfig = SetupConfig(configTemplateSO, levelInfo, i + 1);
                    sceneVarients.SceneName = levelInfo.sceneName;
                    //sceneVarients.SceneName = string.Format("DIYLevel/{0}/{1}", levelSetInfo.levelSetName, levelInfo.sceneName);
                    sceneVarients.Screenshot = levelInfo.screenshot;
                    LevelConfigSetupPerPlayerCountSO configSetupPerPlayerCount = GetConfigPerPlayerCount(levelInfo, i + 1);
                    FieldInfo pcStarBoundariesField = sceneVarients.GetType()
                        .GetField("m_PCStarBoundaries", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    SceneDirectoryData.StarBoundaries starBoundaries = new SceneDirectoryData.StarBoundaries
                        {
                            m_OneStarScore = configSetupPerPlayerCount.m_OneStarScore,
                            m_TwoStarScore = configSetupPerPlayerCount.m_TwoStarScore,
                            m_ThreeStarScore = configSetupPerPlayerCount.m_ThreeStarScore,
                            m_FourStarScore = configSetupPerPlayerCount.m_FourStarScore,
                        };
                    if (pcStarBoundariesField != null)
                    {
                        pcStarBoundariesField.SetValue(sceneVarients, starBoundaries);
                    }
                    else
                    {
                        sceneVarients.m_PCStarBoundaries = starBoundaries;
                    }
                    entry.SceneVarients[i] = sceneVarients;
                }
                entries.Add(entry);
            }
            diyLevelSceneDirectoryData.Scenes = entries.ToArray();
            return diyLevelSceneDirectoryData;
        }

        private static PseudoPrefabSO[] MergeDlcRefs(PseudoPrefabSO[] explicitRefs, PseudoPrefabSO[] autoRefs)
        {
            List<PseudoPrefabSO> merged = new List<PseudoPrefabSO>();
            HashSet<string> seen = new HashSet<string>();
            AddDlcRefs(merged, seen, explicitRefs);
            AddDlcRefs(merged, seen, autoRefs);
            return merged.ToArray();
        }

        private static void AddDlcRefs(List<PseudoPrefabSO> merged, HashSet<string> seen, PseudoPrefabSO[] refs)
        {
            if (refs == null)
            {
                return;
            }
            foreach (PseudoPrefabSO reference in refs)
            {
                if (reference == null)
                {
                    continue;
                }
                string key = reference.bundleName + "|" + reference.assetPath;
                if (seen.Add(key))
                {
                    merged.Add(reference);
                }
            }
        }

        private static PseudoPrefabSO[] GetAutoDlcRecipeMatchLists()
        {
            if (s_autoDlcRecipeMatchLists != null)
            {
                return s_autoDlcRecipeMatchLists;
            }
            s_autoDlcRecipeMatchLists = CreateAutoDlcRefs(AutoDlcRecipeMatchListRefs);
            return s_autoDlcRecipeMatchLists;
        }

        private static PseudoPrefabSO[] GetAutoDlcCookingSteps()
        {
            if (s_autoDlcCookingSteps != null)
            {
                return s_autoDlcCookingSteps;
            }
            s_autoDlcCookingSteps = CreateAutoDlcRefs(AutoDlcCookingStepRefs);
            return s_autoDlcCookingSteps;
        }

        private static PseudoPrefabSO[] CreateAutoDlcRefs(AutoDlcRef[] refs)
        {
            List<PseudoPrefabSO> result = new List<PseudoPrefabSO>();
            for (int i = 0; i < refs.Length; i++)
            {
                if (!AutoDlcBundleExists(refs[i].BundleName))
                {
                    continue;
                }
                PseudoPrefabSO pseudoPrefabSO = ScriptableObject.CreateInstance<PseudoPrefabSO>();
                pseudoPrefabSO.name = refs[i].Name;
                pseudoPrefabSO.prefabName = refs[i].Name;
                pseudoPrefabSO.bundleName = refs[i].BundleName;
                pseudoPrefabSO.assetPath = refs[i].AssetPath;
                result.Add(pseudoPrefabSO);
            }
            return result.ToArray();
        }

        private static bool AutoDlcBundleExists(string bundleName)
        {
            string bundlePath = Path.Combine(Application.streamingAssetsPath, "Windows/" + bundleName);
            return File.Exists(bundlePath);
        }
    }
}
