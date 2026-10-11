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
    //  get_selection — список выделенных объектов
    // =============================================================
    public sealed class GetSelectionTool : IDeepSeekTool
    {
        public string Name => "get_selection";
        public string Description =>
            "Возвращает список объектов, выделенных сейчас в Hierarchy.";

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

            var sb = new StringBuilder();
            sb.AppendLine($"Выделено объектов: {sel.Length}");
            foreach (var go in sel)
                sb.AppendLine($"- {go.name} ({go.GetType().Name})");

            return Task.FromResult(sb.ToString());
        }
    }

    // =============================================================
    //  align_selection_to_ground
    // =============================================================
    public sealed class AlignSelectionToGroundTool : IDeepSeekTool
    {
        public string Name => "align_selection_to_ground";
        public string Description =>
            "Прижимает все выделенные объекты к поверхности (raycast вниз). " +
            "Параметр: offset (по Y, по умолчанию 0).";

        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                offset = new { type = "number", description = "Смещение по Y" }
            },
            required = Array.Empty<string>()
        };

        public Task<string> ExecuteAsync(string jsonArguments)
        {
            var args = JObject.Parse(jsonArguments ?? "{}");
            float offset = args["offset"]?.ToObject<float>() ?? 0f;

            var sel = Selection.gameObjects;
            if (sel == null || sel.Length == 0)
                return Task.FromResult("Ничего не выделено.");

            int count = 0;
            foreach (var go in sel)
            {
                if (go == null) continue;

                // Игнорируем сам объект при raycast
                var cols = go.GetComponentsInChildren<Collider>();
                int layer = go.layer;
                go.layer = 2;
                foreach (var c in cols) c.enabled = false;

                Vector3 origin = go.transform.position + Vector3.up * 500f;
                bool hit = Physics.Raycast(origin, Vector3.down, out RaycastHit h,
                                           1000f, ~0, QueryTriggerInteraction.Ignore);

                go.layer = layer;
                foreach (var c in cols) c.enabled = true;

                if (!hit) continue;

                Undo.RecordObject(go.transform, "Align selection to ground");
                go.transform.position = h.point + Vector3.up * offset;
                count++;
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            return Task.FromResult($"Прижато к земле: {count} объектов из {sel.Length}.");
        }
    }

    // =============================================================
    //  delete_selection
    // =============================================================
    public sealed class DeleteSelectionTool : IDeepSeekTool
    {
        public string Name => "delete_selection";
        public string Description => "Удаляет все выделенные объекты.";

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

            int count = 0;
            foreach (var go in sel)
            {
                if (go == null) continue;
                Undo.DestroyObjectImmediate(go);
                count++;
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            return Task.FromResult($"Удалено: {count} объектов.");
        }
    }

    // =============================================================
    //  duplicate_selection
    // =============================================================
    public sealed class DuplicateSelectionTool : IDeepSeekTool
    {
        public string Name => "duplicate_selection";
        public string Description =>
            "Создаёт копии всех выделенных объектов. " +
            "Параметр: offset_x, offset_y, offset_z (по умолчанию 2, 0, 0).";

        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                offset_x = new { type = "number" },
                offset_y = new { type = "number" },
                offset_z = new { type = "number" }
            },
            required = Array.Empty<string>()
        };

        public Task<string> ExecuteAsync(string jsonArguments)
        {
            var args = JObject.Parse(jsonArguments ?? "{}");
            float ox = args["offset_x"]?.ToObject<float>() ?? 2f;
            float oy = args["offset_y"]?.ToObject<float>() ?? 0f;
            float oz = args["offset_z"]?.ToObject<float>() ?? 0f;
            var offset = new Vector3(ox, oy, oz);

            var sel = Selection.gameObjects;
            if (sel == null || sel.Length == 0)
                return Task.FromResult("Ничего не выделено.");

            var copies = new System.Collections.Generic.List<GameObject>();
            foreach (var go in sel)
            {
                if (go == null) continue;
                var copy = UnityEngine.Object.Instantiate(go);
                copy.name = go.name + "_Copy";
                copy.transform.position += offset;
                Undo.RegisterCreatedObjectUndo(copy, "Duplicate selection");
                copies.Add(copy);
            }

            if (copies.Count > 0)
                Selection.objects = copies.ToArray();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            return Task.FromResult($"Создано копий: {copies.Count}.");
        }
    }

    // =============================================================
    //  add_component_to_selection
    // =============================================================
    public sealed class AddComponentToSelectionTool : IDeepSeekTool
    {
        public string Name => "add_component_to_selection";
        public string Description =>
            "Добавляет компонент на все выделенные объекты. " +
            "Параметр: component_type (Rigidbody, BoxCollider, Light и т.д.).";

        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                component_type = new { type = "string" }
            },
            required = new[] { "component_type" }
        };

        public Task<string> ExecuteAsync(string jsonArguments)
        {
            var args = JObject.Parse(jsonArguments ?? "{}");
            string typeName = args["component_type"]?.ToString();
            if (string.IsNullOrEmpty(typeName))
                return Task.FromResult("Ошибка: не указан component_type.");

            // Ищем тип
            Type type = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = asm.GetType(typeName) ?? asm.GetType("UnityEngine." + typeName);
                if (type != null && typeof(Component).IsAssignableFrom(type)) break;
                type = null;
            }

            if (type == null)
                return Task.FromResult($"Тип '{typeName}' не найден.");

            var sel = Selection.gameObjects;
            if (sel == null || sel.Length == 0)
                return Task.FromResult("Ничего не выделено.");

            int added = 0;
            foreach (var go in sel)
            {
                if (go == null) continue;
                if (go.GetComponent(type) != null) continue;
                Undo.AddComponent(go, type);
                added++;
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            return Task.FromResult($"'{type.Name}' добавлен на {added} из {sel.Length}.");
        }
    }

    // =============================================================
    //  align_all_to_ground (по всей сцене)
    // =============================================================
    public sealed class AlignAllToGroundTool : IDeepSeekTool
    {
        public string Name => "align_all_to_ground";
        public string Description =>
            "Прижимает ВСЕ корневые объекты сцены к поверхности. " +
            "Параметр: offset. ВНИМАНИЕ: тяжёлая операция.";

        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                offset = new { type = "number" }
            },
            required = Array.Empty<string>()
        };

        public Task<string> ExecuteAsync(string jsonArguments)
        {
            var args = JObject.Parse(jsonArguments ?? "{}");
            float offset = args["offset"]?.ToObject<float>() ?? 0f;

            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            int count = 0;

            foreach (var go in roots)
            {
                if (go == null) continue;

                var cols = go.GetComponentsInChildren<Collider>();
                int layer = go.layer;
                go.layer = 2;
                foreach (var c in cols) c.enabled = false;

                Vector3 origin = go.transform.position + Vector3.up * 500f;
                bool hit = Physics.Raycast(origin, Vector3.down, out RaycastHit h,
                                           1000f, ~0, QueryTriggerInteraction.Ignore);

                go.layer = layer;
                foreach (var c in cols) c.enabled = true;

                if (!hit) continue;

                Undo.RecordObject(go.transform, "Align all to ground");
                go.transform.position = h.point + Vector3.up * offset;
                count++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            return Task.FromResult($"Прижато: {count} из {roots.Length}.");
        }
    }
}