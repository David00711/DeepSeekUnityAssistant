using System;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeepSeekUnity.Editor.Tools
{
    // =============================================================
    //  find_prefab_instances — найти все копии префаба в сцене
    // =============================================================
    public sealed class FindPrefabInstancesTool : IDeepSeekTool
    {
        public string Name => "find_prefab_instances";
        public string Description =>
            "Ищет все инстансы указанного префаба на активной сцене. " +
            "Параметр: prefab_path (Assets/...).";

        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                prefab_path = new { type = "string" }
            },
            required = new[] { "prefab_path" }
        };

        public Task<string> ExecuteAsync(string jsonArguments)
        {
            var args = JObject.Parse(jsonArguments ?? "{}");
            string path = args["prefab_path"]?.ToString();
            if (string.IsNullOrEmpty(path))
                return Task.FromResult("Ошибка: не указан prefab_path.");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                return Task.FromResult($"Префаб '{path}' не найден.");

            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            var sb = new StringBuilder();
            int found = 0;

            foreach (var root in roots)
            {
                var transforms = root.GetComponentsInChildren<Transform>(true);
                foreach (var t in transforms)
                {
                    var source = PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject);
                    if (source == prefab)
                    {
                        sb.AppendLine($"- {t.name} @ {t.position}");
                        found++;
                    }
                }
            }

            if (found == 0)
                return Task.FromResult($"Инстансы '{path}' в сцене не найдены.");

            return Task.FromResult($"Найдено {found}:\n{sb}");
        }
    }

    // =============================================================
    //  open_prefab — открыть префаб в Prefab Mode
    // =============================================================
    public sealed class OpenPrefabTool : IDeepSeekTool
    {
        public string Name => "open_prefab";
        public string Description =>
            "Открывает префаб в режиме редактирования (Prefab Mode). " +
            "Параметр: prefab_path.";

        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                prefab_path = new { type = "string" }
            },
            required = new[] { "prefab_path" }
        };

        public Task<string> ExecuteAsync(string jsonArguments)
        {
            var args = JObject.Parse(jsonArguments ?? "{}");
            string path = args["prefab_path"]?.ToString();
            if (string.IsNullOrEmpty(path))
                return Task.FromResult("Ошибка: не указан prefab_path.");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                return Task.FromResult($"Префаб '{path}' не найден.");

            try
            {
                AssetDatabase.OpenAsset(prefab);
                return Task.FromResult($"Префаб открыт: {path}");
            }
            catch (Exception e)
            {
                return Task.FromResult($"Ошибка открытия: {e.Message}");
            }
        }
    }

    // =============================================================
    //  apply_prefab_overrides — применить оверрайды
    // =============================================================
    public sealed class ApplyPrefabOverridesTool : IDeepSeekTool
    {
        public string Name => "apply_prefab_overrides";
        public string Description =>
            "Применяет оверрайды выбранного объекта к его префабу-источнику.";

        public object ParametersSchema => new
        {
            type = "object",
            properties = new { },
            required = Array.Empty<string>()
        };

        public Task<string> ExecuteAsync(string jsonArguments)
        {
            var sel = Selection.gameObjects;
            if (sel == null || sel.Length == 0)
                return Task.FromResult("Ничего не выделено.");

            int applied = 0;
            foreach (var go in sel)
            {
                if (go == null) continue;
                if (!PrefabUtility.IsPartOfPrefabInstance(go)) continue;

                try
                {
                    PrefabUtility.ApplyPrefabInstance(go, InteractionMode.UserAction);
                    applied++;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[DeepSeek] Не удалось применить оверрайды для {go.name}: {e.Message}");
                }
            }

            AssetDatabase.Refresh();
            return Task.FromResult($"Применено оверрайдов: {applied}.");
        }
    }

    // =============================================================
    //  revert_prefab_overrides — откатить оверрайды
    // =============================================================
    public sealed class RevertPrefabOverridesTool : IDeepSeekTool
    {
        public string Name => "revert_prefab_overrides";
        public string Description =>
            "Откатывает оверрайды выделенных объектов к состоянию префаба.";

        public object ParametersSchema => new
        {
            type = "object",
            properties = new { },
            required = Array.Empty<string>()
        };

        public Task<string> ExecuteAsync(string jsonArguments)
        {
            var sel = Selection.gameObjects;
            if (sel == null || sel.Length == 0)
                return Task.FromResult("Ничего не выделено.");

            int reverted = 0;
            foreach (var go in sel)
            {
                if (go == null) continue;
                if (!PrefabUtility.IsPartOfPrefabInstance(go)) continue;

                try
                {
                    PrefabUtility.RevertPrefabInstance(go, InteractionMode.UserAction);
                    reverted++;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[DeepSeek] Не удалось откатить {go.name}: {e.Message}");
                }
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            return Task.FromResult($"Откачено: {reverted}.");
        }
    }

    // =============================================================
    //  unpack_prefab — распаковать префаб
    // =============================================================
    public sealed class UnpackPrefabTool : IDeepSeekTool
    {
        public string Name => "unpack_prefab";
        public string Description =>
            "Распаковывает выделенные инстансы префаба (PrefabUtility.UnpackPrefabInstance).";

        public object ParametersSchema => new
        {
            type = "object",
            properties = new { },
            required = Array.Empty<string>()
        };

        public Task<string> ExecuteAsync(string jsonArguments)
        {
            var sel = Selection.gameObjects;
            if (sel == null || sel.Length == 0)
                return Task.FromResult("Ничего не выделено.");

            int unpacked = 0;
            foreach (var go in sel)
            {
                if (go == null) continue;
                if (!PrefabUtility.IsPartOfPrefabInstance(go)) continue;

                try
                {
                    PrefabUtility.UnpackPrefabInstance(go,
                        PrefabUnpackMode.Completely,
                        InteractionMode.UserAction);
                    unpacked++;
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[DeepSeek] Не удалось распаковать {go.name}: {e.Message}");
                }
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            return Task.FromResult($"Распаковано: {unpacked}.");
        }
    }
}