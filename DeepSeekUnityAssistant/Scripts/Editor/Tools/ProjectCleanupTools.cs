using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
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
    //  find_empty_folders — найти пустые папки
    // =============================================================
    public sealed class FindEmptyFoldersTool : IDeepSeekTool
    {
        public string Name => "find_empty_folders";
        public string Description =>
            "Ищет пустые папки в проекте (в которых нет ни одного файла, кроме .meta). " +
            "Параметры: root (папка, по умолчанию Assets), delete (true чтобы удалить сразу).";

        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                root = new { type = "string", description = "Корневая папка (по умолчанию Assets)" },
                delete = new { type = "boolean", description = "Удалить пустые папки сразу" }
            },
            required = Array.Empty<string>()
        };

        public Task<string> ExecuteAsync(string jsonArguments)
        {
            var args = JObject.Parse(jsonArguments ?? "{}");
            string root = args["root"]?.ToString() ?? "Assets";
            bool delete = args["delete"]?.ToObject<bool>() ?? false;

            if (!AssetDatabase.IsValidFolder(root))
                return Task.FromResult($"Папка '{root}' не найдена.");

            var emptyFolders = new List<string>();
            CollectEmptyFolders(root, emptyFolders);

            if (emptyFolders.Count == 0)
                return Task.FromResult("Пустых папок не найдено.");

            var sb = new StringBuilder();
            sb.AppendLine($"Найдено пустых папок: {emptyFolders.Count}");

            if (!delete)
            {
                foreach (var p in emptyFolders)
                    sb.AppendLine($"- {p}");
                sb.AppendLine();
                sb.AppendLine("Чтобы удалить — передай delete=true");
            }
            else
            {
                int deleted = 0;
                // Удаляем в порядке убывания длины, чтобы сначала удалялись вложенные
                emptyFolders.Sort((a, b) => b.Length.CompareTo(a.Length));

                foreach (var p in emptyFolders)
                {
                    if (AssetDatabase.DeleteAsset(p))
                    {
                        sb.AppendLine($"✗ Удалено: {p}");
                        deleted++;
                    }
                }
                AssetDatabase.Refresh();
                sb.AppendLine($"Итого удалено: {deleted}");
            }

            return Task.FromResult(sb.ToString());
        }

        private static void CollectEmptyFolders(string folder, List<string> result)
        {
            var subFolders = AssetDatabase.GetSubFolders(folder);

            foreach (var sub in subFolders)
            {
                CollectEmptyFolders(sub, result);
            }

            // Проверяем, пустая ли папка
            var guids = AssetDatabase.FindAssets("", new[] { folder });
            if (guids.Length == 0)
                result.Add(folder);
        }
    }

    // =============================================================
    //  find_missing_scripts — найти Missing Script
    // =============================================================
    public sealed class FindMissingScriptsTool : IDeepSeekTool
    {
        public string Name => "find_missing_scripts";
        public string Description =>
            "Ищет GameObject'ы со сломанными (missing) скриптами на текущей сцене. " +
            "Параметр: include_inactive (по умолчанию true).";

        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                include_inactive = new { type = "boolean" }
            },
            required = Array.Empty<string>()
        };

        public Task<string> ExecuteAsync(string jsonArguments)
        {
            var args = JObject.Parse(jsonArguments ?? "{}");
            bool includeInactive = args["include_inactive"]?.ToObject<bool>() ?? true;

            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();
            var sb = new StringBuilder();
            int total = 0;

            foreach (var root in roots)
            {
                var transforms = root.GetComponentsInChildren<Transform>(includeInactive);
                foreach (var t in transforms)
                {
                    var components = t.GetComponents<Component>();
                    int missing = 0;

                    foreach (var c in components)
                    {
                        if (c == null) missing++;
                    }

                    if (missing > 0)
                    {
                        sb.AppendLine($"- {GetPath(t.gameObject)} — {missing} missing");
                        total += missing;
                    }
                }
            }

            if (total == 0)
                return Task.FromResult("Missing script'ов не найдено. Сцена чистая.");

            return Task.FromResult(
                $"Найдено {total} missing-компонентов:\n\n{sb}");
        }

        private static string GetPath(GameObject go)
        {
            var sb = new StringBuilder(go.name);
            var t = go.transform.parent;
            while (t != null)
            {
                sb.Insert(0, t.name + "/");
                t = t.parent;
            }
            return sb.ToString();
        }
    }

    // =============================================================
    //  find_duplicate_files — найти дубликаты файлов
    // =============================================================
    public sealed class FindDuplicateFilesTool : IDeepSeekTool
    {
        public string Name => "find_duplicate_files";
        public string Description =>
            "Ищет файлы-дубликаты по хешу содержимого (MD5). Работает по всему проекту. " +
            "Параметры: root (папка, по умолчанию Assets), min_size_kb (мин. размер, по умолчанию 1).";

        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                root = new { type = "string" },
                min_size_kb = new { type = "integer" }
            },
            required = Array.Empty<string>()
        };

        public Task<string> ExecuteAsync(string jsonArguments)
        {
            var args = JObject.Parse(jsonArguments ?? "{}");
            string root = args["root"]?.ToString() ?? "Assets";
            int minSizeKb = args["min_size_kb"]?.ToObject<int>() ?? 1;

            if (!AssetDatabase.IsValidFolder(root))
                return Task.FromResult($"Папка '{root}' не найдена.");

            // Ищем все файлы
            var files = Directory.GetFiles(root, "*.*", SearchOption.AllDirectories);
            var hashMap = new Dictionary<string, List<string>>();
            long minBytes = minSizeKb * 1024;

            foreach (var file in files)
            {
                if (file.EndsWith(".meta")) continue;

                var fi = new FileInfo(file);
                if (fi.Length < minBytes) continue;

                try
                {
                    string hash = ComputeHash(file);
                    if (!hashMap.ContainsKey(hash))
                        hashMap[hash] = new List<string>();
                    hashMap[hash].Add(file.Replace('\\', '/'));
                }
                catch { /* пропускаем недоступные */ }
            }

            var sb = new StringBuilder();
            int groups = 0;
            long totalWaste = 0;

            foreach (var kv in hashMap)
            {
                if (kv.Value.Count <= 1) continue;
                groups++;

                var fi = new FileInfo(kv.Value[0]);
                long waste = fi.Length * (kv.Value.Count - 1);
                totalWaste += waste;

                sb.AppendLine($"Группа ({kv.Value.Count} копий, ~{fi.Length / 1024} KB каждая):");
                foreach (var p in kv.Value)
                    sb.AppendLine($"  - {p}");
                sb.AppendLine();
            }

            if (groups == 0)
                return Task.FromResult("Дубликатов не найдено.");

            sb.AppendLine($"Всего групп: {groups}");
            sb.AppendLine($"Потенциальная экономия: {totalWaste / 1024 / 1024} MB");

            return Task.FromResult(sb.ToString());
        }

        private static string ComputeHash(string path)
        {
            using var md5 = MD5.Create();
            using var stream = File.OpenRead(path);
            var hash = md5.ComputeHash(stream);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }
    }

    // =============================================================
    //  find_same_name_assets — файлы с одинаковым именем
    // =============================================================
    public sealed class FindSameNameAssetsTool : IDeepSeekTool
    {
        public string Name => "find_same_name_assets";
        public string Description =>
            "Находит ассеты с одинаковыми именами, но в разных папках. " +
            "Это частая причина путаницы в проекте.";

        public object ParametersSchema => new
        {
            type = "object",
            properties = new { },
            required = Array.Empty<string>()
        };

        public Task<string> ExecuteAsync(string jsonArguments)
        {
            var guids = AssetDatabase.FindAssets("");
            var byName = new Dictionary<string, List<string>>();

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetDatabase.IsValidFolder(path)) continue;

                string name = Path.GetFileNameWithoutExtension(path);
                if (!byName.ContainsKey(name))
                    byName[name] = new List<string>();
                byName[name].Add(path);
            }

            var sb = new StringBuilder();
            int groups = 0;

            foreach (var kv in byName)
            {
                if (kv.Value.Count <= 1) continue;
                groups++;
                sb.AppendLine($"'{kv.Key}' встречается {kv.Value.Count} раз:");
                foreach (var p in kv.Value)
                    sb.AppendLine($"  - {p}");
                sb.AppendLine();
            }

            if (groups == 0)
                return Task.FromResult("Ассетов с одинаковыми именами не найдено.");

            return Task.FromResult(
                $"Найдено групп с дублирующимися именами: {groups}\n\n{sb}");
        }
    }

    // =============================================================
    //  find_large_files — самые тяжёлые ассеты
    // =============================================================
    public sealed class FindLargeFilesTool : IDeepSeekTool
    {
        public string Name => "find_large_files";
        public string Description =>
            "Находит самые большие файлы в Assets. " +
            "Параметры: top_count (по умолчанию 20), min_size_mb (по умолчанию 1).";

        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                top_count = new { type = "integer" },
                min_size_mb = new { type = "integer" }
            },
            required = Array.Empty<string>()
        };

        public Task<string> ExecuteAsync(string jsonArguments)
        {
            var args = JObject.Parse(jsonArguments ?? "{}");
            int topCount = args["top_count"]?.ToObject<int>() ?? 20;
            long minSize = (args["min_size_mb"]?.ToObject<int>() ?? 1) * 1024L * 1024L;

            var files = Directory.GetFiles("Assets", "*.*", SearchOption.AllDirectories);
            var list = new List<(string path, long size)>();

            foreach (var file in files)
            {
                if (file.EndsWith(".meta")) continue;
                var fi = new FileInfo(file);
                if (fi.Length < minSize) continue;
                list.Add((file.Replace('\\', '/'), fi.Length));
            }

            if (list.Count == 0)
                return Task.FromResult($"Файлов больше {minSize / 1024 / 1024} MB не найдено.");

            list.Sort((a, b) => b.size.CompareTo(a.size));

            var sb = new StringBuilder();
            long total = 0;
            foreach (var f in list) total += f.size;

            sb.AppendLine($"Найдено файлов: {list.Count} (суммарно {total / 1024 / 1024} MB)");
            sb.AppendLine();

            int shown = Mathf.Min(list.Count, topCount);
            for (int i = 0; i < shown; i++)
            {
                var (path, size) = list[i];
                sb.AppendLine($"{i + 1,3}. {size / 1024 / 1024,5} MB — {path}");
            }

            if (list.Count > shown)
                sb.AppendLine($"\n... и ещё {list.Count - shown}");

            return Task.FromResult(sb.ToString());
        }
    }

    // =============================================================
    //  find_temp_files — временные и мусорные файлы
    // =============================================================
    public sealed class FindTempFilesTool : IDeepSeekTool
    {
        public string Name => "find_temp_files";
        public string Description =>
            "Ищет временные и мусорные файлы: .tmp, .bak, .orig, .ds_store, thumbs.db, " +
            "~$*, .swp и т.д. Параметр: delete (true чтобы удалить).";

        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                delete = new { type = "boolean" }
            },
            required = Array.Empty<string>()
        };

        public Task<string> ExecuteAsync(string jsonArguments)
        {
            var args = JObject.Parse(jsonArguments ?? "{}");
            bool delete = args["delete"]?.ToObject<bool>() ?? false;

            string[] patterns = {
                "*.tmp", "*.bak", "*.orig", "*.old", "*.swp",
                "*~", "*~$*", ".DS_Store", "Thumbs.db", "desktop.ini"
            };

            var found = new List<string>();
            foreach (var pattern in patterns)
            {
                try
                {
                    var matches = Directory.GetFiles("Assets", pattern, SearchOption.AllDirectories);
                    found.AddRange(matches);
                }
                catch { }
            }

            if (found.Count == 0)
                return Task.FromResult("Временных файлов не найдено.");

            var sb = new StringBuilder();
            sb.AppendLine($"Найдено временных файлов: {found.Count}");

            if (!delete)
            {
                foreach (var f in found)
                    sb.AppendLine($"- {f.Replace('\\', '/')}");
                sb.AppendLine("\nЧтобы удалить — передай delete=true");
            }
            else
            {
                int deleted = 0;
                foreach (var f in found)
                {
                    try
                    {
                        string assetPath = f.Replace('\\', '/');
                        AssetDatabase.DeleteAsset(assetPath);
                        deleted++;
                    }
                    catch { }
                }
                AssetDatabase.Refresh();
                sb.AppendLine($"Итого удалено: {deleted}");
            }

            return Task.FromResult(sb.ToString());
        }
    }

    // =============================================================
    //  find_files_outside_assets — файлы вне Assets/
    // =============================================================
    public sealed class FindFilesOutsideAssetsTool : IDeepSeekTool
    {
        public string Name => "find_files_outside_assets";
        public string Description =>
            "Ищет файлы скриптов и ассетов, лежащие вне папки Assets/ " +
            "(частая ошибка — забыл перенести в проект). Сканирует Assets/../";

        public object ParametersSchema => new
        {
            type = "object",
            properties = new { },
            required = Array.Empty<string>()
        };

        public Task<string> ExecuteAsync(string jsonArguments)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;

            // Папки, которые не считаем мусором
            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Assets", "Library", "Packages", "ProjectSettings",
                "Logs", "Temp", "obj", "UserSettings", "Build", "Builds",
                ".git", ".vs", ".vscode", ".idea", "bin", "node_modules"
            };

            var found = new List<string>();

            foreach (var dir in Directory.GetDirectories(projectRoot))
            {
                string name = Path.GetFileName(dir);
                if (allowed.Contains(name)) continue;

                // Ищем .cs и .unity
                try
                {
                    var csFiles = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories);
                    var unityFiles = Directory.GetFiles(dir, "*.unity", SearchOption.AllDirectories);

                    found.AddRange(csFiles);
                    found.AddRange(unityFiles);
                }
                catch { }
            }

            if (found.Count == 0)
                return Task.FromResult("Файлов вне Assets/ не найдено.");

            var sb = new StringBuilder();
            sb.AppendLine($"Файлов вне Assets/: {found.Count}");
            sb.AppendLine();

            int shown = Mathf.Min(found.Count, 40);
            for (int i = 0; i < shown; i++)
            {
                string rel = found[i].Substring(projectRoot.Length).TrimStart('\\', '/');
                sb.AppendLine($"- {rel}");
            }
            if (found.Count > shown)
                sb.AppendLine($"... и ещё {found.Count - shown}");

            return Task.FromResult(sb.ToString());
        }
    }

    // =============================================================
    //  analyze_project — общий отчёт по проекту
    // =============================================================
    public sealed class AnalyzeProjectTool : IDeepSeekTool
    {
        public string Name => "analyze_project";
        public string Description =>
            "Общий анализ проекта: количество ассетов по типам, размер, топ-5 крупных папок. " +
            "Хорошая точка входа для уборки.";

        public object ParametersSchema => new
        {
            type = "object",
            properties = new { },
            required = Array.Empty<string>()
        };

        public Task<string> ExecuteAsync(string jsonArguments)
        {
            var sb = new StringBuilder();

            // --- По типам ---
            var types = new[] { "Material", "Prefab", "Texture2D", "Script",
                                "ScriptableObject", "AudioClip", "AnimationClip",
                                "Scene", "Mesh", "Font" };

            sb.AppendLine("═══ АССЕТЫ ПО ТИПАМ ═══");
            foreach (var type in types)
            {
                var guids = AssetDatabase.FindAssets($"t:{type}");
                sb.AppendLine($"{type,-20} {guids.Length,6}");
            }
            sb.AppendLine();

            // --- Размер по папкам верхнего уровня ---
            sb.AppendLine("═══ РАЗМЕР ПАПОК (Assets/*) ═══");
            var folders = new List<(string path, long size)>();

            foreach (var sub in AssetDatabase.GetSubFolders("Assets"))
            {
                long size = GetFolderSize(sub);
                folders.Add((sub, size));
            }

            folders.Sort((a, b) => b.size.CompareTo(a.size));

            long total = 0;
            foreach (var f in folders) total += f.size;

            int shown = Mathf.Min(folders.Count, 15);
            for (int i = 0; i < shown; i++)
            {
                var (path, size) = folders[i];
                sb.AppendLine($"{size / 1024 / 1024,6} MB  {path}");
            }
            if (folders.Count > shown)
                sb.AppendLine($"... и ещё {folders.Count - shown} папок");

            sb.AppendLine($"\nВсего: {total / 1024 / 1024} MB");
            sb.AppendLine();

            // --- Сцена ---
            var scene = SceneManager.GetActiveScene();
            sb.AppendLine("═══ СЦЕНА ═══");
            sb.AppendLine($"Активная сцена: {scene.name}");

            int totalGO = 0, totalMissing = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                var transforms = root.GetComponentsInChildren<Transform>(true);
                totalGO += transforms.Length;

                foreach (var t in transforms)
                {
                    foreach (var c in t.GetComponents<Component>())
                        if (c == null) totalMissing++;
                }
            }
            sb.AppendLine($"GameObject'ов: {totalGO}");
            sb.AppendLine($"Missing-компонентов: {totalMissing}");

            return Task.FromResult(sb.ToString());
        }

        private static long GetFolderSize(string folder)
        {
            try
            {
                var files = Directory.GetFiles(folder, "*.*", SearchOption.AllDirectories);
                long size = 0;
                foreach (var f in files)
                {
                    if (f.EndsWith(".meta")) continue;
                    size += new FileInfo(f).Length;
                }
                return size;
            }
            catch { return 0; }
        }
    }

    // =============================================================
    //  delete_asset — удалить ассет или папку
    // =============================================================
    public sealed class DeleteAssetTool : IDeepSeekTool
    {
        public string Name => "delete_asset";
        public string Description =>
            "Удаляет ассет или папку. ОСТОРОЖНО — без возможности отмены (кроме Ctrl+Z). " +
            "Параметр: path. Требует confirm=true для безопасности.";

        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                path = new { type = "string" },
                confirm = new { type = "boolean", description = "Обязательно true" }
            },
            required = new[] { "path", "confirm" }
        };

        public Task<string> ExecuteAsync(string jsonArguments)
        {
            var args = JObject.Parse(jsonArguments ?? "{}");
            string path = args["path"]?.ToString();
            bool confirm = args["confirm"]?.ToObject<bool>() ?? false;

            if (string.IsNullOrEmpty(path))
                return Task.FromResult("Ошибка: не указан path.");

            if (!confirm)
                return Task.FromResult("Отказано: confirm не установлен в true.");

            if (path == "Assets" || path == "Assets/")
                return Task.FromResult("Отказано: нельзя удалить корень Assets.");

            if (!File.Exists(path) && !Directory.Exists(path))
                return Task.FromResult($"'{path}' не найден.");

            bool ok = AssetDatabase.DeleteAsset(path);
            AssetDatabase.Refresh();

            return Task.FromResult(ok
                ? $"Удалено: {path}"
                : $"Не удалось удалить: {path}");
        }
    }
}