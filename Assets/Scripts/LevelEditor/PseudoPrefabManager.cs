using LevelEditorStub;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;


namespace LevelEditor
{
    public enum GameEditState
    {
        Edit,
        PrepareForBuilding,
        Game,
    }

    [ExecuteInEditMode]
    [DefaultExecutionOrder(-100)]
    public class PseudoPrefabManager : MonoBehaviour
    {

        public PseudoPrefabManagerStub stub;
        public static PseudoPrefabManager Instance;

        private Dictionary<string, AssetBundle> bundleDict = new Dictionary<string, AssetBundle>();
        private HashSet<string> sharedBundleNames = new HashSet<string>();
        private AssetBundleManifest assetBundleManifest = null;

        public Dictionary<string, Material> editedMaterials = new Dictionary<string, Material>();

        [NonSerialized]
        public bool prepareForBuilding;

        private bool initializing;

        public GameEditState GameEditState
        {
            get
            {
                if (Application.isPlaying) return GameEditState.Game;
                else if (prepareForBuilding) return GameEditState.PrepareForBuilding;
                else return GameEditState.Edit;
            }
        }

        private void Awake()
        {
            stub = GetComponent<PseudoPrefabManagerStub>();
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            if (BuildPipeline.isBuildingPlayer) prepareForBuilding = true;
#endif
            if (Instance != null && Instance.gameObject != this.gameObject)
            {
                Instance.gameObject.Destroy();
            }
            Instance = this;

            if (assetBundleManifest == null)
                Init();
        }

        // weird
        // when exiting play mode in editor,
        // assetBundleManifest != null after OnEnable() but
        // assetBundleManifest == null before Start()
        private void Start()
        {
            if (assetBundleManifest == null)
                Init();
        }

        private void OnDisable()
        {
            DeInit();
            Instance = null;
        }

