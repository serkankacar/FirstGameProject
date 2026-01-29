using UnityEngine;
using UnityEditor;
using OkeyGame.Core;

namespace OkeyGame.Editor
{
    /// <summary>
    /// GameSettings ScriptableObject oluşturma aracı
    /// </summary>
    public static class GameSettingsCreator
    {
        [MenuItem("OkeyGame/Create GameSettings Asset")]
        public static void CreateGameSettingsAsset()
        {
            // Resources klasörü yoksa oluştur
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            // Mevcut asset var mı kontrol et
            var existing = AssetDatabase.LoadAssetAtPath<GameSettings>("Assets/Resources/GameSettings.asset");
            if (existing != null)
            {
                Debug.Log("GameSettings zaten mevcut!");
                Selection.activeObject = existing;
                EditorGUIUtility.PingObject(existing);
                return;
            }

            // Yeni oluştur
            var settings = ScriptableObject.CreateInstance<GameSettings>();
            
            AssetDatabase.CreateAsset(settings, "Assets/Resources/GameSettings.asset");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = settings;
            EditorGUIUtility.PingObject(settings);

            Debug.Log("GameSettings oluşturuldu: Assets/Resources/GameSettings.asset");
        }

        [MenuItem("OkeyGame/Setup Project")]
        public static void SetupProject()
        {
            // GameSettings oluştur
            CreateGameSettingsAsset();

            // Resources klasörü
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            Debug.Log("Proje kurulumu tamamlandı!");
        }
    }
}
