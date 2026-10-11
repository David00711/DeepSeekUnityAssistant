using System;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace DeepSeekUnity.Editor.Tools
{
    // =============================================================
    //  create_material
    // =============================================================
    public sealed class CreateMaterialTool : IDeepSeekTool
    {
        public string Name => "create_material";
        public string Description =>
            "Создаёт материал Standard с заданным цветом и сохраняет в Assets/Materials/. " +
            "Параметры: name, color (hex или r,g,b), metallic, smoothness, path (опционально).";

        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                name = new { type = "string", description = "Имя материала" },
                color = new { type = "string", description = "Цвет в формате #RRGGBB или 'r,g,b'" },
                metallic = new { type = "number", description = "0..1" },
                smoothness = new { type = "number", description = "0..1" },
                path = new { type = "string", description = "Папка для сохранения (по умолчанию Assets/Materials)" }
            },
            required = new[] { "name" }
        };

        public Task<string> ExecuteAsync(string jsonArguments)
        {
            var args = JObject.Parse(jsonArguments ?? "{}");
            string name = args["name"]?.ToString();
            if (string.IsNullOrEmpty(name))
                return Task.FromResult("Ошибка: не указано имя материала.");

            string colorStr = args["color"]?.ToString() ?? "#FFFFFF";
            Color color = ParseColor(colorStr);
            float metallic = args["metallic"]?.ToObject<float>() ?? 0f;
            float smoothness = args["smoothness"]?.ToObject<float>() ?? 0.5f;
            string dir = args["path"]?.ToString() ?? "Assets/Materials";

            if (!AssetDatabase.IsValidFolder(dir))
                Directory.CreateDirectory(dir);

            string assetPath = $"{dir}/{name}.mat";
            if (File.Exists(assetPath))
                AssetDatabase.DeleteAsset(assetPath);

            var shader = Shader.Find("Standard");
            var mat = new Material(shader) { name = name };
            mat.color = color;
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Glossiness", smoothness);

            AssetDatabase.CreateAsset(mat, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return Task.FromResult($"Создан материал: {assetPath}");
        }

        private static Color ParseColor(string s)
        {
            if (string.IsNullOrEmpty(s)) return Color.white;

            s = s.Trim();
            if (s.StartsWith("#"))
            {
                if (ColorUtility.TryParseHtmlString(s, out var c)) return c;
            }
            else if (s.Contains(","))
            {
                var parts = s.Split(',');
                if (parts.Length >= 3)
                {
                    float r = float.Parse(parts[0].Trim(), System.Globalization.CultureInfo.InvariantCulture);
                    float g = float.Parse(parts[1].Trim(), System.Globalization.CultureInfo.InvariantCulture);
                    float b = float.Parse(parts[2].Trim(), System.Globalization.CultureInfo.InvariantCulture);
                    return new Color(r, g, b);
                }
            }
            return Color.white;
        }
    }

    // =============================================================
    //  assign_material
    // =============================================================
    public sealed class AssignMaterialTool : IDeepSeekTool
    {
        public string Name => "assign_material";
        public string Description =>
            "Назначает материал на объект сцены. Параметры: object_name, material_path (Assets/...).";
        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                object_name = new { type = "string", description = "Имя объекта в сцене" },
                material_path = new { type = "string", description = "Путь к материалу (Assets/Materials/...)" }
            },
            required = new[] { "object_name", "material_path" }
        };

        public Task<string> ExecuteAsync(string jsonArguments)
        {
            var args = JObject.Parse(jsonArguments ?? "{}");
            string objName = args["object_name"]?.ToString();
            string matPath = args["material_path"]?.ToString();

            if (string.IsNullOrEmpty(objName) || string.IsNullOrEmpty(matPath))
                return Task.FromResult("Ошибка: нужны object_name и material_path.");

            var go = GameObject.Find(objName);
            if (go == null)
                return Task.FromResult($"Объект '{objName}' не найден.");

            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
                return Task.FromResult($"Материал '{matPath}' не найден.");

            var renderer = go.GetComponent<Renderer>();
            if (renderer == null)
                return Task.FromResult($"У объекта '{objName}' нет Renderer.");

            Undo.RecordObject(renderer, "Assign material");
            renderer.sharedMaterial = mat;
            EditorUtility.SetDirty(renderer);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);

            return Task.FromResult($"Материал '{mat.name}' назначен на '{objName}'.");
        }
    }

    // =============================================================
    //  add_component
    // =============================================================
    public sealed class AddComponentTool : IDeepSeekTool
    {
        public string Name => "add_component";
        public string Description =>
            "Добавляет компонент на объект. Параметры: object_name, component_type " +
            "(например, Rigidbody, BoxCollider, Light, Camera).";
        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                object_name = new { type = "string", description = "Имя объекта" },
                component_type = new { type = "string", description = "Полное или короткое имя типа компонента" }
            },
            required = new[] { "object_name", "component_type" }
        };

        public Task<string> ExecuteAsync(string jsonArguments)
        {
            var args = JObject.Parse(jsonArguments ?? "{}");
            string objName = args["object_name"]?.ToString();
            string typeName = args["component_type"]?.ToString();

            if (string.IsNullOrEmpty(objName) || string.IsNullOrEmpty(typeName))
                return Task.FromResult("Ошибка: нужны object_name и component_type.");

            var go = GameObject.Find(objName);
            if (go == null)
                return Task.FromResult($"Объект '{objName}' не найден.");

            // Ищем тип в сборках Unity
            Type type = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                type = asm.GetType(typeName) ?? asm.GetType("UnityEngine." + typeName);
                if (type != null && typeof(Component).IsAssignableFrom(type)) break;
                type = null;
            }

            if (type == null)
                return Task.FromResult($"Тип '{typeName}' не найден.");

            var comp = Undo.AddComponent(go, type);
            if (comp == null)
                return Task.FromResult($"Не удалось добавить '{typeName}' (возможно, уже есть).");

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
            return Task.FromResult($"Компонент '{type.Name}' добавлен на '{objName}'.");
        }
    }

    // =============================================================
    //  remove_component
    // =============================================================
    public sealed class RemoveComponentTool : IDeepSeekTool
    {
        public string Name => "remove_component";
        public string Description => "Удаляет компонент с объекта. Параметры: object_name, component_type.";
        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                object_name = new { type = "string" },
                component_type = new { type = "string" }
            },
            required = new[] { "object_name", "component_type" }
        };

        public Task<string> ExecuteAsync(string jsonArguments)
        {
            var args = JObject.Parse(jsonArguments ?? "{}");
            string objName = args["object_name"]?.ToString();
            string typeName = args["component_type"]?.ToString();

            var go = GameObject.Find(objName);
            if (go == null) return Task.FromResult($"Объект '{objName}' не найден.");

            Component found = null;
            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null) continue;
                if (c.GetType().Name == typeName || c.GetType().FullName == typeName)
                {
                    found = c;
                    break;
                }
            }

            if (found == null)
                return Task.FromResult($"Компонент '{typeName}' не найден на '{objName}'.");

            Undo.DestroyObjectImmediate(found);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
            return Task.FromResult($"Компонент '{typeName}' удалён с '{objName}'.");
        }
    }

    // =============================================================
    //  create_prefab
    // =============================================================
    public sealed class CreatePrefabTool : IDeepSeekTool
    {
        public string Name => "create_prefab";
        public string Description =>
            "Сохраняет объект сцены как префаб. Параметры: object_name, path " +
            "(по умолчанию Assets/Prefabs/<name>.prefab).";
        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                object_name = new { type = "string" },
                path = new { type = "string", description = "Путь для сохранения префаба" }
            },
            required = new[] { "object_name" }
        };

        public Task<string> ExecuteAsync(string jsonArguments)
        {
            var args = JObject.Parse(jsonArguments ?? "{}");
            string objName = args["object_name"]?.ToString();
            string path = args["path"]?.ToString();

            var go = GameObject.Find(objName);
            if (go == null) return Task.FromResult($"Объект '{objName}' не найден.");

            if (string.IsNullOrEmpty(path))
                path = $"Assets/Prefabs/{go.name}.prefab";

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            bool ok;
            var prefab = PrefabUtility.SaveAsPrefabAssetAndConnect(go, path, InteractionMode.UserAction, out ok);
            AssetDatabase.Refresh();

            return Task.FromResult(ok
                ? $"Префаб создан: {path}"
                : $"Не удалось создать префаб из '{objName}'.");
        }
    }

    // =============================================================
    //  set_light
    // =============================================================
    public sealed class SetLightTool : IDeepSeekTool
    {
        public string Name => "set_light";
        public string Description =>
            "Настраивает Light на объекте. Параметры: object_name, intensity, color, " +
            "range, type (Directional/Point/Spot).";
        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                object_name = new { type = "string" },
                intensity = new { type = "number", description = "0..8" },
                color = new { type = "string", description = "#RRGGBB" },
                range = new { type = "number" },
                type = new { type = "string", description = "Directional|Point|Spot" }
            },
            required = new[] { "object_name" }
        };

        public Task<string> ExecuteAsync(string jsonArguments)
        {
            var args = JObject.Parse(jsonArguments ?? "{}");
            string objName = args["object_name"]?.ToString();
            var go = GameObject.Find(objName);
            if (go == null) return Task.FromResult($"Объект '{objName}' не найден.");

            var light = go.GetComponent<Light>();
            if (light == null) return Task.FromResult($"На '{objName}' нет компонента Light.");

            Undo.RecordObject(light, "Set light");

            if (args["intensity"] != null)
                light.intensity = args["intensity"].ToObject<float>();

            if (args["color"] != null)
            {
                string cstr = args["color"].ToString();
                if (ColorUtility.TryParseHtmlString(cstr, out var c))
                    light.color = c;
            }

            if (args["range"] != null)
                light.range = args["range"].ToObject<float>();

            if (args["type"] != null)
            {
                string t = args["type"].ToString().ToLowerInvariant();
                if (t == "directional") light.type = LightType.Directional;
                else if (t == "point") light.type = LightType.Point;
                else if (t == "spot") light.type = LightType.Spot;
            }

            EditorUtility.SetDirty(light);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
            return Task.FromResult($"Свет настроен на '{objName}'.");
        }
    }
}