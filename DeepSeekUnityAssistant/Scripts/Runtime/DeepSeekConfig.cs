using UnityEngine;

namespace DeepSeekUnity.Runtime
{
    /// <summary>
    /// Настройки DeepSeek.
    /// Создаётся через Assets → Create → DeepSeek → Config.
    /// </summary>
    [CreateAssetMenu(fileName = "DeepSeekConfig", menuName = "DeepSeek/Config")]
    public sealed class DeepSeekConfig : ScriptableObject
    {
        [Header("API")]
        [Tooltip("API-ключ DeepSeek. Получить: https://platform.deepseek.com/api_keys")]
        [SerializeField] private string apiKey = "";

        [Tooltip("Base URL. Не добавляй /v1 — SDK добавит сам.")]
        [SerializeField] private string baseUrl = "https://api.deepseek.com";

        [Tooltip("Модель: deepseek-chat (V3) или deepseek-reasoner (R1)")]
        [SerializeField] private string model = "deepseek-chat";

        [Header("Generation")]
        [Range(0f, 2f)]
        [SerializeField] private float temperature = 0.7f;

        [Range(1, 8192)]
        [SerializeField] private int maxTokens = 4096;

        [Header("Network")]
        [Tooltip("Таймаут в секундах. DeepSeek иногда думает долго.")]
        [Range(30, 300)]
        [SerializeField] private int timeoutSeconds = 180;

        [Tooltip("Стриминг ответа (SSE). Работает на всех платформах кроме WebGL.")]
        [SerializeField] private bool useStreaming = true;

        [Header("Debug")]
        [SerializeField] private bool debugLogging = false;

        // ---------------- Public accessors ----------------

        public string ApiKey => apiKey;
        public string BaseUrl => baseUrl.TrimEnd('/');
        public string Model => model;
        public float Temperature => temperature;
        public int MaxTokens => maxTokens;
        public int TimeoutSeconds => timeoutSeconds;
        public bool UseStreaming => useStreaming;
        public bool DebugLogging => debugLogging;

        /// <summary>Проверка, что ключ заполнен и выглядит как настоящий.</summary>
        public bool IsValid =>
            !string.IsNullOrWhiteSpace(apiKey) && apiKey.StartsWith("sk-");

        /// <summary>Полный URL для запроса Chat Completions.</summary>
        public string ChatCompletionsUrl => $"{BaseUrl}/chat/completions";

        private void OnValidate()
        {
            if (!string.IsNullOrEmpty(apiKey))
                apiKey = apiKey.Trim();
            if (!string.IsNullOrEmpty(baseUrl))
                baseUrl = baseUrl.Trim().TrimEnd('/');
            if (!string.IsNullOrEmpty(model))
                model = model.Trim();
        }
    }
}