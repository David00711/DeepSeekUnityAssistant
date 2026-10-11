using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace DeepSeekUnity.Editor.Tools
{
    // =============================================================
    //  read_script — прочитать .cs файл
    // =============================================================
    public sealed class ReadScriptTool : IDeepSeekTool
    {
        public string Name => "read_script";
        public string Description =>
            "Читает содержимое файла скрипта. Параметр: path " +
            "(например, Assets/Scripts/Player.cs).";

        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                path = new { type = "string", description = "Путь к файлу относительно корня проекта" }
            },
            required = new[] { "path" }
        };

        public Task<string> ExecuteAsync(string jsonArguments)
        {
            var args = JObject.Parse(jsonArguments ?? "{}");
            string path = args["path"]?.ToString();

            if (string.IsNullOrEmpty(path))
                return Task.FromResult("Ошибка: не указан path.");

            if (!File.Exists(path))
                return Task.FromResult($"Файл '{path}' не найден.");

            string content = File.ReadAllText(path, Encoding.UTF8);
            return Task.FromResult(content);
        }
    }

    // =============================================================
    //  create_script — создать новый .cs файл
    // =============================================================
    public sealed class CreateScriptTool : IDeepSeekTool
    {
        public string Name => "create_script";
        public string Description =>
            "Создаёт новый .cs файл с указанным содержимым. Параметры: path, content.";

        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                path = new { type = "string", description = "Путь для сохранения (Assets/...)" },
                content = new { type = "string", description = "Исходный код скрипта" }
            },
            required = new[] { "path", "content" }
        };

        public Task<string> ExecuteAsync(string jsonArguments)
        {
            var args = JObject.Parse(jsonArguments ?? "{}");
            string path = args["path"]?.ToString();
            string content = args["content"]?.ToString();

            if (string.IsNullOrEmpty(path) || content == null)
                return Task.FromResult("Ошибка: нужны path и content.");

            string dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            File.WriteAllText(path, content, Encoding.UTF8);
            UnityEditor.AssetDatabase.ImportAsset(path);
            UnityEditor.AssetDatabase.Refresh();

            return Task.FromResult($"Создан скрипт: {path}");
        }
    }
}