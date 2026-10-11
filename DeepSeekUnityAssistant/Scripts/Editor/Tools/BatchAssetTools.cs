using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace DeepSeekUnity.Editor.Tools
{
    // =============================================================
    //  batch_rename — переименовать все ассеты в папке
    // =============================================================
    public sealed class BatchRenameTool : IDeepSeekTool
    {
        public string Name => "batch_rename";
        public string Description =>
            "Переименовывает все ассеты в папке с заменой подстроки. " +
            "Параметры: folder (путь), search (что искать), replace (на что менять).";

        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                folder = new { type = "string" },
                search = new { type = "string" },
                replace = new { type = "string" }
            },
            required = new[] { "folder", "search", "replace" }
        };

        public Task<string> ExecuteAsync(string jsonArguments)
        {
            var args = JObject.Parse(jsonArguments ?? "{}");
            string folder = args["folder"]?.ToString();
            string search = args["search"]?.ToString();
            string replace = args["replace"]?.ToString() ?? "";

            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
                return Task.FromResult($"Папка '{folder}' не найдена.");

            if (string.IsNullOrEmpty(search))
                return Task.FromResult("Ошибка: не указан search.");

            var guids = AssetDatabase.FindAssets("", new[] { folder });
            int renamed = 0;

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string name = Path.GetFileNameWithoutExtension(path);
                string ext = Path.GetExtension(path);

                if (!name.Contains(search)) continue;

                string newName = name.Replace(search, replace);
                if (string.IsNullOrEmpty(newName)) newName = "unnamed";

                string newPath = Path.Combine(Path.GetDirectoryName(path), newName + ext)
                    .Replace('\\', '/');

                string err = AssetDatabase.MoveAsset(path, newPath);
                if (string.IsNullOrEmpty(err)) renamed++;
            }

            AssetDatabase.Refresh();
            return Task.FromResult($"Переименовано: {renamed}.");
        }
    }

    // =============================================================
    //  batch_texture_import — изменить импорт-настройки текстур
    // =============================================================
    public sealed class BatchTextureImportTool : IDeepSeekTool
    {
        public string Name => "batch_texture_import";
        public string Description =>
            "Меняет настройки импорта всех текстур в папке. " +
            "Параметры: folder, max_size (32/64/128/256/512/1024/2048/4096), " +
            "compression (None/Compressed), readable (true/false).";

        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                folder = new { type = "string" },
                max_size = new { type = "integer" },
                compression = new { type = "string" },
                readable = new { type = "boolean" }
            },
            required = new[] { "folder" }
        };

        public Task<string> ExecuteAsync(string jsonArguments)
        {
            var args = JObject.Parse(jsonArguments ?? "{}");
            string folder = args["folder"]?.ToString();
            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
                return Task.FromResult($"Папка '{folder}' не найдена.");

            int maxSize = args["max_size"]?.ToObject<int>() ?? 0;
            string compression = args["compression"]?.ToString();
            bool? readable = args["readable"]?.ToObject<bool>();

            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folder });
            int changed = 0;

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                if (maxSize > 0) importer.maxTextureSize = maxSize;

                if (!string.IsNullOrEmpty(compression))
                {
                    importer.textureCompression = compression.ToLowerInvariant() == "none"
                        ? TextureImporterCompression.Uncompressed
                        : TextureImporterCompression.Compressed;
                }

                if (readable.HasValue)
                    importer.isReadable = readable.Value;

                EditorUtility.SetDirty(importer);
                importer.SaveAndReimport();
                changed++;
            }

            AssetDatabase.Refresh();
            return Task.FromResult($"Изменено текстур: {changed}.");
        }
    }

    // =============================================================
    //  batch_change_shader — сменить шейдер у материалов в папке
    // =============================================================
    public sealed class BatchChangeShaderTool : IDeepSeekTool
    {
        public string Name => "batch_change_shader";
        public string Description =>
            "Меняет шейдер у всех материалов в папке. " +
            "Параметры: folder, shader_name (Standard, Universal Render Pipeline/Lit и т.д.).";

        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                folder = new { type = "string" },
                shader_name = new { type = "string" }
            },
            required = new[] { "folder", "shader_name" }
        };

        public Task<string> ExecuteAsync(string jsonArguments)
        {
            var args = JObject.Parse(jsonArguments ?? "{}");
            string folder = args["folder"]?.ToString();
            string shaderName = args["shader_name"]?.ToString();

            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
                return Task.FromResult($"Папка '{folder}' не найдена.");

            var shader = Shader.Find(shaderName);
            if (shader == null)
                return Task.FromResult($"Шейдер '{shaderName}' не найден.");

            var guids = AssetDatabase.FindAssets("t:Material", new[] { folder });
            int changed = 0;

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;

                Undo.RecordObject(mat, "Batch change shader");
                mat.shader = shader;
                EditorUtility.SetDirty(mat);
                changed++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return Task.FromResult($"Сменён шейдер у {changed} материалов.");
        }
    }

    // =============================================================
    //  find_duplicate_materials — найти дубликаты материалов
    // =============================================================
    public sealed class FindDuplicateMaterialsTool : IDeepSeekTool
    {
        public string Name => "find_duplicate_materials";
        public string Description =>
            "Находит материалы с одинаковыми настройками (шэйдер + цвет + main texture).";

        public object ParametersSchema => new
        {
            type = "object",
            properties = new { },
            required = Array.Empty<string>()
        };

        public Task<string> ExecuteAsync(string jsonArguments)
        {
            var guids = AssetDatabase.FindAssets("t:Material");
            var map = new Dictionary<string, List<string>>();

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;

                string key = $"{mat.shader.name}|{mat.color}|" +
                             $"{(mat.mainTexture != null ? AssetDatabase.GetAssetPath(mat.mainTexture) : "none")}";

                if (!map.ContainsKey(key)) map[key] = new List<string>();
                map[key].Add(path);
            }

            var sb = new StringBuilder();
            int groupsFound = 0;

            foreach (var kv in map)
            {
                if (kv.Value.Count <= 1) continue;
                groupsFound++;
                sb.AppendLine($"Группа ({kv.Value.Count} шт):");
                foreach (var p in kv.Value) sb.AppendLine($"  - {p}");
                sb.AppendLine();
            }

            if (groupsFound == 0)
                return Task.FromResult("Дубликатов не найдено.");

            return Task.FromResult($"Найдено групп дубликатов: {groupsFound}\n\n{sb}");
        }
    }

    // =============================================================
    //  find_assets_in_folder — простой список всех ассетов папки
    // =============================================================
    public sealed class FindAssetsInFolderTool : IDeepSeekTool
    {
        public string Name => "find_assets_in_folder";
        public string Description =>
            "Возвращает все ассеты в указанной папке. Параметры: folder, filter (опционально).";

        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                folder = new { type = "string" },
                filter = new { type = "string" }
            },
            required = new[] { "folder" }
        };

        public Task<string> ExecuteAsync(string jsonArguments)
        {
            var args = JObject.Parse(jsonArguments ?? "{}");
            string folder = args["folder"]?.ToString();
            string filter = args["filter"]?.ToString() ?? "";

            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
                return Task.FromResult($"Папка '{folder}' не найдена.");

            var guids = AssetDatabase.FindAssets(filter, new[] { folder });
            var sb = new StringBuilder();
            sb.AppendLine($"Всего ассетов: {guids.Length}");

            int shown = Mathf.Min(guids.Length, 60);
            for (int i = 0; i < shown; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                sb.AppendLine($"- {path}");
            }
            if (guids.Length > shown)
                sb.AppendLine($"... и ещё {guids.Length - shown}");

            return Task.FromResult(sb.ToString());
        }
    }
}