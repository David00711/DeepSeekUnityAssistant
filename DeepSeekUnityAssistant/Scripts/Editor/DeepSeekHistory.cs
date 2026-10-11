using System;
using System.Collections.Generic;
using System.IO;
using DeepSeekUnity.Runtime;
using Newtonsoft.Json;
using UnityEngine;

namespace DeepSeekUnity.Editor
{
    /// <summary>
    /// Сохранение и загрузка истории диалога в Library/DeepSeek/history.json.
    /// </summary>
    public static class DeepSeekHistory
    {
        private const string DirName = "DeepSeek";
        private const string FileName = "history.json";

        private static string DirPath =>
            Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library", DirName);

        private static string FilePath => Path.Combine(DirPath, FileName);

        [Serializable]
        private sealed class HistoryData
        {
            public string savedAt;
            public int version = 1;
            public List<ChatMessage> messages = new();
        }

        /// <summary>Сохраняет историю на диск. Возвращает true при успехе.</summary>
        public static bool Save(List<ChatMessage> messages)
        {
            if (messages == null) return false;

            try
            {
                if (!Directory.Exists(DirPath))
                    Directory.CreateDirectory(DirPath);

                var data = new HistoryData
                {
                    savedAt = DateTime.UtcNow.ToString("o"),
                    messages = new List<ChatMessage>(messages)
                };

                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                File.WriteAllText(FilePath, json, System.Text.Encoding.UTF8);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[DeepSeek] Save history error: {e.Message}");
                return false;
            }
        }

        /// <summary>Загружает историю с диска. Возвращает пустой список, если файла нет.</summary>
        public static List<ChatMessage> Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return new List<ChatMessage>();

                string json = File.ReadAllText(FilePath, System.Text.Encoding.UTF8);
                var data = JsonConvert.DeserializeObject<HistoryData>(json);
                return data?.messages ?? new List<ChatMessage>();
            }
            catch (Exception e)
            {
                Debug.LogError($"[DeepSeek] Load history error: {e.Message}");
                return new List<ChatMessage>();
            }
        }

        /// <summary>Удаляет файл истории.</summary>
        public static void Clear()
        {
            try
            {
                if (File.Exists(FilePath))
                    File.Delete(FilePath);
            }
            catch (Exception e)
            {
                Debug.LogError($"[DeepSeek] Clear history error: {e.Message}");
            }
        }

        /// <summary>Есть ли сохранённая история.</summary>
        public static bool Exists => File.Exists(FilePath);

        /// <summary>Путь к файлу (для отладки).</summary>
        public static string DebugPath => FilePath;
    }
}