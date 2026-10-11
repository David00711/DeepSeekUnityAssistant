using System.Collections.Generic;
using UnityEngine;

namespace DeepSeekUnity.Editor
{
    /// <summary>
    /// Хранит набор часто используемых промптов.
    /// Создаётся через Assets → Create → DeepSeek → Macros.
    /// </summary>
    [CreateAssetMenu(fileName = "DeepSeekMacros", menuName = "DeepSeek/Macros")]
    public sealed class DeepSeekMacros : ScriptableObject
    {
        [System.Serializable]
        public sealed class Macro
        {
            public string name = "New macro";
            [TextArea(2, 6)]
            public string prompt = "";
        }

        public List<Macro> items = new();

        private static DeepSeekMacros _cached;

        /// <summary>
        /// Возвращает макросы. Если в проекте нет файла макросов —
        /// возвращает встроенные дефолты.
        /// </summary>
        public static DeepSeekMacros Load()
        {
            if (_cached != null) return _cached;

            var guids = UnityEditor.AssetDatabase.FindAssets("t:DeepSeekMacros");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                _cached = UnityEditor.AssetDatabase.LoadAssetAtPath<DeepSeekMacros>(path);
                if (_cached != null && _cached.items.Count > 0) return _cached;
            }

            // Дефолт: создаём временный ассет в памяти
            _cached = CreateInstance<DeepSeekMacros>();
            _cached.items = DefaultMacros();
            return _cached;
        }

        /// <summary>
        /// Создаёт .asset файл в проекте (если ещё нет) и перезагружает кэш.
        /// </summary>
        public static DeepSeekMacros CreateAssetFile()
        {
            const string dir = "Assets/DeepSeekAssistant";
            if (!UnityEditor.AssetDatabase.IsValidFolder(dir))
                UnityEditor.AssetDatabase.CreateFolder("Assets", "DeepSeekAssistant");

            string path = $"{dir}/DeepSeekMacros.asset";
            var existing = UnityEditor.AssetDatabase.LoadAssetAtPath<DeepSeekMacros>(path);
            if (existing != null)
            {
                _cached = existing;
                return existing;
            }

            var asset = CreateInstance<DeepSeekMacros>();
            asset.items = DefaultMacros();

            UnityEditor.AssetDatabase.CreateAsset(asset, path);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();

            _cached = asset;
            return asset;
        }

        private static List<Macro> DefaultMacros()
        {
            return new List<Macro>
            {
                new Macro
                {
                    name = "🌲 Разбросать деревья",
                    prompt = "Создай куб 1×3×1 названный Tree, затем разбросай 100 копий в радиусе 150 метров вокруг 0 0"
                },
                new Macro
                {
                    name = "⛰ Прижать всё к земле",
                    prompt = "Прижми все объекты на сцене к поверхности"
                },
                new Macro
                {
                    name = "🏠 Быстрый домик",
                    prompt = "Создай простой дом: куб 6×4×8 (стены), плоский куб 8×0.2×10 (крыша), расположи их на рельефе в точке 0 0"
                },
                new Macro
                {
                    name = "📦 Заспавнить лут",
                    prompt = "Размести 30 объектов из папки Assets/DeepSeekAssistant/TestLoot/ на рельефе в радиусе 60 метров вокруг 0 0"
                },
                new Macro
                {
                    name = "🔍 Осмотреть сцену",
                    prompt = "Осмотри сцену: перечисли объекты, найди ошибки, проверь дубликаты по имени"
                },
                new Macro
                {
                    name = "📸 Скриншот + анализ",
                    prompt = "Сними скриншот Game View и опиши, что видно (сохрани в Library/DeepSeek/screenshots)"
                },
            };
        }

        public static void ClearCache() => _cached = null;
    }
}