using System;
using System.Collections.Generic;
using System.IO;
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
    //  move_asset — переместить/переименовать ассет
    // =============================================================
    public sealed class MoveAssetTool : IDeepSeekTool
    {
        public string Name => "move_asset";
        public string Description =>
            "Перемещает или переименовывает ассет. Параметры: from (путь), to (путь). " +
            "Работает и для папок. Создаёт конечную папку, если её нет.";

        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                from = new { type = "string", description = "Текущий путь (Assets/...)" },
                to = new { type = "string", description = "Новый путь (Assets/...)" }
            },
            required = new[] { "from", "to" }
        };

        public Task<string> ExecuteAsync(string jsonArguments)
        {
            var args = JObject.Parse(jsonArguments ?? "{}");
            string from = args["from"]?.ToString();
            string to = args["to"]?.ToString();

            if (string.IsNullOrEmpty(from) || string.IsNullOrEmpty(to))
                return Task.FromResult("Ошибка: нужны from и to.");

            if (!File.Exists(from) && !Directory.Exists(from))
                return Task.FromResult($"Ассет '{from}' не найден.");

            string toDir = Path.GetDirectoryName(to).Replace('\\', '/');
            if (!string.IsNullOrEmpty(toDir) && !AssetDatabase.IsValidFolder(toDir))
            {
                Directory.CreateDirectory(toDir);
                AssetDatabase.Refresh();
            }

            string error = AssetDatabase.MoveAsset(from, to);
            if (!string.IsNullOrEmpty(error))
                return Task.FromResult($"Ошибка перемещения: {error}");

            AssetDatabase.Refresh();
            return Task.FromResult($"Перемещено: {from} → {to}");
        }
    }

    // =============================================================
    //  duplicate_object — дубликат объекта сцены
    // =============================================================
    public sealed class DuplicateObjectTool : IDeepSeekTool
    {
        public string Name => "duplicate_object";
        public string Description =>
            "Создаёт копию объекта сцены. Параметры: name, new_name (опционально), " +
            "position (опционально).";

        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                name = new { type = "string", description = "Имя объекта для копирования" },
                new_name = new { type = "string", description = "Имя копии (по умолчанию <name>_Copy)" },
                position = new
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

        public Task<string> ExecuteAsync(string jsonArguments)
        {
            var args = JObject.Parse(jsonArguments ?? "{}");
            string name = args["name"]?.ToString();
            if (string.IsNullOrEmpty(name))
                return Task.FromResult("Ошибка: не указано name.");

            var source = GameObject.Find(name);
            if (source == null)
                return Task.FromResult($"Объект '{name}' не найден.");

            var copy = UnityEngine.Object.Instantiate(source);
            copy.name = args["new_name"]?.ToString() ?? (source.name + "_Copy");

            // Сдвигаем на +1 по X, если позиция не задана
            if (args["position"] is JObject p)
            {
                copy.transform.position = new Vector3(
                    p["x"]?.ToObject<float>() ?? source.transform.position.x,
                    p["y"]?.ToObject<float>() ?? source.transform.position.y,
                    p["z"]?.ToObject<float>() ?? source.transform.position.z);
            }
            else
            {
                copy.transform.position = source.transform.position + new Vector3(1f, 0f, 0f);
            }

            Undo.RegisterCreatedObjectUndo(copy, $"Duplicate {name}");
            Selection.activeGameObject = copy;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            return Task.FromResult($"Создана копия: '{copy.name}'.");
        }
    }

    // =============================================================
    //  align_to_ground — прижать объект к поверхности
    // =============================================================
    public sealed class AlignToGroundTool : IDeepSeekTool
    {
        public string Name => "align_to_ground";
        public string Description =>
            "Прижимает объект к поверхности под ним (raycast вниз). " +
            "Параметры: name, offset (смещение по Y, по умолчанию 0), " +
            "raycast_height (по умолчанию 500).";

        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                name = new { type = "string", description = "Имя объекта" },
                offset = new { type = "number", description = "Смещение по Y" },
                raycast_height = new { type = "number", description = "Высота луча (по умолчанию 500)" }
            },
            required = new[] { "name" }
        };

        public Task<string> ExecuteAsync(string jsonArguments)
        {
            var args = JObject.Parse(jsonArguments ?? "{}");
            string name = args["name"]?.ToString();
            if (string.IsNullOrEmpty(name))
                return Task.FromResult("Ошибка: не указано name.");

            var go = GameObject.Find(name);
            if (go == null)
                return Task.FromResult($"Объект '{name}' не найден.");

            float offset = args["offset"]?.ToObject<float>() ?? 0f;
            float height = args["raycast_height"]?.ToObject<float>() ?? 500f;

            Vector3 origin = go.transform.position + Vector3.up * height;

            // Игнорируем сам объект и его детей — чтобы луч не попал в собственный коллайдер
            var colliders = go.GetComponentsInChildren<Collider>();
            int layer = go.layer;
            go.layer = 2; // Ignore Raycast
            foreach (var c in colliders) c.enabled = false;

            bool hitFound = Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                                             height * 2f, ~0, QueryTriggerInteraction.Ignore);

            // Возвращаем как было
            go.layer = layer;
            foreach (var c in colliders) c.enabled = true;

            if (!hitFound)
                return Task.FromResult($"Под '{name}' не найдено поверхности.");

            Undo.RecordObject(go.transform, "Align to ground");
            Vector3 newPos = hit.point + Vector3.up * offset;
            go.transform.position = newPos;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            return Task.FromResult(
                $"'{name}' прижат к земле. Позиция: {newPos}, поверхность: {hit.collider.name}.");
        }
    }

    // =============================================================
    //  terrain_get_height — узнать высоту рельефа
    // =============================================================
    public sealed class TerrainGetHeightTool : IDeepSeekTool
    {
        public string Name => "terrain_get_height";
        public string Description =>
            "Возвращает высоту рельефа в заданной точке мира. " +
            "Параметры: x, z (мировые координаты).";

        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                x = new { type = "number" },
                z = new { type = "number" }
            },
            required = new[] { "x", "z" }
        };

        public Task<string> ExecuteAsync(string jsonArguments)
        {
            var args = JObject.Parse(jsonArguments ?? "{}");
            float x = args["x"]?.ToObject<float>() ?? 0f;
            float z = args["z"]?.ToObject<float>() ?? 0f;

            var terrain = FindTerrainAt(x, z);
            if (terrain == null)
                return Task.FromResult($"Terrain под точкой ({x}, {z}) не найден.");

            float y = terrain.SampleHeight(new Vector3(x, 0, z)) + terrain.transform.position.y;
            return Task.FromResult($"Высота в ({x}, {z}): Y = {y:F2} (terrain '{terrain.name}')");
        }

        private static Terrain FindTerrainAt(float x, float z)
        {
            var terrains = Terrain.activeTerrains;
            Vector3 p = new Vector3(x, 0, z);

            foreach (var t in terrains)
            {
                if (t == null || t.terrainData == null) continue;
                Vector3 pos = t.transform.position;
                Vector3 size = t.terrainData.size;

                if (p.x >= pos.x && p.x <= pos.x + size.x &&
                    p.z >= pos.z && p.z <= pos.z + size.z)
                    return t;
            }
            return null;
        }
    }

    // =============================================================
    //  terrain_place_on_surface — разместить объект на рельефе
    // =============================================================
    public sealed class TerrainPlaceOnSurfaceTool : IDeepSeekTool
    {
        public string Name => "terrain_place_on_surface";
        public string Description =>
            "Помещает объект на поверхность рельефа в заданной XZ-точке. " +
            "Параметры: name, x, z, offset (по Y, по умолчанию 0).";

        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                name = new { type = "string" },
                x = new { type = "number" },
                z = new { type = "number" },
                offset = new { type = "number" }
            },
            required = new[] { "name", "x", "z" }
        };

        public Task<string> ExecuteAsync(string jsonArguments)
        {
            var args = JObject.Parse(jsonArguments ?? "{}");
            string name = args["name"]?.ToString();
            float x = args["x"]?.ToObject<float>() ?? 0f;
            float z = args["z"]?.ToObject<float>() ?? 0f;
            float offset = args["offset"]?.ToObject<float>() ?? 0f;

            var go = GameObject.Find(name);
            if (go == null) return Task.FromResult($"Объект '{name}' не найден.");

            var terrain = FindTerrainAt(x, z);
            if (terrain == null)
                return Task.FromResult($"Terrain под точкой ({x}, {z}) не найден.");

            float y = terrain.SampleHeight(new Vector3(x, 0, z)) + terrain.transform.position.y;

            Undo.RecordObject(go.transform, "Place on terrain");
            go.transform.position = new Vector3(x, y + offset, z);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            return Task.FromResult($"'{name}' размещён на рельефе в ({x}, {y:F2}, {z}).");
        }

        private static Terrain FindTerrainAt(float x, float z)
        {
            var p = new Vector3(x, 0, z);
            foreach (var t in Terrain.activeTerrains)
            {
                if (t == null || t.terrainData == null) continue;
                var pos = t.transform.position;
                var size = t.terrainData.size;
                if (p.x >= pos.x && p.x <= pos.x + size.x &&
                    p.z >= pos.z && p.z <= pos.z + size.z)
                    return t;
            }
            return null;
        }
    }

    // =============================================================
    //  scatter_objects — рассыпать объекты по рельефу
    // =============================================================
    public sealed class ScatterObjectsTool : IDeepSeekTool
    {
        public string Name => "scatter_objects";
        public string Description =>
            "Создаёт N копий объекта в случайных точках вокруг центра. " +
            "Параметры: name (шаблон), count, radius, center_x, center_z, " +
            "align_to_ground (bool, по умолчанию true), name_prefix (опционально).";

        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                name = new { type = "string", description = "Объект-шаблон" },
                count = new { type = "integer", description = "Сколько копий создать" },
                radius = new { type = "number", description = "Радиус разброса" },
                center_x = new { type = "number" },
                center_z = new { type = "number" },
                align_to_ground = new { type = "boolean" },
                name_prefix = new { type = "string" }
            },
            required = new[] { "name", "count", "radius" }
        };

        public Task<string> ExecuteAsync(string jsonArguments)
        {
            var args = JObject.Parse(jsonArguments ?? "{}");
            string name = args["name"]?.ToString();
            if (string.IsNullOrEmpty(name))
                return Task.FromResult("Ошибка: не указано name.");

            var template = GameObject.Find(name);
            if (template == null)
                return Task.FromResult($"Шаблон '{name}' не найден.");

            int count = args["count"]?.ToObject<int>() ?? 10;
            float radius = args["radius"]?.ToObject<float>() ?? 20f;
            float cx = args["center_x"]?.ToObject<float>() ?? template.transform.position.x;
            float cz = args["center_z"]?.ToObject<float>() ?? template.transform.position.z;
            bool align = args["align_to_ground"]?.ToObject<bool>() ?? true;
            string prefix = args["name_prefix"]?.ToString() ?? (template.name + "_");

            var parent = new GameObject($"Scatter_{template.name}").transform;
            Undo.RegisterCreatedObjectUndo(parent.gameObject, "Scatter parent");

            int created = 0;
            int attempts = 0;

            while (created < count && attempts < count * 20)
            {
                attempts++;

                Vector2 rnd = UnityEngine.Random.insideUnitCircle * radius;
                float x = cx + rnd.x;
                float z = cz + rnd.y;

                // Получаем высоту
                float y = template.transform.position.y;

                if (align)
                {
                    var terrain = FindTerrainAt(x, z);
                    if (terrain != null)
                        y = terrain.SampleHeight(new Vector3(x, 0, z)) + terrain.transform.position.y;
                    else
                    {
                        // fallback — raycast
                        var origin = new Vector3(x, 500f, z);
                        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                                            1000f, ~0, QueryTriggerInteraction.Ignore))
                            y = hit.point.y;
                    }
                }

                var copy = UnityEngine.Object.Instantiate(template);
                copy.name = $"{prefix}{created + 1:D3}";
                copy.transform.position = new Vector3(x, y, z);
                copy.transform.SetParent(parent, true);

                // Случайный поворот по Y — чтобы не выглядело однообразно
                copy.transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);

                Undo.RegisterCreatedObjectUndo(copy, $"Scatter {copy.name}");
                created++;
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            return Task.FromResult(
                $"Создано {created} копий '{template.name}' в радиусе {radius}м " +
                $"вокруг ({cx}, {cz}). Родитель: '{parent.name}'.");
        }

        private static Terrain FindTerrainAt(float x, float z)
        {
            var p = new Vector3(x, 0, z);
            foreach (var t in Terrain.activeTerrains)
            {
                if (t == null || t.terrainData == null) continue;
                var pos = t.transform.position;
                var size = t.terrainData.size;
                if (p.x >= pos.x && p.x <= pos.x + size.x &&
                    p.z >= pos.z && p.z <= pos.z + size.z)
                    return t;
            }
            return null;
        }
    }

    // =============================================================
    //  spawn_loot_on_terrain — рассыпать предметы-лут на рельефе
    // =============================================================
    public sealed class SpawnLootOnTerrainTool : IDeepSeekTool
    {
        public string Name => "spawn_loot_on_terrain";
        public string Description =>
            "Рассыпает указанный предмет из папки Assets/.../Prefabs на рельефе. " +
            "Параметры: prefab_path (путь к префабу), count, radius, center_x, center_z. " +
            "Каждый экземпляр получает имя EDITOR_LOOT_<prefab>_N (префикс для очистки).";

        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                prefab_path = new { type = "string", description = "Путь к префабу в Assets/..." },
                count = new { type = "integer" },
                radius = new { type = "number" },
                center_x = new { type = "number" },
                center_z = new { type = "number" }
            },
            required = new[] { "prefab_path", "count", "radius" }
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

            int count = args["count"]?.ToObject<int>() ?? 20;
            float radius = args["radius"]?.ToObject<float>() ?? 50f;
            float cx = args["center_x"]?.ToObject<float>() ?? 0f;
            float cz = args["center_z"]?.ToObject<float>() ?? 0f;

            var parent = new GameObject($"EDITOR_LOOT_{prefab.name}").transform;
            Undo.RegisterCreatedObjectUndo(parent.gameObject, "Loot parent");

            int placed = 0;
            int attempts = 0;

            while (placed < count && attempts < count * 30)
            {
                attempts++;

                Vector2 rnd = UnityEngine.Random.insideUnitCircle * radius;
                float x = cx + rnd.x;
                float z = cz + rnd.y;

                var terrain = FindTerrainAt(x, z);
                if (terrain == null) continue;

                float y = terrain.SampleHeight(new Vector3(x, 0, z)) + terrain.transform.position.y + 0.1f;

                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                inst.transform.position = new Vector3(x, y, z);
                inst.transform.rotation = Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f);

                Undo.RegisterCreatedObjectUndo(inst, $"Loot {inst.name}");
                placed++;
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            return Task.FromResult(
                $"Размещено {placed} экземпляров '{prefab.name}' в радиусе {radius}м " +
                $"вокруг ({cx}, {cz}). Родитель: '{parent.name}'. " +
                $"Для очистки — правый клик по родителю → Clear editor preview.");
        }

        private static Terrain FindTerrainAt(float x, float z)
        {
            var p = new Vector3(x, 0, z);
            foreach (var t in Terrain.activeTerrains)
            {
                if (t == null || t.terrainData == null) continue;
                var pos = t.transform.position;
                var size = t.terrainData.size;
                if (p.x >= pos.x && p.x <= pos.x + size.x &&
                    p.z >= pos.z && p.z <= pos.z + size.z)
                    return t;
            }
            return null;
        }
    }

    // =============================================================
    //  list_terrains — список загруженных Terrains
    // =============================================================
    public sealed class ListTerrainsTool : IDeepSeekTool
    {
        public string Name => "list_terrains";
        public string Description => "Показывает все загруженные Terrains в сцене.";

        public object ParametersSchema => new
        {
            type = "object",
            properties = new { },
            required = Array.Empty<string>()
        };

        public Task<string> ExecuteAsync(string jsonArguments)
        {
            var terrains = Terrain.activeTerrains;
            if (terrains.Length == 0)
                return Task.FromResult("В сцене нет активных Terrains.");

            var sb = new StringBuilder();
            sb.AppendLine($"Загружено Terrains: {terrains.Length}");
            foreach (var t in terrains)
            {
                if (t == null) continue;
                var pos = t.transform.position;
                var size = t.terrainData != null ? t.terrainData.size : Vector3.zero;
                sb.AppendLine($"- {t.name} @ ({pos.x:F0}, {pos.z:F0}), size {size.x:F0}x{size.z:F0}");
            }
            return Task.FromResult(sb.ToString());
        }
    }
}