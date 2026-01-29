#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using OkeyGame.Core;

namespace OkeyGame.Editor
{
    /// <summary>
    /// Otomatik proje kurulumu - Unity menüsünden çalıştırılır
    /// </summary>
    public static class ProjectSetup
    {
        private const string SETTINGS_PATH = "Assets/Settings";
        private const string GAME_SETTINGS_PATH = "Assets/Settings/GameSettings.asset";
        private const string TEXTURES_PATH = "Assets/Textures";
        private const string AUDIO_PATH = "Assets/Audio";
        private const string FONTS_PATH = "Assets/Fonts";
        private const string PREFABS_PATH = "Assets/Prefabs";
        private const string SCENES_PATH = "Assets/Scenes";

        [MenuItem("OkeyGame/Setup Project", false, 1)]
        public static void SetupProject()
        {
            CreateFolders();
            CreateGameSettings();
            Debug.Log("[ProjectSetup] ✅ Proje kurulumu tamamlandı!");
            EditorUtility.DisplayDialog("Proje Kurulumu", 
                "Proje kurulumu tamamlandı!\n\n" +
                "Şimdi yapılması gerekenler:\n" +
                "1. OkeyGame → Setup Scene menüsünü çalıştırın\n" +
                "2. Play butonuna basarak test edin", 
                "Tamam");
        }

        [MenuItem("OkeyGame/Setup Scene", false, 2)]
        public static void SetupScene()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            
            // Bootstrap
            CreateBootstrap();
            
            // UI Documents
            CreateUIDocument("MainMenuUI", "Assets/UI/Documents/MainMenuScreen.uxml", typeof(OkeyGame.UI.MainMenuScreen));
            CreateUIDocument("LobbyUI", "Assets/UI/Documents/LobbyScreen.uxml", typeof(OkeyGame.UI.LobbyScreen), false);
            CreateUIDocument("GameTableUI", "Assets/UI/Documents/GameTableScreen.uxml", typeof(OkeyGame.UI.GameTableScreen), false);
            
            // Scene Controller
            CreateSceneController();
            
            // Mark scene dirty
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            
            Debug.Log("[ProjectSetup] ✅ Sahne kurulumu tamamlandı!");
            EditorUtility.DisplayDialog("Sahne Kurulumu", 
                "Sahne kurulumu tamamlandı!\n\n" +
                "Play butonuna basarak test edebilirsiniz.", 
                "Tamam");
        }

        [MenuItem("OkeyGame/Fix Scene Layout", false, 3)]
        public static void FixSceneLayout()
        {
            // MainMenuUI'ı aktif, diğerlerini deaktif yap
            var mainMenuUI = GameObject.Find("MainMenuUI");
            var lobbyUI = GameObject.Find("LobbyUI");
            var gameTableUI = GameObject.Find("GameTableUI");
            
            // Deaktif olanları da bul
            if (lobbyUI == null)
            {
                var allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
                foreach (var obj in allObjects)
                {
                    if (obj.name == "LobbyUI" && obj.scene.IsValid())
                        lobbyUI = obj;
                    if (obj.name == "GameTableUI" && obj.scene.IsValid())
                        gameTableUI = obj;
                    if (obj.name == "MainMenuUI" && obj.scene.IsValid())
                        mainMenuUI = obj;
                }
            }
            
            if (mainMenuUI != null)
            {
                mainMenuUI.SetActive(true);
                Debug.Log("[FixScene] MainMenuUI aktif edildi");
            }
            
            if (lobbyUI != null)
            {
                lobbyUI.SetActive(false);
                Debug.Log("[FixScene] LobbyUI deaktif edildi");
            }
            
            if (gameTableUI != null)
            {
                gameTableUI.SetActive(false);
                Debug.Log("[FixScene] GameTableUI deaktif edildi");
            }
            
            // SceneController referanslarını düzelt
            var sceneController = Object.FindAnyObjectByType<OkeyGame.UI.SceneController>();
            if (sceneController == null)
            {
                var scGO = GameObject.Find("SceneController");
                if (scGO != null)
                {
                    sceneController = scGO.GetComponent<OkeyGame.UI.SceneController>();
                }
            }
            
            if (sceneController != null)
            {
                var serializedObj = new SerializedObject(sceneController);
                
                if (mainMenuUI != null)
                {
                    var docProp = serializedObj.FindProperty("_mainMenuDocument");
                    if (docProp != null) docProp.objectReferenceValue = mainMenuUI.GetComponent<UIDocument>();
                    
                    var screenProp = serializedObj.FindProperty("_mainMenuScreen");
                    if (screenProp != null) screenProp.objectReferenceValue = mainMenuUI.GetComponent<OkeyGame.UI.MainMenuScreen>();
                }
                
                if (lobbyUI != null)
                {
                    var docProp = serializedObj.FindProperty("_lobbyDocument");
                    if (docProp != null) docProp.objectReferenceValue = lobbyUI.GetComponent<UIDocument>();
                    
                    var screenProp = serializedObj.FindProperty("_lobbyScreen");
                    if (screenProp != null) screenProp.objectReferenceValue = lobbyUI.GetComponent<OkeyGame.UI.LobbyScreen>();
                }
                
                if (gameTableUI != null)
                {
                    var docProp = serializedObj.FindProperty("_gameTableDocument");
                    if (docProp != null) docProp.objectReferenceValue = gameTableUI.GetComponent<UIDocument>();
                    
                    var screenProp = serializedObj.FindProperty("_gameTableScreen");
                    if (screenProp != null) screenProp.objectReferenceValue = gameTableUI.GetComponent<OkeyGame.UI.GameTableScreen>();
                }
                
                serializedObj.ApplyModifiedProperties();
                Debug.Log("[FixScene] SceneController referansları güncellendi");
            }
            
            // Scene'i kaydet
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            
            EditorUtility.DisplayDialog("Scene Düzeltildi", 
                "Sahne düzeni düzeltildi!\n\n" +
                "• MainMenuUI: Aktif\n" +
                "• LobbyUI: Deaktif\n" +
                "• GameTableUI: Deaktif\n\n" +
                "Play butonuna basarak test edin.", 
                "Tamam");
        }

