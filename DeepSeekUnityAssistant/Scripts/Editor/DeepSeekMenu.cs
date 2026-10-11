using UnityEditor;
using UnityEngine;

namespace DeepSeekUnity.Editor
{
    /// <summary>
    /// Автопоказ мастера при первом импорте и дополнительные пункты меню.
    /// </summary>
    [InitializeOnLoad]
    public static class DeepSeekFirstRun
    {
        static DeepSeekFirstRun()
        {
            EditorApplication.delayCall += OnFirstRun;
        }

        private static void OnFirstRun()
        {
            // Пропускаем, если Unity в процессе обновления ассетов
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += OnFirstRun;
                return;
            }

            DeepSeekSetupWizard.OpenIfNeeded();
        }
    }

    /// <summary>Пункты меню Window → DeepSeek → ...</summary>
    public static class DeepSeekMenu
    {
        [MenuItem("Window/DeepSeek/Assistant %#a")]
        public static void OpenAssistant()
        {
            DeepSeekAssistantWindow.Open();
        }

        [MenuItem("Window/DeepSeek/Open Config %#g")]
        public static void OpenConfig()
        {
            var cfg = DeepSeekSetupWizard.ExistingConfig();

            if (cfg == null)
            {
                if (EditorUtility.DisplayDialog(
                    "DeepSeek",
                    "Конфиг не найден. Создать его сейчас?",
                    "Открыть мастер", "Отмена"))
                {
                    DeepSeekSetupWizard.Open();
                }
                return;
            }

            Selection.activeObject = cfg;
            EditorGUIUtility.PingObject(cfg);
            Debug.Log($"[DeepSeek] Config: {AssetDatabase.GetAssetPath(cfg)}");
        }

        [MenuItem("Window/DeepSeek/Reset Wizard Preferences")]
        public static void ResetWizard()
        {
            if (!EditorUtility.DisplayDialog(
                "DeepSeek",
                "Сбросить настройки мастера? Он снова откроется при следующем запуске Unity.",
                "Сбросить", "Отмена"))
                return;

            EditorPrefs.DeleteKey(DeepSeekSetupWizard.DontShowKey);
            EditorPrefs.DeleteKey(DeepSeekSetupWizard.ConfigGuidKey);
            Debug.Log("[DeepSeek] Настройки мастера сброшены.");
        }

        [MenuItem("Window/DeepSeek/About")]
        public static void About()
        {
            EditorUtility.DisplayDialog(
                "DeepSeek Unity Assistant",
                "DeepSeek Unity Assistant v1.0.0\n\n" +
                "AI-ассистент для Unity Editor на базе DeepSeek.\n\n" +
                "Автор: Галавинов Давид Викторович\n" +
                "Лицензия: MIT\n\n" +
                "github.com/ТВОЙ_РЕПОЗИТОРИЙ/DeepSeekUnityAssistant",
                "OK");
        }
    }
}