using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

namespace DeepSeekUnity.Runtime
{
    /// <summary>
    /// HTTP-клиент для DeepSeek API с поддержкой потокового вывода (SSE).
    /// </summary>
    public sealed class DeepSeekClient
    {
        private readonly DeepSeekConfig _config;
        private UnityWebRequest _activeRequest;

        public DeepSeekClient(DeepSeekConfig config)
        {
            _config = config;
        }

        // =========================================================
        //  Обычный запрос (без стриминга)
        // =========================================================

        public async Task<ChatResponse> ChatAsync(ChatRequest request)
        {
            request.Stream = false;
            string json = JsonConvert.SerializeObject(request);

            if (_config.DebugLogging)
                Debug.Log($"[DeepSeek] → {json}");

            using var www = new UnityWebRequest(_config.ChatCompletionsUrl, "POST");
            www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            www.uploadHandler.contentType = "application/json; charset=utf-8";
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
            www.SetRequestHeader("Authorization", $"Bearer {_config.ApiKey}");
            www.timeout = _config.TimeoutSeconds;
            www.chunkedTransfer = false;

            _activeRequest = www;
            var op = www.SendWebRequest();
            while (!op.isDone)
            {
                if (_activeRequest == null) break; // отменено
                await Task.Yield();
            }

            if (www.result != UnityWebRequest.Result.Success)
            {
                string err = Encoding.UTF8.GetString(www.downloadHandler.data ?? new byte[0]);
                throw new Exception(FormatHttpError(www.responseCode, www.error, err));
            }

            string responseJson = Encoding.UTF8.GetString(www.downloadHandler.data);
            _activeRequest = null;

            if (_config.DebugLogging)
                Debug.Log($"[DeepSeek] ← {responseJson}");

            return JsonConvert.DeserializeObject<ChatResponse>(responseJson);
        }

        // =========================================================
        //  Стриминг (SSE)
        // =========================================================

        public Task<ChatResponse> ChatStreamAsync(
            ChatRequest request,
            Action<string> onDelta,
            Action<Exception> onError = null)
        {
            request.Stream = true;
            var tcs = new TaskCompletionSource<ChatResponse>();

            var handler = new SseStreamHandler(
                onDelta: onDelta,
                onComplete: (finalResponse) =>
                {
                    _activeRequest = null;
                    tcs.TrySetResult(finalResponse);
                },
                onError: (ex) =>
                {
                    _activeRequest = null;
                    onError?.Invoke(ex);
                    tcs.TrySetException(ex);
                });

            string url = _config.ChatCompletionsUrl;
            string json = JsonConvert.SerializeObject(request);

            if (_config.DebugLogging)
                Debug.Log($"[DeepSeek/stream] → {json}");

            var www = new UnityWebRequest(url, "POST");
            www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            www.uploadHandler.contentType = "application/json; charset=utf-8";
            www.downloadHandler = handler;
            www.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
            www.SetRequestHeader("Accept", "text/event-stream");
            www.SetRequestHeader("Authorization", $"Bearer {_config.ApiKey}");
            www.timeout = 0;  // без таймаута для стрима
            www.chunkedTransfer = false;

            _activeRequest = www;

            var op = www.SendWebRequest();

            // Запускаем пинг, который дёргает handler.PumpEvents() каждый кадр
            PumpEventsAsync(www, op, handler, tcs).ContinueWith(_ => { });

            return tcs.Task;
        }

        private async Task PumpEventsAsync(
            UnityWebRequest www,
            UnityWebRequestAsyncOperation op,
            SseStreamHandler handler,
            TaskCompletionSource<ChatResponse> tcs)
        {
            try
            {
                while (!op.isDone)
                {
                    handler.PumpEvents();
                    await Task.Yield();
                }

                handler.PumpEvents();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    string err = "stream error";
                    try
                    {
                        err = www.downloadHandler?.text ?? www.error ?? "unknown";
                    }
                    catch { /* не критично */ }

                    throw new Exception(FormatHttpError(www.responseCode, www.error, err));
                }

                handler.CompleteStream();
            }
            catch (Exception e)
            {
                handler.RaiseError(e);
            }
            finally
            {
                try { www.Dispose(); } catch { }
            }
        }