        [MenuItem("OkeyGame/Validate Project", false, 10)]
        public static void ValidateProject()
        {
            var issues = new System.Collections.Generic.List<string>();
            
            // Check GameSettings
            var gameSettings = AssetDatabase.LoadAssetAtPath<GameSettings>(GAME_SETTINGS_PATH);
            if (gameSettings == null)
            {
                issues.Add("❌ GameSettings.asset bulunamadı - OkeyGame → Setup Project çalıştırın");
            }
            else
            {
                issues.Add("✅ GameSettings.asset mevcut");
            }
            
            // Check UXML files
            CheckAsset("Assets/UI/Documents/MainMenuScreen.uxml", issues);
            CheckAsset("Assets/UI/Documents/LobbyScreen.uxml", issues);
            CheckAsset("Assets/UI/Documents/GameTableScreen.uxml", issues);
            
            // Check USS files
            CheckAsset("Assets/UI/Styles/MainMenuStyles.uss", issues);
            CheckAsset("Assets/UI/Styles/LobbyStyles.uss", issues);
            CheckAsset("Assets/UI/Styles/GameTableStyles.uss", issues);
            
            // Check scripts
            CheckScript<GameManager>(issues);
            CheckScript<GameSettings>(issues);
            CheckScript<OkeyGame.Network.ApiService>(issues);
            CheckScript<OkeyGame.Network.SignalRConnection>(issues);
            CheckScript<OkeyGame.Game.GameTableController>(issues);
            
            // Display results
            string message = string.Join("\n", issues);
            EditorUtility.DisplayDialog("Proje Doğrulama", message, "Tamam");
            
            Debug.Log("[ProjectSetup] Validation results:\n" + message);
        }

        private static void CreateFolders()
        {
            CreateFolderIfNotExists(SETTINGS_PATH);
            CreateFolderIfNotExists(TEXTURES_PATH);
            CreateFolderIfNotExists(AUDIO_PATH);
            CreateFolderIfNotExists(FONTS_PATH);
            CreateFolderIfNotExists(PREFABS_PATH);
            CreateFolderIfNotExists(SCENES_PATH);
            
            AssetDatabase.Refresh();
        }

