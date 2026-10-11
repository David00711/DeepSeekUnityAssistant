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
    /// <summary>Базовый класс для инструментов работы со сценой.</summary>
    public abstract class SceneToolBase : IDeepSeekTool
    {
        public abstract string Name { get; }
        public abstract string Description { get; }
        public abstract object ParametersSchema { get; }

        public abstract Task<string> ExecuteAsync(string jsonArguments);

        protected static JObject ParseArgs(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new JObject();
            try { return JObject.Parse(json); }
            catch { return new JObject(); }
        }

        protected static void MarkDirty()
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
    }

    // =============================================================
    //  inspect_scene — список объектов сцены
    // =============================================================
    public sealed class InspectSceneTool : SceneToolBase
    {
        public override string Name => "inspect_scene";
        public override string Description =>
            "Возвращает список корневых объектов активной сцены с их компонентами.";

        public override object ParametersSchema => new
        {
            type = "object",
            properties = new { },
            required = Array.Empty<string>()
        };

        public override Task<string> ExecuteAsync(string jsonArguments)
        {
            var sb = new StringBuilder();
            var scene = SceneManager.GetActiveScene();
            sb.AppendLine($"Scene: {scene.name} ({scene.rootCount} roots)");

            foreach (var root in scene.GetRootGameObjects())
            {
                sb.AppendLine($"- {root.name}");
                var comps = root.GetComponents<Component>();
                foreach (var c in comps)
                {
                    if (c == null) continue;
                    sb.AppendLine($"    · {c.GetType().Name}");
                }
            }

            return Task.FromResult(sb.ToString());
        }
    }

    // =============================================================
    //  create_gameobject
    // =============================================================
    public sealed class CreateGameObjectTool : SceneToolBase
    {
        public override string Name => "create_gameobject";
        public override string Description =>
            "Создаёт GameObject в сцене. Параметры: name (string), " +
            "primitive (cube/sphere/cylinder/capsule/plane/empty), " +
            "position (x,y,z), parent (имя родителя, опционально).";

        public override object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                name = new { type = "string", description = "Имя объекта" },
                primitive = new { type = "string", description = "cube|sphere|cylinder|capsule|plane|empty" },
                position = new
                {
                    type = "object",
                    properties = new
                    {
                        x = new { type = "number" },
                        y = new { type = "number" },
                        z = new { type = "number" }
                    }
                },
                parent = new { type = "string", description = "Имя родителя (опционально)" }
            },
            required = new[] { "name" }
        };

        public override Task<string> ExecuteAsync(string jsonArguments)
        {
            var args = ParseArgs(jsonArguments);
            string name = args["name"]?.ToString() ?? "NewObject";
            string primitive = (args["primitive"]?.ToString() ?? "empty").ToLowerInvariant();

            Vector3 pos = Vector3.zero;
            var posNode = args["position"];
            if (posNode != null)
            {
                pos = new Vector3(
                    posNode["x"]?.ToObject<float>() ?? 0f,
                    posNode["y"]?.ToObject<float>() ?? 0f,
                    posNode["z"]?.ToObject<float>() ?? 0f);
            }

            GameObject go;
            switch (primitive)
            {
                case "cube":     go = GameObject.CreatePrimitive(PrimitiveType.Cube); break;
                case "sphere":   go = GameObject.CreatePrimitive(PrimitiveType.Sphere); break;
                case "cylinder": go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); break;
                case "capsule":  go = GameObject.CreatePrimitive(PrimitiveType.Capsule); break;
                case "plane":    go = GameObject.CreatePrimitive(PrimitiveType.Plane); break;
                default:         go = new GameObject(); break;
            }

            go.name = name;
            go.transform.position = pos;

            string parentName = args["parent"]?.ToString();
            if (!string.IsNullOrEmpty(parentName))
            {
                var parent = GameObject.Find(parentName);
                if (parent != null) go.transform.SetParent(parent.transform, true);
            }

            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
            Selection.activeGameObject = go;
            MarkDirty();

            return Task.FromResult($"Создан объект '{name}' в позиции {pos}.");
        }
    }

    // =============================================================
    //  delete_gameobject
    // =============================================================
    public sealed class DeleteGameObjectTool : SceneToolBase
    {
        public override string Name => "delete_gameobject";
        public override string Description =>
            "Удаляет GameObject по имени. Параметр: name (string).";

        public override object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                name = new { type = "string", description = "Имя объекта для удаления" }
            },
            required = new[] { "name" }
        };

        public override Task<string> ExecuteAsync(string jsonArguments)
        {
            var args = ParseArgs(jsonArguments);
            string name = args["name"]?.ToString();
            if (string.IsNullOrEmpty(name))
                return Task.FromResult("Ошибка: не указано имя.");

            var go = GameObject.Find(name);
            if (go == null)
                return Task.FromResult($"Объект '{name}' не найден.");

            Undo.DestroyObjectImmediate(go);
            MarkDirty();
            return Task.FromResult($"Удалён объект '{name}'.");
        }
    }

    // =============================================================
    //  set_transform
    // =============================================================
    public sealed class SetTransformTool : SceneToolBase
    {
        public override string Name => "set_transform";
        public override string Description =>
            "Меняет позицию/поворот/масштаб объекта. Параметры: name, position, rotation, scale.";

        public override object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                name = new { type = "string" },
                position = new
                {
                    type = "object",
                    properties = new
                    {
                        x = new { type = "number" },
                        y = new { type = "number" },
                        z = new { type = "number" }
                    }
                },
                rotation = new
                {
                    type = "object",
                    properties = new
                    {
                        x = new { type = "number" },
                        y = new { type = "number" },
                        z = new { type = "number" }
                    }
                },
                scale = new
                {
                    type = "object",
                    properties = new
                    {
                        x = new { type = "number" },
                        y = new { type = "number" },
                        z = new { type = "number" }
                    }
                }
            },
            required = new[] { "name" }
        };

        public override Task<string> ExecuteAsync(string jsonArguments)
        {
            var args = ParseArgs(jsonArguments);
            string name = args["name"]?.ToString();
            if (string.IsNullOrEmpty(name))
                return Task.FromResult("Ошибка: не указано имя.");

            var go = GameObject.Find(name);
            if (go == null)
                return Task.FromResult($"Объект '{name}' не найден.");

            Undo.RecordObject(go.transform, "Set transform");

            if (args["position"] is JObject p)
                go.transform.position = new Vector3(
                    p["x"]?.ToObject<float>() ?? 0f,
                    p["y"]?.ToObject<float>() ?? 0f,
                    p["z"]?.ToObject<float>() ?? 0f);

            if (args["rotation"] is JObject r)
                go.transform.eulerAngles = new Vector3(
                    r["x"]?.ToObject<float>() ?? 0f,
                    r["y"]?.ToObject<float>() ?? 0f,
                    r["z"]?.ToObject<float>() ?? 0f);

            if (args["scale"] is JObject s)
                go.transform.localScale = new Vector3(
                    s["x"]?.ToObject<float>() ?? 1f,
                    s["y"]?.ToObject<float>() ?? 1f,
                    s["z"]?.ToObject<float>() ?? 1f);

            MarkDirty();
            return Task.FromResult($"Обновлён transform у '{name}'.");
        }
    }

    // =============================================================
    //  list_root_objects — быстрый список корневых объектов
    // =============================================================
    public sealed class ListRootObjectsTool : SceneToolBase
    {
        public override string Name => "list_root_objects";
        public override string Description =>
            "Возвращает имена всех корневых объектов активной сцены (быстрая версия inspect_scene).";

        public override object ParametersSchema => new
        {
            type = "object",
            properties = new { },
            required = Array.Empty<string>()
        };

        public override Task<string> ExecuteAsync(string jsonArguments)
        {
            var scene = SceneManager.GetActiveScene();
            var sb = new StringBuilder();
            foreach (var root in scene.GetRootGameObjects())
                sb.AppendLine(root.name);
            return Task.FromResult(sb.ToString());
        }
    }
}