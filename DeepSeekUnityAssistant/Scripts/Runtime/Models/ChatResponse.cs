using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace DeepSeekUnity.Runtime
{
    /// <summary>
    /// Ответ от DeepSeek Chat Completions API.
    /// </summary>
    [Serializable]
    public sealed class ChatResponse
    {
        [JsonProperty("id")]
        public string Id;

        [JsonProperty("model")]
        public string Model;

        [JsonProperty("choices")]
        public List<Choice> Choices = new();

        [JsonProperty("usage")]
        public UsageInfo Usage;

        [JsonProperty("error")]
        public ApiError Error;

        // ---------------- Удобные свойства ----------------

        /// <summary>Текст первого выбора.</summary>
        [JsonIgnore]
        public string Content =>
            Choices != null && Choices.Count > 0
                ? Choices[0].Message?.Content ?? Choices[0].Delta?.Content
                : null;

        /// <summary>Tool calls из первого выбора.</summary>
        [JsonIgnore]
        public List<ToolCall> ToolCalls =>
            Choices != null && Choices.Count > 0
                ? Choices[0].Message?.ToolCalls ?? Choices[0].Delta?.ToolCalls
                : null;

        /// <summary>Причина остановки: stop, tool_calls, length.</summary>
        [JsonIgnore]
        public string FinishReason =>
            Choices != null && Choices.Count > 0
                ? Choices[0].FinishReason
                : null;

        [JsonIgnore]
        public bool HasToolCalls => ToolCalls != null && ToolCalls.Count > 0;

        [JsonIgnore]
        public bool IsError => Error != null;
    }

    /// <summary>Один вариант ответа.</summary>
    [Serializable]
    public sealed class Choice
    {
        [JsonProperty("index")]
        public int Index;

        /// <summary>Полный ответ (для не-стриминга).</summary>
        [JsonProperty("message")]
        public ChatMessage Message;

        /// <summary>Дельта-ответ (для стриминга).</summary>
        [JsonProperty("delta")]
        public ChatMessage Delta;

        [JsonProperty("finish_reason")]
        public string FinishReason;
    }

    /// <summary>Информация о расходе токенов.</summary>
    [Serializable]
    public sealed class UsageInfo
    {
        [JsonProperty("prompt_tokens")]
        public int PromptTokens;

        [JsonProperty("completion_tokens")]
        public int CompletionTokens;

        [JsonProperty("total_tokens")]
        public int TotalTokens;
    }

    /// <summary>Ошибка от API.</summary>
    [Serializable]
    public sealed class ApiError
    {
        [JsonProperty("message")]
        public string Message;

        [JsonProperty("type")]
        public string Type;

        [JsonProperty("code")]
        public string Code;

        public override string ToString() => $"[{Code}] {Message}";
    }
}