        private static void CreateFolderIfNotExists(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                var parts = path.Split('/');
                string currentPath = parts[0];
                
                for (int i = 1; i < parts.Length; i++)
                {
                    string newPath = currentPath + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(newPath))
                    {
                        AssetDatabase.CreateFolder(currentPath, parts[i]);
                        Debug.Log($"[ProjectSetup] Created folder: {newPath}");
                    }
                    currentPath = newPath;
                }
            }
        }

        private static void CreateGameSettings()
        {
            if (AssetDatabase.LoadAssetAtPath<GameSettings>(GAME_SETTINGS_PATH) != null)
            {
                Debug.Log("[ProjectSetup] GameSettings already exists");
                return;
            }

            var settings = ScriptableObject.CreateInstance<GameSettings>();
            AssetDatabase.CreateAsset(settings, GAME_SETTINGS_PATH);
            AssetDatabase.SaveAssets();
            
            Debug.Log("[ProjectSetup] Created GameSettings.asset");
        }

        private static void CreateBootstrap()
        {
            var existing = Object.FindAnyObjectByType<GameBootstrap>();
            if (existing != null)
            {
                Debug.Log("[ProjectSetup] Bootstrap already exists");
                return;
            }

            var bootstrapGO = new GameObject("Bootstrap");
            var bootstrap = bootstrapGO.AddComponent<GameBootstrap>();
            
            // Assign GameSettings
            var gameSettings = AssetDatabase.LoadAssetAtPath<GameSettings>(GAME_SETTINGS_PATH);
            if (gameSettings != null)
            {
                var serializedObj = new SerializedObject(bootstrap);
                var prop = serializedObj.FindProperty("_gameSettings");
                if (prop != null)
                {
                    prop.objectReferenceValue = gameSettings;
                    serializedObj.ApplyModifiedProperties();
                }
            }
            
            Debug.Log("[ProjectSetup] Created Bootstrap GameObject");
        }

        private static void CreateUIDocument(string name, string uxmlPath, System.Type controllerType, bool activeByDefault = true)
        {
            // Check if already exists
            var existing = GameObject.Find(name);
            if (existing != null)
            {
                Debug.Log($"[ProjectSetup] {name} already exists");
                return;
            }

            var go = new GameObject(name);
            
            // Add UIDocument
            var uiDoc = go.AddComponent<UIDocument>();
            
            // Load and assign UXML
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
            if (uxml != null)
            {
                uiDoc.visualTreeAsset = uxml;
            }
            else
            {
                Debug.LogWarning($"[ProjectSetup] UXML not found: {uxmlPath}");
            }
            
            // Load and assign Panel Settings
            var panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/UI/DefaultPanelSettings.asset");
            if (panelSettings != null)
            {
                uiDoc.panelSettings = panelSettings;
            }
            
            // Add controller script
            var controller = go.AddComponent(controllerType);
            
            // Assign UIDocument reference to controller
            var serializedObj = new SerializedObject(controller);
            var prop = serializedObj.FindProperty("_uiDocument");
            if (prop != null)
            {
                prop.objectReferenceValue = uiDoc;
                serializedObj.ApplyModifiedProperties();
            }
            
            // Set active state
            go.SetActive(activeByDefault);
            
            Debug.Log($"[ProjectSetup] Created {name} GameObject");
        }

        private static void CreateSceneController()
        {
            var existing = Object.FindAnyObjectByType<OkeyGame.UI.SceneController>();
            if (existing != null)
            {
                Debug.Log("[ProjectSetup] SceneController already exists");
                return;
            }

            var go = new GameObject("SceneController");
            var controller = go.AddComponent<OkeyGame.UI.SceneController>();
            
            // Find and assign UI documents
            var serializedObj = new SerializedObject(controller);
            
            var mainMenuUI = GameObject.Find("MainMenuUI");
            var lobbyUI = GameObject.Find("LobbyUI");
            var gameTableUI = GameObject.Find("GameTableUI");
            
            if (mainMenuUI != null)
            {
                var prop = serializedObj.FindProperty("_mainMenuDocument");
                if (prop != null)
                {
                    prop.objectReferenceValue = mainMenuUI.GetComponent<UIDocument>();
                }
                
                var screenProp = serializedObj.FindProperty("_mainMenuScreen");
                if (screenProp != null)
                {
                    screenProp.objectReferenceValue = mainMenuUI.GetComponent<OkeyGame.UI.MainMenuScreen>();
                }
            }
            
            if (gameTableUI != null)
            {
                var prop = serializedObj.FindProperty("_gameTableDocument");
                if (prop != null)
                {
                    prop.objectReferenceValue = gameTableUI.GetComponent<UIDocument>();
                }
                
                var screenProp = serializedObj.FindProperty("_gameTableScreen");
                if (screenProp != null)
                {
                    screenProp.objectReferenceValue = gameTableUI.GetComponent<OkeyGame.UI.GameTableScreen>();
                }
            }
            
            serializedObj.ApplyModifiedProperties();
            
            Debug.Log("[ProjectSetup] Created SceneController GameObject");
        }

        private static void CheckAsset(string path, System.Collections.Generic.List<string> issues)
        {
            var asset = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (asset != null)
            {
                issues.Add($"✅ {System.IO.Path.GetFileName(path)}");
            }
            else
            {
                issues.Add($"❌ {System.IO.Path.GetFileName(path)} bulunamadı");
            }
        }

        private static void CheckScript<T>(System.Collections.Generic.List<string> issues) where T : class
        {
            var typeName = typeof(T).Name;
            var guids = AssetDatabase.FindAssets($"t:MonoScript {typeName}");
            if (guids.Length > 0)
            {
                issues.Add($"✅ {typeName}.cs");
            }
            else
            {
                issues.Add($"❌ {typeName}.cs bulunamadı");
            }
        }
    }
}
#endif
