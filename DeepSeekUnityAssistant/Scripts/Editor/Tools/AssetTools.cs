using System;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace DeepSeekUnity.Editor.Tools
{
    // =============================================================
    //  find_assets — поиск ассетов в проекте
    // =============================================================
    public sealed class FindAssetsTool : IDeepSeekTool
    {
        public string Name => "find_assets";
        public string Description =>
            "Ищет ассеты в проекте. Параметры: query (строка поиска), " +
            "type (опционально, например 'Material', 'Prefab', 'Texture2D').";

        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                query = new { type = "string", description = "Поисковый запрос" },
                type = new { type = "string", description = "Тип ассета (опционально)" }
            },
            required = new[] { "query" }
        };

        public Task<string> ExecuteAsync(string jsonArguments)
        {
            var args = JObject.Parse(jsonArguments ?? "{}");
            string query = args["query"]?.ToString();
            string type = args["type"]?.ToString();

            if (string.IsNullOrEmpty(query))
                return Task.FromResult("Ошибка: нужен query.");

            string filter = string.IsNullOrEmpty(type)
                ? query
                : $"{query} t:{type}";

            var guids = AssetDatabase.FindAssets(filter);
            if (guids.Length == 0)
                return Task.FromResult($"Ничего не найдено по '{filter}'.");

            var sb = new StringBuilder();
            sb.AppendLine($"Найдено {guids.Length} ассетов:");

            int shown = Mathf.Min(guids.Length, 30);
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

    // =============================================================
    //  inspect_object — детальная информация об объекте сцены
    // =============================================================
    public sealed class InspectObjectTool : IDeepSeekTool
    {
        public string Name => "inspect_object";
        public string Description =>
            "Возвращает подробную информацию об объекте сцены: " +
            "компоненты, позиция, дети. Параметр: name.";

        public object ParametersSchema => new
        {
            type = "object",
            properties = new
            {
                name = new { type = "string", description = "Имя объекта" }
            },
            required = new[] { "name" }
        };

        public Task<string> ExecuteAsync(string jsonArguments)
        {
            var args = JObject.Parse(jsonArguments ?? "{}");
            string name = args["name"]?.ToString();

            if (string.IsNullOrEmpty(name))
                return Task.FromResult("Ошибка: не указано имя.");

            var go = GameObject.Find(name);
            if (go == null)
                return Task.FromResult($"Объект '{name}' не найден.");

            var sb = new StringBuilder();
            sb.AppendLine($"GameObject: {go.name}");
            sb.AppendLine($"Active: {go.activeSelf}, Tag: {go.tag}, Layer: {LayerMask.LayerToName(go.layer)}");
            sb.AppendLine($"Position: {go.transform.position}");
            sb.AppendLine($"Rotation: {go.transform.eulerAngles}");
            sb.AppendLine($"Scale: {go.transform.lossyScale}");
            sb.AppendLine("Components:");
            foreach (var c in go.GetComponents<Component>())
            {
                if (c == null) continue;
                sb.AppendLine($"  · {c.GetType().FullName}");
            }
            sb.AppendLine($"Children: {go.transform.childCount}");
            for (int i = 0; i < go.transform.childCount; i++)
                sb.AppendLine($"  · {go.transform.GetChild(i).name}");

            return Task.FromResult(sb.ToString());
        }
    }
}