using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace DeepSeekUnity.Runtime
{
    /// <summary>
    /// Запрос к DeepSeek Chat Completions API.
    /// Формат совместим с OpenAI.
    /// </summary>
    [Serializable]
    public sealed class ChatRequest
    {
        [JsonProperty("model")]
        public string Model;

        [JsonProperty("messages")]
        public List<ChatMessage> Messages = new();

        [JsonProperty("temperature", NullValueHandling = NullValueHandling.Ignore)]
        public float? Temperature;

        [JsonProperty("max_tokens", NullValueHandling = NullValueHandling.Ignore)]
        public int? MaxTokens;

        [JsonProperty("stream", NullValueHandling = NullValueHandling.Ignore)]
        public bool? Stream;

        [JsonProperty("tools", NullValueHandling = NullValueHandling.Ignore)]
        public List<ToolDefinition> Tools;

        [JsonProperty("tool_choice", NullValueHandling = NullValueHandling.Ignore)]
        public object ToolChoice;

        // ---------------- Helpers ----------------

        public void AddSystem(string content) =>
            Messages.Add(new ChatMessage { Role = "system", Content = content });

        public void AddUser(string content) =>
            Messages.Add(new ChatMessage { Role = "user", Content = content });

        public void AddAssistant(string content, List<ToolCall> toolCalls = null)
        {
            var msg = new ChatMessage { Role = "assistant", Content = content };
            if (toolCalls != null && toolCalls.Count > 0)
            {
                msg.ToolCalls = toolCalls;
                msg.Content = null; // API требует null, если есть tool_calls
            }
            Messages.Add(msg);
        }

        public void AddToolResult(string toolCallId, string content) =>
            Messages.Add(new ChatMessage
            {
                Role = "tool",
                ToolCallId = toolCallId,
                Content = content
            });
    }

    /// <summary>Сообщение в диалоге.</summary>
    [Serializable]
    public sealed class ChatMessage
    {
        [JsonProperty("role")]
        public string Role;

        [JsonProperty("content", NullValueHandling = NullValueHandling.Ignore)]
        public string Content;

        [JsonProperty("tool_calls", NullValueHandling = NullValueHandling.Ignore)]
        public List<ToolCall> ToolCalls;

        [JsonProperty("tool_call_id", NullValueHandling = NullValueHandling.Ignore)]
        public string ToolCallId;
    }

    /// <summary>Вызов инструмента, который запросила модель.</summary>
    [Serializable]
    public sealed class ToolCall
    {
        [JsonProperty("id")]
        public string Id;

        [JsonProperty("type")]
        public string Type = "function";

        [JsonProperty("function")]
        public FunctionCall Function;
    }

    /// <summary>Имя функции и её аргументы (JSON-строкой).</summary>
    [Serializable]
    public sealed class FunctionCall
    {
        [JsonProperty("name")]
        public string Name;

        [JsonProperty("arguments")]
        public string Arguments;
    }

    /// <summary>Определение инструмента для API.</summary>
    [Serializable]
    public sealed class ToolDefinition
    {
        [JsonProperty("type")]
        public string Type = "function";

        [JsonProperty("function")]
        public FunctionDefinition Function;
    }

    /// <summary>Схема функции — имя, описание, параметры.</summary>
    [Serializable]
    public sealed class FunctionDefinition
    {
        [JsonProperty("name")]
        public string Name;

        [JsonProperty("description")]
        public string Description;

        [JsonProperty("parameters")]
        public object Parameters;
    }
}