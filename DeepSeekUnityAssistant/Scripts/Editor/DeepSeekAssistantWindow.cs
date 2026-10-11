using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using DeepSeekUnity.Editor.Tools;
using DeepSeekUnity.Runtime;
using DeepSeekUnity.Shared;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace DeepSeekUnity.Editor
{
    /// <summary>
    /// Главное окно ассистента. Открывается через Window → DeepSeek → Assistant.
    /// </summary>
    public sealed class DeepSeekAssistantWindow : EditorWindow
    {
        private const string ConfigKey = DeepSeekSetupWizard.ConfigGuidKey;
        private const int MaxToolIterations = 8;

        // -------- Состояние --------
        private DeepSeekConfig _config;
        private DeepSeekClient _client;
        private readonly List<ChatMessage> _history = new();
        private readonly List<IDeepSeekTool> _tools = new();

        private Vector2 _scroll;
        private string _input = "";
        private bool _isBusy;
        private string _statusMessage = "";

        // -------- Стриминг --------
        private string _streamingContent = "";
        private bool _isStreaming;

        // -------- Undo --------
        private int _lastUndoGroup = -1;

        // -------- Макросы --------
        private DeepSeekMacros _macros;

        // -------- Стили --------
        private GUIStyle _userStyle;
        private GUIStyle _assistantStyle;
        private GUIStyle _systemStyle;
        private GUIStyle _toolStyle;
        private GUIStyle _headerBoldStyle;
        private GUIStyle _copyBtnStyle;
        private bool _stylesReady;

        // =========================================================
        //  Открытие
        // =========================================================

        [MenuItem("Window/DeepSeek/Assistant %#a")]
        public static void Open()
        {
            var w = GetWindow<DeepSeekAssistantWindow>("DeepSeek Assistant");
            w.minSize = new Vector2(480, 500);
            w.Show();
        }

        private void OnEnable()
        {
            LoadConfig();
            RegisterTools();
            LoadMacros();

            if (_config != null)
                _client = new DeepSeekClient(_config);

            if (DeepSeekHistory.Exists)
            {
                var loaded = DeepSeekHistory.Load();
                if (loaded.Count > 0)
                {
                    _history.AddRange(loaded);
                    Debug.Log($"[DeepSeek] Автозагружено {loaded.Count} сообщений из прошлой сессии.");
                }
            }
        }

        private void OnDisable()
        {
            _client?.Abort();
        }

        private void LoadConfig()
        {
            string guid = EditorPrefs.GetString(ConfigKey, "");
            if (!string.IsNullOrEmpty(guid))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path))
                    _config = AssetDatabase.LoadAssetAtPath<DeepSeekConfig>(path);
            }

            if (_config == null)
                _config = DeepSeekSetupWizard.ExistingConfig();
        }

        private void LoadMacros()
        {
            _macros = DeepSeekMacros.Load();
        }

        private void RegisterTools()
        {
            _tools.Clear();

            // --- Сцена ---
            _tools.Add(new InspectSceneTool());
            _tools.Add(new CreateGameObjectTool());
            _tools.Add(new DeleteGameObjectTool());
            _tools.Add(new SetTransformTool());
            _tools.Add(new InspectObjectTool());
            _tools.Add(new ListRootObjectsTool());

            // --- Скрипты ---
            _tools.Add(new ReadScriptTool());
            _tools.Add(new CreateScriptTool());

            // --- Ассеты ---
            _tools.Add(new FindAssetsTool());

            // --- Расширенные ---
            _tools.Add(new CreateMaterialTool());
            _tools.Add(new AssignMaterialTool());
            _tools.Add(new AddComponentTool());
            _tools.Add(new RemoveComponentTool());
            _tools.Add(new CreatePrefabTool());
            _tools.Add(new SetLightTool());

            // --- Расширенные v2 ---
            _tools.Add(new MoveAssetTool());
            _tools.Add(new DuplicateObjectTool());
            _tools.Add(new AlignToGroundTool());
            _tools.Add(new TerrainGetHeightTool());
            _tools.Add(new TerrainPlaceOnSurfaceTool());
            _tools.Add(new ScatterObjectsTool());
            _tools.Add(new SpawnLootOnTerrainTool());
            _tools.Add(new ListTerrainsTool());

            // --- Работа с выделением ---
            _tools.Add(new GetSelectionTool());
            _tools.Add(new AlignSelectionToGroundTool());
            _tools.Add(new DeleteSelectionTool());
            _tools.Add(new DuplicateSelectionTool());
            _tools.Add(new AddComponentToSelectionTool());
            _tools.Add(new AlignAllToGroundTool());

            // --- Префабы ---
            _tools.Add(new FindPrefabInstancesTool());
            _tools.Add(new OpenPrefabTool());
            _tools.Add(new ApplyPrefabOverridesTool());
            _tools.Add(new RevertPrefabOverridesTool());
            _tools.Add(new UnpackPrefabTool());

            // --- Пакетные операции с ассетами ---
            _tools.Add(new BatchRenameTool());
            _tools.Add(new BatchTextureImportTool());
            _tools.Add(new BatchChangeShaderTool());
            _tools.Add(new FindDuplicateMaterialsTool());
            _tools.Add(new FindAssetsInFolderTool());


            // --- Уборка проекта ---
            _tools.Add(new AnalyzeProjectTool());
            _tools.Add(new FindEmptyFoldersTool());
            _tools.Add(new FindMissingScriptsTool());
            _tools.Add(new FindDuplicateFilesTool());
            _tools.Add(new FindSameNameAssetsTool());
            _tools.Add(new FindLargeFilesTool());
            _tools.Add(new FindTempFilesTool());
            _tools.Add(new FindFilesOutsideAssetsTool());
            _tools.Add(new DeleteAssetTool());
        }

        // =========================================================
        //  Стили
        // =========================================================

        private void EnsureStyles()
        {
            if (_stylesReady) return;

            _userStyle = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true,
                richText = false,
                padding = new RectOffset(6, 6, 4, 4),
                border = new RectOffset(0, 0, 0, 0),
                normal = { background = null, textColor = new Color(0.85f, 0.92f, 1f) },
                focused = { background = null, textColor = new Color(0.85f, 0.92f, 1f) },
                hover = { background = null, textColor = new Color(0.85f, 0.92f, 1f) },
                active = { background = null, textColor = new Color(0.85f, 0.92f, 1f) }
            };

            _assistantStyle = new GUIStyle(_userStyle)
            {
                normal = { background = null, textColor = new Color(0.95f, 0.95f, 0.95f) },
                focused = { background = null, textColor = new Color(0.95f, 0.95f, 0.95f) },
                hover = { background = null, textColor = new Color(0.95f, 0.95f, 0.95f) },
                active = { background = null, textColor = new Color(0.95f, 0.95f, 0.95f) }
            };

            _systemStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                wordWrap = true,
                normal = { textColor = new Color(0.6f, 0.7f, 0.9f) }
            };

            _toolStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                wordWrap = true,
                normal = { textColor = new Color(0.5f, 0.85f, 0.5f) }
            };

            _headerBoldStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 12
            };

            _copyBtnStyle = new GUIStyle(EditorStyles.miniButton)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(2, 2, 0, 2)
            };

            _stylesReady = true;
        }

        // =========================================================
        //  OnGUI
        // =========================================================

        private void OnGUI()
        {
            EnsureStyles();
            HandleHotkeys();

            DrawHeader();
            EditorGUILayout.Space(4);

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.ExpandHeight(true));
            DrawHistory();
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space(4);
            DrawInput();
        }

        // =========================================================
        //  Горячие клавиши
        // =========================================================

        private void HandleHotkeys()
        {
            var e = Event.current;
            if (e == null) return;
            if (e.type != EventType.KeyDown) return;
            if (!e.control) return;

            if (e.shift && e.keyCode == KeyCode.C)
            {
                if (_history.Count > 0)
                {
                    EditorGUIUtility.systemCopyBuffer = BuildFullConversationText();
                    ShowNotification(new GUIContent("Диалог скопирован"));
                }
                e.Use();
            }
        }

        private string BuildFullConversationText()
        {
            var sb = new StringBuilder();
            foreach (var msg in _history)
            {
                switch (msg.Role)
                {
                    case "user":
                        sb.AppendLine("### You");
                        sb.AppendLine(msg.Content);
                        sb.AppendLine();
                        break;

                    case "assistant":
                        sb.AppendLine("### Assistant");
                        if (!string.IsNullOrEmpty(msg.Content))
                            sb.AppendLine(msg.Content);
                        if (msg.ToolCalls != null)
                        {
                            foreach (var tc in msg.ToolCalls)
                                sb.AppendLine($"[tool] {tc.Function.Name}({tc.Function.Arguments})");
                        }
                        sb.AppendLine();
                        break;

                    case "tool":
                        sb.AppendLine($"[tool result] {msg.Content}");
                        sb.AppendLine();
                        break;
                }
            }
            return sb.ToString().TrimEnd();
        }

        // =========================================================
        //  Header
        // =========================================================

        private void DrawHeader()
        {
            // ----- Верхний ряд: имя, инфо, кнопки -----
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label("DeepSeek Assistant", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (_config != null)
            {
                GUILayout.Label($"Model: {_config.Model}", EditorStyles.miniLabel);
                GUILayout.Space(6);
                GUILayout.Label($"Tools: {_tools.Count}", EditorStyles.miniLabel);
                GUILayout.Space(6);
                GUILayout.Label($"Msgs: {_history.Count}", EditorStyles.miniLabel);
                GUILayout.Space(6);
            }
            else
            {
                GUILayout.Label("⚠ Конфиг не найден", EditorStyles.miniLabel);

                if (GUILayout.Button("Настроить", EditorStyles.toolbarButton))
                    DeepSeekSetupWizard.Open();
            }

            // --- Screenshot dropdown ---
            if (GUILayout.Button(new GUIContent("📷", "Скриншот"), EditorStyles.toolbarButton))
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Scene View"), false, () =>
                {
                    string path = DeepSeekScreenshot.CaptureSceneView();
                    if (!string.IsNullOrEmpty(path))
                        ShowNotification(new GUIContent("Скриншот снят"));
                });
                menu.AddItem(new GUIContent("Game View"), false, () =>
                {
                    string path = DeepSeekScreenshot.CaptureGameView();
                    if (!string.IsNullOrEmpty(path))
                        ShowNotification(new GUIContent("Скриншот снят"));
                });
                menu.AddSeparator("");
                menu.AddItem(new GUIContent("Открыть папку"), false, () =>
                {
                    EditorUtility.RevealInFinder(DeepSeekScreenshot.ScreenshotDir);
                });
                menu.ShowAsContext();
            }

            // --- Undo AI ---
            if (GUILayout.Button(new GUIContent("↶ Undo AI", "Откатить последнее изменение от ассистента"), EditorStyles.toolbarButton))
            {
                Undo.PerformUndo();
                ShowNotification(new GUIContent("Откат"));
            }

            // --- Macros dropdown ---
            if (GUILayout.Button(new GUIContent("Macros ▾", "Сохранённые промпты"), EditorStyles.toolbarButton))
            {
                ShowMacrosMenu();
            }

            // --- Copy All ---
            if (GUILayout.Button(new GUIContent("Copy All", "Скопировать весь диалог"), EditorStyles.toolbarButton))
            {
                if (_history.Count > 0)
                {
                    EditorGUIUtility.systemCopyBuffer = BuildFullConversationText();
                    ShowNotification(new GUIContent("Диалог скопирован"));
                }
            }

            // --- New ---
            if (GUILayout.Button("New", EditorStyles.toolbarButton))
            {
                if (_history.Count == 0 || EditorUtility.DisplayDialog(
                    "DeepSeek",
                    "Начать новый диалог?\nТекущая история будет очищена.\n" +
                    "(Сохранённая на диск — останется)",
                    "Да", "Отмена"))
                {
                    _history.Clear();
                    Repaint();
                }
            }

            // --- Save ---
            if (GUILayout.Button("Save", EditorStyles.toolbarButton))
            {
                bool ok = DeepSeekHistory.Save(_history);
                if (ok)
                    Debug.Log($"[DeepSeek] История сохранена: {DeepSeekHistory.DebugPath}");
                else
                    Debug.LogError("[DeepSeek] Не удалось сохранить историю.");
            }

            // --- Load ---
            if (GUILayout.Button("Load", EditorStyles.toolbarButton))
            {
                if (!DeepSeekHistory.Exists)
                {
                    EditorUtility.DisplayDialog("DeepSeek", "Сохранённой истории нет.", "OK");
                }
                else if (_history.Count == 0 || EditorUtility.DisplayDialog(
                    "DeepSeek",
                    "Загрузить сохранённую историю?\nТекущая будет потеряна.",
                    "Да", "Отмена"))
                {
                    var loaded = DeepSeekHistory.Load();
                    _history.Clear();
                    _history.AddRange(loaded);
                    Debug.Log($"[DeepSeek] Загружено {loaded.Count} сообщений.");
                    Repaint();
                }
            }

            // --- Clear ---
            if (GUILayout.Button("Clear", EditorStyles.toolbarButton))
            {
                if (_history.Count > 0)
                {
                    bool ok = EditorUtility.DisplayDialog(
                        "DeepSeek",
                        "Очистить историю диалога?",
                        "Очистить", "Отмена");
                    if (ok)
                    {
                        _history.Clear();
                        Repaint();
                    }
                }
            }

            // --- Settings ---
            if (GUILayout.Button("Settings", EditorStyles.toolbarButton))
            {
                if (_config != null)
                {
                    Selection.activeObject = _config;
                    EditorGUIUtility.PingObject(_config);
                }
                else
                {
                    DeepSeekSetupWizard.Open();
                }
            }

            EditorGUILayout.EndHorizontal();
        }

        // =========================================================
        //  Macros
        // =========================================================

        private void ShowMacrosMenu()
        {
            var menu = new GenericMenu();

            if (_macros != null && _macros.items.Count > 0)
            {
                foreach (var macro in _macros.items)
                {
                    var captured = macro;
                    menu.AddItem(new GUIContent(macro.name), false, () =>
                    {
                        _input = captured.prompt;
                        GUI.FocusControl(null);
                        Repaint();
                    });
                }

                menu.AddSeparator("");
            }

            menu.AddItem(new GUIContent("Создать файл макросов"), false, () =>
            {
                var asset = DeepSeekMacros.CreateAssetFile();
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
                LoadMacros();
            });

            menu.AddItem(new GUIContent("Открыть файл макросов"), false, () =>
            {
                var asset = DeepSeekMacros.CreateAssetFile();
                Selection.activeObject = asset;
                EditorGUIUtility.PingObject(asset);
            });

            menu.AddItem(new GUIContent("Перезагрузить"), false, () =>
            {
                DeepSeekMacros.ClearCache();
                LoadMacros();
                Repaint();
            });

            menu.ShowAsContext();
        }

        // =========================================================
        //  History
        // =========================================================

        private void DrawHistory()
        {
            if (_history.Count == 0 && !_isBusy)
            {
                EditorGUILayout.HelpBox(
                    "Привет! Я ассистент на базе DeepSeek.\n" +
                    "Могу работать со сценой, скриптами, ассетами, рельефом и префабами.\n\n" +
                    "Примеры команд:\n" +
                    "  • Создай куб на позиции 0 5 0\n" +
                    "  • Прижми всё выделенное к земле\n" +
                    "  • Разбросай 50 копий Tree в радиусе 100 вокруг 0 0\n" +
                    "  • Найди все материалы-дубликаты\n" +
                    "  • Переименуй все ассеты в Assets/Old в New_*\n\n" +
                    "Инструменты в шапке:\n" +
                    "  • 📷 Скриншот Scene / Game View\n" +
                    "  • ↶ Undo AI — откат последнего изменения\n" +
                    "  • Macros ▾ — сохранённые промпты",
                    MessageType.Info);
                return;
            }

            for (int i = 0; i < _history.Count; i++)
            {
                bool isStreamingThis = _isStreaming
                                       && i == _history.Count - 1
                                       && _history[i].Role == "assistant";

                DrawMessage(_history[i], isStreamingThis);
                EditorGUILayout.Space(6);
            }

            if (_isBusy)
            {
                EditorGUILayout.LabelField(_statusMessage, EditorStyles.miniLabel);
                Repaint();
            }
        }

        private void DrawMessage(ChatMessage msg, bool isStreamingThis)
        {
            switch (msg.Role)
            {
                case "user":
                    DrawMessageBody("You", msg.Content, _userStyle, isStreamingThis);
                    break;

                case "assistant":
                    if (!string.IsNullOrEmpty(msg.Content))
                        DrawMessageBody("Assistant", msg.Content, _assistantStyle, isStreamingThis);

                    if (msg.ToolCalls != null)
                    {
                        foreach (var tc in msg.ToolCalls)
                        {
                            EditorGUILayout.LabelField(
                                $"  🔧 {tc.Function.Name}({Shorten(tc.Function.Arguments, 120)})",
                                _toolStyle);
                        }
                    }
                    break;

                case "tool":
                    DrawMessageBody("↩ Tool",
                        Shorten(msg.Content, 300),
                        _systemStyle,
                        isStreamingThis,
                        showCopy: false);
                    break;

                case "system":
                    EditorGUILayout.LabelField(msg.Content, _systemStyle);
                    break;
            }
        }

        private void DrawMessageBody(
            string title,
            string content,
            GUIStyle textStyle,
            bool isStreamingThis,
            bool showCopy = true)
        {
            content = content ?? "";

            // ---------- Заголовок ----------
            Rect headerRect = EditorGUILayout.GetControlRect(false, 18f);
            Rect titleRect = new Rect(headerRect.x, headerRect.y, 120f, headerRect.height);
            EditorGUI.LabelField(titleRect, title, _headerBoldStyle);

            if (showCopy)
            {
                Rect btnRect = new Rect(headerRect.xMax - 26f, headerRect.y + 1f, 24f, 16f);

                bool hasContent = !string.IsNullOrEmpty(content);
                bool oldEnabled = GUI.enabled;
                Color oldColor = GUI.color;

                GUI.enabled = hasContent;
                GUI.color = hasContent
                    ? new Color(0.7f, 0.85f, 1f, 0.95f)
                    : new Color(0.7f, 0.85f, 1f, 0.25f);

                if (GUI.Button(btnRect, new GUIContent("C", "Скопировать всё сообщение"), _copyBtnStyle))
                {
                    EditorGUIUtility.systemCopyBuffer = content;
                    ShowNotification(new GUIContent("Скопировано"));
                }

                GUI.color = oldColor;
                GUI.enabled = oldEnabled;
            }

            // ---------- Тело ----------
            float bodyWidth = position.width - 40f;
            float bodyHeight = textStyle.CalcHeight(new GUIContent(content), bodyWidth);
            bodyHeight = Mathf.Max(bodyHeight, 18f);

            Rect bodyRect = EditorGUILayout.GetControlRect(false, bodyHeight);

            if (isStreamingThis)
            {
                EditorGUI.LabelField(bodyRect, content, textStyle);
                Repaint();
            }
            else
            {
                GUI.SetNextControlName("msg_" + GetHashCode() + "_" + bodyRect.y);

                var prevBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0f, 0f, 0f, 0.12f);
                EditorGUI.TextArea(bodyRect, content, textStyle);
                GUI.backgroundColor = prevBg;
            }

            // ---------- Контекстное меню ----------
            Event e = Event.current;
            if (e != null && e.type == EventType.ContextClick && bodyRect.Contains(e.mousePosition))
            {
                var menu = new GenericMenu();

                if (!string.IsNullOrEmpty(content))
                {
                    menu.AddItem(new GUIContent("Копировать это сообщение"), false, () =>
                    {
                        EditorGUIUtility.systemCopyBuffer = content;
                        ShowNotification(new GUIContent("Скопировано"));
                    });

                    menu.AddItem(new GUIContent("Копировать с заголовком"), false, () =>
                    {
                        EditorGUIUtility.systemCopyBuffer = $"{title}:\n{content}";
                        ShowNotification(new GUIContent("Скопировано"));
                    });
                }
                else
                {
                    menu.AddDisabledItem(new GUIContent("Копировать это сообщение"));
                }

                menu.AddSeparator("");

                menu.AddItem(new GUIContent("Копировать весь диалог"), false, () =>
                {
                    EditorGUIUtility.systemCopyBuffer = BuildFullConversationText();
                    ShowNotification(new GUIContent("Диалог скопирован"));
                });

                menu.AddSeparator("");

                menu.AddItem(new GUIContent("Очистить историю"), false, () =>
                {
                    _history.Clear();
                    Repaint();
                });

                menu.ShowAsContext();
                e.Use();
            }
        }

        private static string Shorten(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= max ? s : s.Substring(0, max) + "…";
        }

        // =========================================================
        //  Input
        // =========================================================

        private void DrawInput()
        {
            EditorGUILayout.BeginHorizontal();

            bool enterPressed = Event.current.type == EventType.KeyDown
                                && (Event.current.keyCode == KeyCode.Return
                                    || Event.current.keyCode == KeyCode.KeypadEnter)
                                && !Event.current.shift;

            bool canSend = !_isBusy
                           && !string.IsNullOrWhiteSpace(_input)
                           && _config != null
                           && _config.IsValid;

            if (enterPressed && canSend)
            {
                Send();
                Event.current.Use();
            }

            GUI.enabled = !_isBusy && _config != null && _config.IsValid;
            _input = EditorGUILayout.TextArea(_input,
                GUILayout.MinHeight(40),
                GUILayout.ExpandHeight(false));
            GUI.enabled = true;

            if (_isBusy && _isStreaming)
            {
                var oldBg = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.9f, 0.35f, 0.35f);

                if (GUILayout.Button("Stop", GUILayout.Width(60), GUILayout.Height(40)))
                    _client?.Abort();

                GUI.backgroundColor = oldBg;
            }
            else
            {
                GUI.enabled = canSend;

                if (GUILayout.Button(_isBusy ? "..." : "Send",
                                     GUILayout.Width(60),
                                     GUILayout.Height(40)))
                {
                    Send();
                }

                GUI.enabled = true;
            }

            EditorGUILayout.EndHorizontal();

            if (_config == null)
            {
                EditorGUILayout.HelpBox(
                    "Конфиг не найден. Нажми «Настроить» вверху.",
                    MessageType.Warning);
            }
            else if (!_config.IsValid)
            {
                EditorGUILayout.HelpBox(
                    "API-ключ не задан или неверный. Открой конфиг и вставь ключ.",
                    MessageType.Warning);
            }

            if (_isStreaming && !string.IsNullOrEmpty(_streamingContent))
            {
                EditorGUILayout.LabelField(
                    $"Стрим: {_streamingContent.Length} символов получено…",
                    EditorStyles.miniLabel);
            }
        }

        // =========================================================
        //  Отправка
        // =========================================================

        private async void Send()
        {
            if (string.IsNullOrWhiteSpace(_input)) return;
            if (_isBusy) return;
            if (_config == null || !_config.IsValid) return;

            if (_client == null)
                _client = new DeepSeekClient(_config);

            _isBusy = true;
            _statusMessage = "Отправка...";
            string userText = _input.Trim();
            _input = "";

            // ---- Undo: запоминаем текущую группу до начала ----
            int undoGroupBefore = Undo.GetCurrentGroup();
            Undo.IncrementCurrentGroup();
            int undoGroupNew = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"DeepSeek: {Shorten(userText, 40)}");
            _lastUndoGroup = undoGroupNew;

            Repaint();

            _history.Add(new ChatMessage { Role = "user", Content = userText });

            try
            {
                await RunAgentLoop();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _history.Add(new ChatMessage
                {
                    Role = "assistant",
                    Content = $"❌ Ошибка: {e.Message}"
                });
            }
            finally
            {
                _isBusy = false;
                _isStreaming = false;
                _streamingContent = "";
                _statusMessage = "";

                // ---- Undo: склеиваем все изменения в одну группу ----
                Undo.CollapseUndoOperations(undoGroupNew);

                if (_history.Count > 0)
                    DeepSeekHistory.Save(_history);

                Repaint();
            }
        }

        private async Task RunAgentLoop()
        {
            if (_client == null) _client = new DeepSeekClient(_config);

            for (int iteration = 0; iteration < MaxToolIterations; iteration++)
            {
                _statusMessage = $"Запрос к модели (шаг {iteration + 1}/{MaxToolIterations})...";
                _streamingContent = "";
                _isStreaming = _config.UseStreaming;
                Repaint();

                ChatResponse response;

                if (_config.UseStreaming)
                {
                    var placeholder = new ChatMessage { Role = "assistant", Content = "" };
                    _history.Add(placeholder);

                    try
                    {
                        response = await _client.ChatStreamAsync(
                            BuildRequest(),
                            onDelta: (delta) =>
                            {
                                _streamingContent += delta;
                                placeholder.Content = _streamingContent;
                                Repaint();
                            });
                    }
                    catch (Exception e)
                    {
                        _history.Remove(placeholder);
                        _history.Add(new ChatMessage
                        {
                            Role = "assistant",
                            Content = $"❌ {e.Message}"
                        });
                        _isStreaming = false;
                        return;
                    }

                    _isStreaming = false;

                    int idx = _history.IndexOf(placeholder);
                    if (idx >= 0)
                    {
                        _history[idx] = new ChatMessage
                        {
                            Role = "assistant",
                            Content = response?.Content,
                            ToolCalls = response?.ToolCalls
                        };
                    }
                }
                else
                {
                    try
                    {
                        response = await _client.ChatAsync(BuildRequest());
                    }
                    catch (Exception e)
                    {
                        _history.Add(new ChatMessage
                        {
                            Role = "assistant",
                            Content = $"❌ {e.Message}"
                        });
                        return;
                    }

                    if (response == null || response.IsError)
                    {
                        _history.Add(new ChatMessage
                        {
                            Role = "assistant",
                            Content = response?.IsError == true
                                ? $"❌ API error: {response.Error}"
                                : "❌ Пустой ответ от API."
                        });
                        return;
                    }

                    if (!response.HasToolCalls)
                    {
                        _history.Add(new ChatMessage
                        {
                            Role = "assistant",
                            Content = response.Content ?? "(пустой ответ)"
                        });
                        return;
                    }

                    _history.Add(new ChatMessage
                    {
                        Role = "assistant",
                        Content = response.Content,
                        ToolCalls = response.ToolCalls
                    });
                }

                if (_config.UseStreaming && (response == null || !response.HasToolCalls))
                    return;

                if (response == null || response.ToolCalls == null) continue;

                foreach (var call in response.ToolCalls)
                {
                    _statusMessage = $"Выполняю: {call.Function.Name}...";
                    Repaint();

                    string result = await ExecuteToolAsync(call);

                    _history.Add(new ChatMessage
                    {
                        Role = "tool",
                        ToolCallId = call.Id,
                        Content = result
                    });
                }
            }

            _history.Add(new ChatMessage
            {
                Role = "assistant",
                Content = "⚠ Достигнут лимит шагов агента. Попробуй переформулировать запрос."
            });
        }

        private ChatRequest BuildRequest()
        {
            var req = new ChatRequest
            {
                Model = _config.Model,
                Temperature = _config.Temperature,
                MaxTokens = _config.MaxTokens,
                Stream = _config.UseStreaming,
                Messages = new List<ChatMessage>(_history)
            };

            bool hasSystem = req.Messages.Count > 0 && req.Messages[0].Role == "system";
            if (!hasSystem)
            {
                req.Messages.Insert(0, new ChatMessage
                {
                    Role = "system",
                    Content = BuildSystemPrompt()
                });
            }

            req.Tools = new List<ToolDefinition>();
            foreach (var tool in _tools)
            {
                req.Tools.Add(new ToolDefinition
                {
                    Function = new FunctionDefinition
                    {
                        Name = tool.Name,
                        Description = tool.Description,
                        Parameters = tool.ParametersSchema
                    }
                });
            }
            req.ToolChoice = "auto";

            return req;
        }

        private static string BuildSystemPrompt()
        {
            return
                "Ты — AI-ассистент, встроенный в Unity Editor. " +
                "Ты можешь выполнять реальные действия в проекте через инструменты (tools). " +
                "Отвечай на русском языке. Будь кратким. " +
                "Когда пользователь просит что-то сделать — сразу вызывай подходящий инструмент, " +
                "не объясняй, что ты собираешься сделать. " +
                "После выполнения — коротко подтверди результат. " +
                "Если задача требует нескольких шагов — выполняй их последовательно, " +
                "вызывая инструменты по одному. " +
                "Если просят разместить объекты на рельефе — terrain_place_on_surface. " +
                "Если разбросать копии — scatter_objects. " +
                "Если прижать выделенное — align_selection_to_ground. " +
                "Если работают с префабами — find_prefab_instances, open_prefab, apply_prefab_overrides, " +
                "revert_prefab_overrides, unpack_prefab. " +
                "Для пакетных операций с ассетами — batch_rename, batch_texture_import, " +
                "batch_change_shader, find_duplicate_materials.";
        }

        private async Task<string> ExecuteToolAsync(ToolCall call)
        {
            foreach (var tool in _tools)
            {
                if (tool.Name != call.Function.Name) continue;

                try
                {
                    return await tool.ExecuteAsync(call.Function.Arguments);
                }
                catch (Exception e)
                {
                    return $"Ошибка выполнения инструмента '{tool.Name}': {e.Message}";
                }
            }

            return $"Инструмент '{call.Function.Name}' не найден.";
        }
    }
}