        public void Init()
        {
            initializing = true;

            if (!EnsureLoadAllAssetBundles())
            {
                initializing = false;
                return;
            }
            if (GameEditState == GameEditState.Game || GameEditState == GameEditState.Edit)
            {
                SetAssetRef();

                if (GameEditState == GameEditState.Game)
                {
                    GameSession gameSession = GameUtils.GetGameSession();
                    stub.BootstrapManagerGO.GetComponent<KitchenBootstrapManager>().EnsureSetup();
                    if (gameSession == null)
                    {
                        GameSession gameSession1 = GameUtils.GetGameSession();
                        typeof(GameProgress)
                            .GetField("m_sceneDirectory", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                            .SetValue(gameSession1.Progress, LevelConfigSetup.SetupSceneDirectoryData(stub.configTemplateSO, stub.levelInfo));
                        var sceneDirectoryVarientEntry = gameSession1.Progress.GetSceneDirectory().Scenes[0].GetSceneVarient(4);
                        gameSession1.LevelSettings = new GameSession.GameLevelSettings() { SceneDirectoryVarientEntry = sceneDirectoryVarientEntry };
                    }
                }

                editedMaterials.Clear();
                ResetAllPseudoPrefabs();
            }

            initializing = false;
        }

        public void DeInit()
        {
            UnSetAssetRef();
            ClearAllPseudoPrefabs();
            RuntimePrefabManager.ClearAllRuntimePrefabs();
            foreach (var key in bundleDict.Keys.ToArray())
            {
                UnloadAssetBundle(key);
            }
            bundleDict.Clear();
            sharedBundleNames.Clear();
            editedMaterials.Clear();
            assetBundleManifest = null;
        }

        private bool EnsureLoadAllAssetBundles()
        {
            if (assetBundleManifest != null) return true;
            string manifestAssetBundleName = "Windows";
            var bundle = LoadAssetBundle(manifestAssetBundleName, true);
            if (bundle == null)
            {
                Debug.LogError("Unable to load AssetBundle manifest. Check Assets/StreamingAssets/Windows.");
                return false;
            }
            assetBundleManifest = bundle.LoadAsset("AssetBundleManifest") as AssetBundleManifest;
            if (assetBundleManifest == null)
            {
                Debug.LogError("Unable to load AssetBundleManifest from Windows bundle.");
                return false;
            }

            foreach (string name in stub.levelInfo.dependencies)
            {
                LoadAssetBundle(name, false);
            }
            Debug.Log("All loaded bundles: " + string.Join(" ", bundleDict.Select(x => x.Key).ToArray()));
            return true;
        }

        // set all prefab ref in the scene
        private void SetAssetRef()
        {
            Component component = stub.FlowManagerGO.GetComponent<LevelIntroFlowroutine>();
            LevelIntroFlowroutineData m_data = (LevelIntroFlowroutineData)component.GetType()
                .GetField("m_data", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .GetValue(component);
            m_data.GoUIPrefab = LoadAsset(stub.GoSO);
            m_data.ReadyUIPrefab = LoadAsset(stub.ReadySO);
            m_data.TutorialPopup.Prefab = LoadAsset(stub.TutorialSplashSO);

            component = stub.RecipeUIGO.GetComponent<RecipeFlowGUI>();
            component.GetType()
                .GetField("m_recipeWidgetPrefab", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(component, LoadAsset(stub.RecipeUISO).GetComponent<RecipeWidgetUIController>());

            component = stub.AudioManagerGO.GetComponent<CampaignAudioManager>();
            component.GetType()
                .GetField("m_inLevelMusic", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(component, LoadAsset<AudioClip>(stub.InLevelMusicSO));
            component.GetType()
                .GetField("m_inLevelAmbiences", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(component, stub.InLevelAmbiences.Select(x => (GameLoopingAudioTag)x).ToArray());
            component.GetType()
                .GetField("m_summaryScreenMusic", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(component, LoadAsset<AudioClip>(stub.RoundResultsSO));
            AudioDirectoryData[] m_audioDirectories = new AudioDirectoryData[stub.AudioDirectorySOs.Length];
            for (int i = 0; i < stub.AudioDirectorySOs.Length; i++)
            {
                m_audioDirectories[i] = LoadAsset<AudioDirectoryData>(stub.AudioDirectorySOs[i]);
                foreach (var audio in m_audioDirectories[i].LoopingAudio)
                    if (audio.Tag == GameLoopingAudioTag.Flamethrower)
                        audio.Volume = 0.5f;
            }
            typeof(AudioManager)
                .GetField("m_audioDirectories", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(component, m_audioDirectories);

            component = stub.PlayerSwitchingManagerGO.GetComponent<PlayerSwitchingManager>();
            component.GetType()
                .GetField("m_transitionParticlePrefab", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(component, LoadAsset(stub.PFXSOs[0]).GetComponent<ParticleSystem>());
            component.GetType()
                .GetField("m_transitionStartParticlePrefab", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(component, LoadAsset(stub.PFXSOs[1]).GetComponent<ParticleSystem>());
            component.GetType()
                .GetField("m_transitionEndParticlePrefab", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(component, LoadAsset(stub.PFXSOs[2]).GetComponent<ParticleSystem>());

            component = stub.BootstrapManagerGO.GetComponent<KitchenBootstrapManager>();
            ChefAvatarData[] chefAvatarData = GetSelectedChefAvatarData();
            ChefColourData[] chefColourData = stub.PlayerColourSOs.Select(x => LoadAsset<ChefColourData>(x)).ToArray();
            component.GetType()
                .GetField("m_playerOneChef", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(component, new GameSession.SelectedChefData(chefAvatarData[0], chefColourData[0]));
            component.GetType()
                .GetField("m_playerTwoChef", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(component, new GameSession.SelectedChefData(chefAvatarData[1], chefColourData[1]));
            component.GetType()
                .GetField("m_playerThreeChef", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(component, new GameSession.SelectedChefData(chefAvatarData[2], chefColourData[2]));
            component.GetType()
                .GetField("m_playerFourChef", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(component, new GameSession.SelectedChefData(chefAvatarData[3], chefColourData[3]));
            typeof(BootstrapManager)
                .GetField("m_gameMetaEnvironmentPrefab", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(component, LoadAsset(stub.GameMetaEnvironmentSO));
            component.GetType()
                .GetField("m_bootstrapConfig", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(component, LevelConfigSetup.SetupConfig(stub.configTemplateSO, stub.levelInfo, 4));

            component = stub.KillPlaneGO.GetComponent<RespawnCollider>();
            if (stub.OnDeathEffectSO != null)
                (component as RespawnCollider).m_onDeathEffect = LoadAsset(stub.OnDeathEffectSO);
        }

        private ChefAvatarData[] GetSelectedChefAvatarData()
        {
            ChefAvatarData[] chefAvatarData = new ChefAvatarData[4];
            for (int i = 0; i < chefAvatarData.Length; i++)
            {
                PseudoPrefabSO chefSO = null;
                if (stub.PlayerChefSOs != null && i < stub.PlayerChefSOs.Length)
                    chefSO = stub.PlayerChefSOs[i];
                if (chefSO == null)
                    chefSO = stub.PlayerBlackCatSO;
                chefAvatarData[i] = LoadAsset<ChefAvatarData>(chefSO);
            }
            return chefAvatarData;
        }

        public static void ResetAllPseudoPrefabs()
        {
            PseudoPrefab[] pseudoPrefabs = GameObject.FindObjectsOfType<PseudoPrefab>();
            foreach (var pseudoPrefab in pseudoPrefabs)
                pseudoPrefab.ResetChild();
            foreach (var pseudoPrefab in pseudoPrefabs)
                pseudoPrefab.LateSetup();

            PseudoParticleSystem[] pseudoParticleSystems = GameObject.FindObjectsOfType<PseudoParticleSystem>();
            foreach (var pseudoParticleSystem in pseudoParticleSystems)
                pseudoParticleSystem.Setup();
        }

        public static void ClearAllPseudoPrefabs()
        {
            PseudoPrefab[] pseudoPrefabs = GameObject.FindObjectsOfType<PseudoPrefab>();
            foreach (var pseudoPrefab in pseudoPrefabs)
                pseudoPrefab.ClearChild();
            PseudoParticleSystem[] pseudoParticleSystems = GameObject.FindObjectsOfType<PseudoParticleSystem>();
            foreach (var pseudoParticleSystem in pseudoParticleSystems)
                pseudoParticleSystem.Clear();
        }

        public static void SetupAfterStartSynchronisingAllPseudoPrefabs()
        {
            PseudoPrefab[] pseudoPrefabs = GameObject.FindObjectsOfType<PseudoPrefab>();
            foreach (var pseudoPrefab in pseudoPrefabs)
                pseudoPrefab.SetupAfterStartSynchronising();
        }

        // unset all prefab ref in the scene
        private void UnSetAssetRef()
        {
            if (stub.FlowManagerGO == null || stub.RecipeUIGO == null ||
                stub.AudioManagerGO == null || stub.PlayerSwitchingManagerGO == null || stub.BootstrapManagerGO == null)
            {
                return;
            }

            Component component = stub.FlowManagerGO.GetComponent<LevelIntroFlowroutine>();
            if (component != null)
            {
                FieldInfo dataField = component.GetType()
                    .GetField("m_data", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                LevelIntroFlowroutineData m_data = (dataField == null) ? null : (LevelIntroFlowroutineData)dataField.GetValue(component);
                if (m_data != null)
                {
                    m_data.GoUIPrefab = null;
                    m_data.ReadyUIPrefab = null;
                    m_data.TutorialPopup.Prefab = null;
                }
            }

            component = stub.RecipeUIGO.GetComponent<RecipeFlowGUI>();
            if (component != null)
                component.GetType()
                    .GetField("m_recipeWidgetPrefab", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .SetValue(component, null);

            component = stub.AudioManagerGO.GetComponent<CampaignAudioManager>();
            component.GetType()
                .GetField("m_inLevelMusic", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(component, null);
            component.GetType()
                .GetField("m_inLevelAmbiences", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(component, new GameLoopingAudioTag[0]);
            component.GetType()
                .GetField("m_summaryScreenMusic", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(component, null);
            AudioDirectoryData[] m_audioDirectories = (AudioDirectoryData[])typeof(AudioManager)
                .GetField("m_audioDirectories", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .GetValue(component);
            for (int i = 0; i < m_audioDirectories.Length; i++)
            {
                m_audioDirectories[i] = null;
            }

            component = stub.PlayerSwitchingManagerGO.GetComponent<PlayerSwitchingManager>();
            component.GetType()
                .GetField("m_transitionParticlePrefab", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(component, null);
            component.GetType()
                .GetField("m_transitionStartParticlePrefab", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(component, null);
            component.GetType()
                .GetField("m_transitionEndParticlePrefab", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(component, null);

            component = stub.BootstrapManagerGO.GetComponent<KitchenBootstrapManager>();
            component.GetType()
                .GetField("m_playerOneChef", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(component, new GameSession.SelectedChefData(null, null));
            component.GetType()
                .GetField("m_playerTwoChef", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(component, new GameSession.SelectedChefData(null, null));
            component.GetType()
                .GetField("m_playerThreeChef", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(component, new GameSession.SelectedChefData(null, null));
            component.GetType()
                .GetField("m_playerFourChef", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(component, new GameSession.SelectedChefData(null, null));
            typeof(BootstrapManager)
                .GetField("m_gameMetaEnvironmentPrefab", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(component, null);
            component.GetType()
                .GetField("m_bootstrapConfig", BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SetValue(component, null);

            component = stub.KillPlaneGO.GetComponent<RespawnCollider>();
            (component as RespawnCollider).m_onDeathEffect = null;
        }

        public static AssetBundle GetAssetBundle(string bundleName)
        {
            if (!Instance.bundleDict.ContainsKey(bundleName) || Instance.bundleDict[bundleName] == null)
                Instance.LoadAssetBundle(bundleName);
            if (!Instance.bundleDict.ContainsKey(bundleName))
                return null;
            return Instance.bundleDict[bundleName];
        }

        public static GameObject LoadAsset(PseudoPrefabSO pseudoPrefabSO)
        {
            AssetBundle bundle = GetAssetBundle(pseudoPrefabSO.bundleName);
            if (bundle == null)
                return null;
            GameObject asset = bundle.LoadAsset<GameObject>(pseudoPrefabSO.assetPath);
            if (asset == null && !Instance.initializing)
            {
                Instance.DeInit();
                Instance.Init();
            }
            bundle = GetAssetBundle(pseudoPrefabSO.bundleName);
            if (bundle == null)
                return null;
            asset = bundle.LoadAsset<GameObject>(pseudoPrefabSO.assetPath);
            return asset;
        }

        public static T LoadAsset<T>(PseudoPrefabSO pseudoPrefabSO) where T : UnityEngine.Object
        {
            AssetBundle bundle = GetAssetBundle(pseudoPrefabSO.bundleName);
            if (bundle == null)
                return null;
            T asset = bundle.LoadAsset<T>(pseudoPrefabSO.assetPath);
            if (asset == null && !Instance.initializing)
            {
                Instance.DeInit();
                Instance.Init();
            }
            bundle = GetAssetBundle(pseudoPrefabSO.bundleName);
            if (bundle == null)
                return null;
            asset = bundle.LoadAsset<T>(pseudoPrefabSO.assetPath);
            return asset;
        }

        public static Sprite LoadSpriteSubAsset(PseudoPrefabSO pseudoPrefabSO)
        {
            AssetBundle bundle = GetAssetBundle(pseudoPrefabSO.bundleName);
            if (bundle == null)
                return null;
            var sprites = bundle.LoadAssetWithSubAssets<Sprite>(pseudoPrefabSO.assetPath);
            Sprite sprite = sprites.Length > 0 ? sprites[0] : null;
            if (sprite == null && !Instance.initializing)
            {
                Instance.DeInit();
                Instance.Init();
            }
            bundle = GetAssetBundle(pseudoPrefabSO.bundleName);
            if (bundle == null)
                return null;
            sprites = bundle.LoadAssetWithSubAssets<Sprite>(pseudoPrefabSO.assetPath);
            sprite = sprites.Length > 0 ? sprites[0] : null;
            return sprite;
        }

        public static Mesh LoadMeshSubAsset(PseudoPrefabSO pseudoPrefabSO)
        {
            AssetBundle bundle = GetAssetBundle(pseudoPrefabSO.bundleName);
            if (bundle == null)
                return null;
            var meshes = bundle.LoadAssetWithSubAssets<Mesh>(pseudoPrefabSO.assetPath);
            Mesh mesh = meshes.Length > 0 ? meshes[0] : null;
            if (mesh == null && !Instance.initializing)
            {
                Instance.DeInit();
                Instance.Init();
            }
            bundle = GetAssetBundle(pseudoPrefabSO.bundleName);
            if (bundle == null)
                return null;
            meshes = bundle.LoadAssetWithSubAssets<Mesh>(pseudoPrefabSO.assetPath);
            mesh = meshes.Length > 0 ? meshes[0] : null;
            return mesh;
        }

        private AssetBundle LoadAssetBundle(string assetBundleName, bool isLoadingAssetBundleManifest = false)
        {
            if (bundleDict.ContainsKey(assetBundleName) && bundleDict[assetBundleName] != null)
            {
                //Debug.Log("Loaded Asset Bundle : " + assetBundleName);
                return bundleDict[assetBundleName];
            }
            //Debug.Log("Loading Asset Bundle " + ((!isLoadingAssetBundleManifest) ? ": " : "Manifest: ") + assetBundleName);
            if (!isLoadingAssetBundleManifest && assetBundleManifest == null)
            {
                Debug.LogError("Please initialize AssetBundleManifest");
                return null;
            }
            LoadAssetBundleInternal(assetBundleName);
            if (!bundleDict.ContainsKey(assetBundleName) || bundleDict[assetBundleName] == null)
            {
                return null;
            }
            if (!isLoadingAssetBundleManifest)
            {
                string[] allDependencies = assetBundleManifest.GetAllDependencies(assetBundleName);
                for (int j = 0; j < allDependencies.Length; j++)
                {
                    if (!bundleDict.ContainsKey(allDependencies[j]) || bundleDict[allDependencies[j]] == null)
                        LoadAssetBundleInternal(allDependencies[j]);
                }
            }
            return bundleDict[assetBundleName];
        }

        private void LoadAssetBundleInternal(string assetBundleName)
        {
            if (bundleDict.ContainsKey(assetBundleName) && bundleDict[assetBundleName] != null)
            {
                return;
            }

            AssetBundle loadedAssetBundle = FindLoadedAssetBundle(assetBundleName);
            if (loadedAssetBundle != null)
            {
                bundleDict.SafeAdd(assetBundleName, loadedAssetBundle);
                sharedBundleNames.Add(assetBundleName);
                return;
            }

            string path = Path.Combine(Application.streamingAssetsPath, "Windows/" + assetBundleName).Replace("\\", "/");

            //FileInfo fileInfo = new FileInfo(path);
            //long fileSizeInBytes = fileInfo.Length;
            //double fileSizeInMB = fileSizeInBytes / 1024.0 / 1024.0;
            //Debug.Log(string.Format("Loading bundle: {0}, size (MB): {1:F1}", assetBundleName, fileSizeInMB));

            AssetBundle assetBundle = AssetBundle.LoadFromFile(path);
            if (assetBundle == null)
            {
                loadedAssetBundle = FindLoadedAssetBundle(assetBundleName);
                if (loadedAssetBundle != null)
                {
                    bundleDict.SafeAdd(assetBundleName, loadedAssetBundle);
                    sharedBundleNames.Add(assetBundleName);
                    return;
                }
                Debug.LogError(string.Format("{0} is not a valid asset bundle. Loaded bundles: {1}", assetBundleName, string.Join(" ", AssetBundle.GetAllLoadedAssetBundles().Select(x => x.name).ToArray())));
            }
            else
            {
                bundleDict.SafeAdd(assetBundleName, assetBundle);
            }
        }

        private AssetBundle FindLoadedAssetBundle(string assetBundleName)
        {
            foreach (AssetBundle assetBundle in AssetBundle.GetAllLoadedAssetBundles())
            {
                string loadedName = assetBundle.name.Replace("\\", "/");
                string loadedFileName = Path.GetFileName(loadedName);
                if (assetBundleName == "Windows")
                {
                    AssetBundleManifest manifest = TryLoadManifest(assetBundle);
                    if (manifest != null)
                    {
                        return assetBundle;
                    }
                }
                if (assetBundleName == "Windows" && string.IsNullOrEmpty(loadedName))
                {
                    return assetBundle;
                }
                if (string.Equals(loadedName, assetBundleName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(loadedFileName, assetBundleName, StringComparison.OrdinalIgnoreCase) ||
                    loadedName.EndsWith("/" + assetBundleName, StringComparison.OrdinalIgnoreCase))
                {
                    return assetBundle;
                }
            }
            return null;
        }

        private AssetBundleManifest TryLoadManifest(AssetBundle assetBundle)
        {
            try
            {
                return assetBundle.LoadAsset("AssetBundleManifest") as AssetBundleManifest;
            }
            catch
            {
                return null;
            }
        }

        private void UnloadAssetBundle(string assetBundleName)
        {
            if (bundleDict.ContainsKey(assetBundleName))
            {
                if (bundleDict[assetBundleName] != null && !sharedBundleNames.Contains(assetBundleName))
                {
                    bundleDict[assetBundleName].Unload(true);
                    //Debug.Log(assetBundleName + " has been unloaded successfully.");
                }
                else
                {
                    //Debug.Log(assetBundleName + " to be unloaded is null.");
                }
                bundleDict.Remove(assetBundleName);
                sharedBundleNames.Remove(assetBundleName);
            }
            else
            {
                Debug.Log(assetBundleName + " to be unloaded is not loaded.");
            }
        }
    }
}