        // =========================================================
        //  Отмена / продолжение
        // =========================================================

        /// <summary>Прервать текущий запрос (включая стрим).</summary>
        public void Abort()
        {
            if (_activeRequest == null) return;

            try { _activeRequest.Abort(); }
            catch { }

            _activeRequest = null;
        }

        public bool IsBusy => _activeRequest != null;

        // =========================================================
        //  Утилиты
        // =========================================================

        private static string FormatHttpError(long code, string wwwError, string body)
        {
            if (code == 401) return "HTTP 401: неверный или истёкший API-ключ.";
            if (code == 402) return "HTTP 402: недостаточно средств на балансе DeepSeek.";
            if (code == 429) return "HTTP 429: слишком много запросов. Подожди.";

            return $"HTTP {code}: {wwwError}\n{Shorten(body, 400)}";
        }

        private static string Shorten(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= max ? s : s.Substring(0, max) + "…";
        }
    }

    // =============================================================
    //  SSE-обработчик
    // =============================================================

    /// <summary>
    /// DownloadHandlerScript, парсящий SSE-поток от DeepSeek.
    /// Собирает content, tool_calls и события в готовые объекты.
    /// </summary>
    internal sealed class SseStreamHandler : DownloadHandlerScript
    {
        private readonly StringBuilder _buffer = new();
        private readonly StringBuilder _content = new();
        private readonly List<ToolCall> _toolCalls = new();

        private readonly Action<string> _onDelta;
        private readonly Action<ChatResponse> _onComplete;
        private readonly Action<Exception> _onError;

        private string _finishReason;
        private string _responseId;
        private string _responseModel;
        private UsageInfo _usage;

        // Для отложенного парсинга на главном потоке
        private readonly object _lock = new();
        private readonly Queue<string> _incomingEvents = new();

        // Буфер текущего tool_call (инкрементальные аргументы)
        private int _currentToolIndex = -1;
        private readonly Dictionary<int, ToolCall> _partialToolCalls = new();

        private bool _completed;

        public SseStreamHandler(
            Action<string> onDelta,
            Action<ChatResponse> onComplete,
            Action<Exception> onError)
            : base(new byte[8192])
        {
            _onDelta = onDelta;
            _onComplete = onComplete;
            _onError = onError;
        }

        // ---- ReceiveData вызывается Unity-воркером ----
        protected override bool ReceiveData(byte[] data, int dataLength)
        {
            if (data == null || dataLength <= 0) return true;

            string chunk = Encoding.UTF8.GetString(data, 0, dataLength);
            lock (_lock) { _buffer.Append(chunk); }

            return true;
        }

        /// <summary>Дёргается из главного потока — парсит накопленные данные.</summary>
        public void PumpEvents()
        {
            string toParse = null;

            lock (_lock)
            {
                if (_buffer.Length == 0) return;
                toParse = _buffer.ToString();
                _buffer.Clear();
            }

            // Разбиваем по строкам. Последнюю неполную — обратно в буфер.
            int lastNewline = toParse.LastIndexOf('\n');
            string complete, remainder;

            if (lastNewline < 0)
            {
                lock (_lock) { _buffer.Append(toParse); }
                return;
            }

            complete = toParse.Substring(0, lastNewline);
            remainder = toParse.Substring(lastNewline + 1);

            if (!string.IsNullOrEmpty(remainder))
            {
                lock (_lock) { _buffer.Insert(0, remainder); }
            }

            // Парсим построчно
            foreach (var rawLine in complete.Split('\n'))
            {
                string line = rawLine.TrimEnd('\r');
                ProcessLine(line);
            }
        }

        private void ProcessLine(string line)
        {
            if (string.IsNullOrEmpty(line)) return;

            // SSE-комментарий
            if (line.StartsWith(":")) return;

            // "data: {...}"
            if (!line.StartsWith("data:")) return;

            string payload = line.Substring(5).Trim();
            if (string.IsNullOrEmpty(payload)) return;

            if (payload == "[DONE]") return; // на всякий случай, DeepSeek не шлёт

            try
            {
                HandleEvent(payload);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[DeepSeek] SSE parse error: {e.Message}\nLine: {Shorten(payload, 200)}");
            }
        }

        private void HandleEvent(string json)
        {
            var evt = JsonConvert.DeserializeObject<ChatResponse>(json);
            if (evt == null) return;

            if (evt.Id != null) _responseId = evt.Id;
            if (evt.Model != null) _responseModel = evt.Model;
            if (evt.Usage != null) _usage = evt.Usage;

            if (evt.Choices == null || evt.Choices.Count == 0) return;

            var choice = evt.Choices[0];
            if (!string.IsNullOrEmpty(choice.FinishReason))
                _finishReason = choice.FinishReason;

            var delta = choice.Delta;
            if (delta == null) return;

            // 1) Текстовый контент
            if (!string.IsNullOrEmpty(delta.Content))
            {
                _content.Append(delta.Content);
                _onDelta?.Invoke(delta.Content);
            }

            // 2) Tool calls (накапливаем по индексу)
            if (delta.ToolCalls != null)
            {
                foreach (var tc in delta.ToolCalls)
                {
                    // Индекс в delta приходит в поле Index
                    int idx = tc.Function != null
                        ? (_partialToolCalls.ContainsKey(_currentToolIndex + 1) ? _currentToolIndex + 1 : _currentToolIndex)
                        : _currentToolIndex;

                    // Проще: у DeepSeek каждый delta-tool_call имеет свой id (первый раз) 
                    // и потом доливает arguments без id.
                    if (!string.IsNullOrEmpty(tc.Id) && tc.Function != null)
                    {
                        // Начало нового tool_call
                        _currentToolIndex++;
                        var newCall = new ToolCall
                        {
                            Id = tc.Id,
                            Type = tc.Type ?? "function",
                            Function = new FunctionCall
                            {
                                Name = tc.Function.Name ?? "",
                                Arguments = tc.Function.Arguments ?? ""
                            }
                        };
                        _partialToolCalls[_currentToolIndex] = newCall;
                    }
                    else if (tc.Function != null && _currentToolIndex >= 0)
                    {
                        // Продолжение — доливаем arguments
                        var existing = _partialToolCalls[_currentToolIndex];
                        if (existing?.Function != null)
                        {
                            existing.Function.Arguments += tc.Function.Arguments ?? "";
                        }
                    }
                }
            }
        }

        protected override void CompleteContent()
        {
            base.CompleteContent();
            // Логику завершения делает главный поток в PumpEvents/CompleteStream
        }

        /// <summary>Финализирует стрим, собирает ChatResponse, вызывает onComplete.</summary>
        public void CompleteStream()
        {
            if (_completed) return;
            _completed = true;

            var response = new ChatResponse
            {
                Id = _responseId,
                Model = _responseModel,
                Usage = _usage,
                Choices = new List<Choice>
                {
                    new Choice
                    {
                        Index = 0,
                        FinishReason = _finishReason ?? "stop",
                        Message = new ChatMessage
                        {
                            Role = "assistant",
                            Content = _content.Length > 0 ? _content.ToString() : null,
                            ToolCalls = _partialToolCalls.Count > 0
                                ? new List<ToolCall>(_partialToolCalls.Values)
                                : null
                        }
                    }
                }
            };

            if (_config_debugLogging)
                Debug.Log($"[DeepSeek/stream] ← finish={_finishReason}, " +
                          $"content={_content.Length} chars, tool_calls={_partialToolCalls.Count}");

            _onComplete?.Invoke(response);
        }

        public void RaiseError(Exception e)
        {
            if (_completed) return;
            _completed = true;
            _onError?.Invoke(e);
        }

        // Флаг отладки ставим снаружи
        internal bool _config_debugLogging;

        private static string Shorten(string s, int max)
            => string.IsNullOrEmpty(s) || s.Length <= max ? s : s.Substring(0, max) + "…";
    }
}