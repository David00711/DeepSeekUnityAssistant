using System;
using System.Text;
using System.Threading.Tasks;
using DeepSeekUnity.Runtime;
using DeepSeekUnity.Shared;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace DeepSeekUnity.Editor
{
    /// <summary>
    /// Мастер первого запуска. Открывается автоматически, если конфига нет.
    /// Проводит по шагам: получить ключ → вставить → проверить → создать конфиг.
    /// </summary>
    public sealed class DeepSeekSetupWizard : EditorWindow
    {
        private enum Step
        {
            Welcome,
            ApiKey,
            Verifying,
            Verified,
            Done,
            Failed
        }

        // -------- Ключи EditorPrefs --------
        public const string DontShowKey = "DeepSeekUnity.Wizard.DontShow";
        public const string ConfigGuidKey = "DeepSeekUnity.ConfigGuid";

        private const string ConfigFolder = "Assets/DeepSeekAssistant";
        private const string ConfigFileName = "DeepSeekConfig.asset";
        private const string DefaultModel = "deepseek-chat";
        private const string DefaultBaseUrl = "https://api.deepseek.com";

        private Step _step = Step.Welcome;
        private string _apiKeyInput = "";
        private string _verifyText = "";
        private string _verifyError = "";
        private bool _isVerifying;
        private Vector2 _scroll;
        private bool _dontShowAgain;

        private GUIStyle _titleStyle;
        private GUIStyle _bodyStyle;
        private GUIStyle _stepBadgeStyle;
        private GUIStyle _bigButtonStyle;
        private bool _stylesReady;

        // =========================================================
        //  Публичный API
        // =========================================================

        [MenuItem("Window/DeepSeek/Setup Wizard")]
        public static void Open()
        {
            var w = GetWindow<DeepSeekSetupWizard>(true, "DeepSeek Setup", true);
            w.minSize = new Vector2(560, 460);
            w.maxSize = new Vector2(800, 720);
            w.Show();
        }

        /// <summary>Открывает визард, если конфига нет и пользователь не отключил авто-показ.</summary>
        public static void OpenIfNeeded()
        {
            if (EditorPrefs.GetBool(DontShowKey, false)) return;
            if (ExistingConfig() != null) return;

            // Не перебиваем другие окна при перекомпиляции
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

            Open();
        }

        /// <summary>Находит существующий конфиг в проекте, если есть.</summary>
        public static DeepSeekConfig ExistingConfig()
        {
            var guids = AssetDatabase.FindAssets("t:DeepSeekConfig");
            if (guids.Length == 0) return null;

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            var config = AssetDatabase.LoadAssetAtPath<DeepSeekConfig>(path);
            if (config != null)
                EditorPrefs.SetString(ConfigGuidKey, guids[0]);
            return config;
        }

        // =========================================================
        //  Lifecycle
        // =========================================================

        private void OnEnable()
        {
            var existing = ExistingConfig();
            if (existing != null)
            {
                _step = Step.Done;
            }
            _dontShowAgain = EditorPrefs.GetBool(DontShowKey, false);
        }

        private void EnsureStyles()
        {
            if (_stylesReady) return;

            _titleStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleLeft,
                margin = new RectOffset(0, 0, 4, 8)
            };

            _bodyStyle = new GUIStyle(EditorStyles.label)
            {
                fontSize = 12,
                wordWrap = true,
                richText = true
            };

            _stepBadgeStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontSize = 11,
                fontStyle = FontStyle.Italic,
                normal = { textColor = new Color(0.55f, 0.55f, 0.6f) }
            };

            _bigButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                fixedHeight = 36
            };

            _stylesReady = true;
        }

        // =========================================================
        //  OnGUI
        // =========================================================

        private void OnGUI()
        {
            EnsureStyles();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            GUILayout.Space(8);
            DrawHeader();
            GUILayout.Space(10);

            switch (_step)
            {
                case Step.Welcome:  DrawWelcome();  break;
                case Step.ApiKey:   DrawApiKey();   break;
                case Step.Verifying: DrawVerifying(); break;
                case Step.Verified: DrawVerified(); break;
                case Step.Done:     DrawDone();     break;
                case Step.Failed:   DrawFailed();   break;
            }

            GUILayout.Space(16);
            DrawFooter();

            EditorGUILayout.EndScrollView();
        }

        private void DrawHeader()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            GUILayout.Label("DeepSeek Unity Assistant", _titleStyle);
            GUILayout.Label("Бесплатный AI-ассистент для Unity Editor · Галавинов Д. В.", _stepBadgeStyle);
            EditorGUILayout.EndVertical();
        }

        private void DrawWelcome()
        {
            EditorGUILayout.LabelField("Добро пожаловать!", EditorStyles.boldLabel);
            GUILayout.Space(4);

            EditorGUILayout.LabelField(
                "Этот мастер за 3 шага настроит доступ к DeepSeek API. " +
                "Тебе понадобится API-ключ — его можно получить бесплатно, " +
                "а платить только за использование (≈ $0.14 за миллион токенов).",
                _bodyStyle);

            GUILayout.Space(10);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("1. Регистрация на платформе DeepSeek", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Открой в браузере platform.deepseek.com и создай аккаунт. " +
                "Пополни баланс — минимум $2. Без баланса API вернёт ошибку 402.",
                _bodyStyle);

            if (GUILayout.Button("🌐 Открыть platform.deepseek.com", GUILayout.Height(26)))
                Application.OpenURL("https://platform.deepseek.com/api_keys");

            EditorGUILayout.EndVertical();
            GUILayout.Space(8);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("2. Создай API-ключ", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "На странице API Keys нажми «Create new key». Ключ начинается с «sk-». " +
                "Скопируй его — он показывается только один раз.",
                _bodyStyle);
            EditorGUILayout.EndVertical();
            GUILayout.Space(8);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("3. Вернись сюда и нажми «Далее»", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Мы проверим ключ, создадим конфиг и откроем окно ассистента.",
                _bodyStyle);
            EditorGUILayout.EndVertical();

            GUILayout.Space(12);
            if (GUILayout.Button("Далее →", _bigButtonStyle))
                _step = Step.ApiKey;
        }

        private void DrawApiKey()
        {
            EditorGUILayout.LabelField("Шаг 2 · Вставь API-ключ", EditorStyles.boldLabel);
            GUILayout.Space(4);

            EditorGUILayout.LabelField(
                "Ключ начинается с «sk-» и содержит только латиницу, цифры и дефисы. " +
                "Не копируй вместе с пробелами.",
                _bodyStyle);

            GUILayout.Space(8);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            _apiKeyInput = EditorGUILayout.PasswordField("API Key:", _apiKeyInput, GUILayout.Height(24));

            if (!string.IsNullOrEmpty(_apiKeyInput))
            {
                string trimmed = _apiKeyInput.Trim();
                bool looksValid = trimmed.StartsWith("sk-") && trimmed.Length >= 20;

                if (!looksValid)
                    EditorGUILayout.HelpBox("Ключ не похож на настоящий (должен начинаться с «sk-»).", MessageType.Warning);
                else
                    EditorGUILayout.LabelField($"Будет сохранён: {trimmed.Substring(0, 8)}...{trimmed.Substring(trimmed.Length - 4)}",
                        EditorStyles.miniLabel);
            }

            EditorGUILayout.EndVertical();

            GUILayout.Space(12);

            EditorGUILayout.BeginHorizontal();

            GUI.enabled = !_isVerifying;
            if (GUILayout.Button("← Назад", GUILayout.Width(100), GUILayout.Height(30)))
                _step = Step.Welcome;

            GUILayout.FlexibleSpace();

            GUI.enabled = !_isVerifying && IsApiKeyLooksValid();
            if (GUILayout.Button("Проверить ключ →", _bigButtonStyle, GUILayout.Width(200)))
            {
                _verifyError = "";
                _verifyText = "";
                _step = Step.Verifying;
                _ = VerifyAsync();
            }

            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawVerifying()
        {
            EditorGUILayout.LabelField("Проверка ключа...", EditorStyles.boldLabel);
            GUILayout.Space(10);
            EditorGUILayout.LabelField("Отправляем тестовый запрос к api.deepseek.com. Это займёт пару секунд.", _bodyStyle);
            GUILayout.Space(10);
            EditorGUILayout.LabelField(_verifyText, EditorStyles.miniLabel);
            Repaint();
        }

        private void DrawVerified()
        {
            EditorGUILayout.LabelField("✅ Ключ работает!", EditorStyles.boldLabel);
            GUILayout.Space(4);

            EditorGUILayout.LabelField(
                "Мы получили ответ от DeepSeek. Теперь создадим конфиг и откроем ассистента.",
                _bodyStyle);

            GUILayout.Space(10);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Что будет создано:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"• {ConfigFolder}/{ConfigFileName}", _bodyStyle);
            EditorGUILayout.LabelField("• Ключ сохранится в этом файле", _bodyStyle);
            EditorGUILayout.LabelField("• Добавим .gitignore-запись (опционально)", _bodyStyle);
            EditorGUILayout.EndVertical();

            GUILayout.Space(12);

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = !_isVerifying;
            if (GUILayout.Button("← Ввести другой ключ", GUILayout.Width(180), GUILayout.Height(30)))
                _step = Step.ApiKey;

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Создать конфиг →", _bigButtonStyle, GUILayout.Width(200)))
                CreateConfigAndFinish();

            GUI.enabled = true;
            EditorGUILayout.EndHorizontal();
        }

        private void DrawDone()
        {
            EditorGUILayout.LabelField("🎉 Настройка завершена", EditorStyles.boldLabel);
            GUILayout.Space(4);

            EditorGUILayout.LabelField(
                "Ассистент готов к работе. Открой окно через Window → DeepSeek → Assistant " +
                "или нажми кнопку ниже.",
                _bodyStyle);

            GUILayout.Space(10);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Примеры команд:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("• Создай куб на позиции 0 5 0", _bodyStyle);
            EditorGUILayout.LabelField("• Прочитай Assets/Scripts/Player.cs", _bodyStyle);
            EditorGUILayout.LabelField("• Найди все материалы в проекте", _bodyStyle);
            EditorGUILayout.EndVertical();

            GUILayout.Space(12);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("🔧 Открыть конфиг", GUILayout.Width(160), GUILayout.Height(30)))
            {
                var cfg = ExistingConfig();
                if (cfg != null)
                {
                    Selection.activeObject = cfg;
                    EditorGUIUtility.PingObject(cfg);
                }
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Открыть ассистент →", _bigButtonStyle, GUILayout.Width(200)))
            {
                Close();
                DeepSeekAssistantWindow.Open();
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawFailed()
        {
            EditorGUILayout.LabelField("❌ Проверка не удалась", EditorStyles.boldLabel);
            GUILayout.Space(4);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Ошибка:", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(_verifyError, _bodyStyle);
            EditorGUILayout.EndVertical();

            GUILayout.Space(8);
            EditorGUILayout.LabelField(
                "Возможные причины: неверный ключ, истёк, или на балансе 0. " +
                "Проверь ключ на platform.deepseek.com и попробуй снова.",
                _bodyStyle);

            GUILayout.Space(12);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("← Ввести другой ключ", GUILayout.Width(180), GUILayout.Height(30)))
                _step = Step.ApiKey;

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Открыть platform.deepseek.com", GUILayout.Height(30)))
                Application.OpenURL("https://platform.deepseek.com/api_keys");

            EditorGUILayout.EndHorizontal();
        }

        private void DrawFooter()
        {
            EditorGUILayout.BeginHorizontal();

            bool newDont = EditorGUILayout.ToggleLeft(
                "Не показывать этот мастер снова",
                _dontShowAgain);

            if (newDont != _dontShowAgain)
            {
                _dontShowAgain = newDont;
                EditorPrefs.SetBool(DontShowKey, _dontShowAgain);
            }

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Закрыть", GUILayout.Width(80)))
                Close();

            EditorGUILayout.EndHorizontal();
        }

        // =========================================================
        //  Логика проверки ключа
        // =========================================================

        private bool IsApiKeyLooksValid()
        {
            if (string.IsNullOrWhiteSpace(_apiKeyInput)) return false;
            string t = _apiKeyInput.Trim();
            return t.StartsWith("sk-") && t.Length >= 20;
        }

        private async Task VerifyAsync()
        {
            _isVerifying = true;
            _step = Step.Verifying;
            _verifyText = "Подключение...";
            Repaint();

            string apiKey = _apiKeyInput.Trim();

            try
            {
                string payload = JsonConvert.SerializeObject(new
                {
                    model = DefaultModel,
                    messages = new[]
                    {
                        new { role = "user", content = "Reply with just: ok" }
                    },
                    max_tokens = 8,
                    temperature = 0f,
                    stream = false
                });

                string url = $"{DefaultBaseUrl}/chat/completions";

                using var www = new UnityWebRequest(url, "POST");
                www.uploadHandler = Utf8Helper.CreateJsonUpload(payload);
                www.downloadHandler = new DownloadHandlerBuffer();
                www.SetRequestHeader("Authorization", $"Bearer {apiKey}");
                www.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
                www.timeout = 60;

                var op = www.SendWebRequest();

                float t0 = (float)EditorApplication.timeSinceStartup;
                while (!op.isDone)
                {
                    float elapsed = (float)EditorApplication.timeSinceStartup - t0;
                    _verifyText = $"Запрос отправлен... {elapsed:F1} сек";
                    Repaint();
                    await Task.Yield();
                }

                if (www.result != UnityWebRequest.Result.Success)
                {
                    string err = Utf8Helper.ReadJson(www.downloadHandler);

                    // 401 → неверный ключ
                    if (www.responseCode == 401)
                        throw new Exception("Ошибка 401: неверный или истёкший ключ.");
                    // 402 → нет баланса
                    if (www.responseCode == 402)
                        throw new Exception("Ошибка 402: на балансе DeepSeek нет средств. Пополни.");
                    // 429 → слишком часто
                    if (www.responseCode == 429)
                        throw new Exception("Ошибка 429: слишком много запросов. Подожди и попробуй снова.");

                    throw new Exception($"HTTP {www.responseCode}: {www.error}\n{Trim(err, 400)}");
                }

                string json = Utf8Helper.ReadJson(www.downloadHandler);
                var resp = JsonConvert.DeserializeObject<ChatResponse>(json);

                if (resp == null)
                    throw new Exception("Пустой ответ от API.");
                if (resp.IsError)
                    throw new Exception($"API error: {resp.Error}");

                _step = Step.Verified;
            }
            catch (Exception e)
            {
                _verifyError = e.Message;
                _step = Step.Failed;
            }
            finally
            {
                _isVerifying = false;
                Repaint();
            }
        }

        private static string Trim(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }

        // =========================================================
        //  Создание конфига
        // =========================================================

        private void CreateConfigAndFinish()
        {
            try
            {
                // 1. Папка
                if (!AssetDatabase.IsValidFolder(ConfigFolder))
                {
                    if (!AssetDatabase.IsValidFolder("Assets/DeepSeekAssistant"))
                        AssetDatabase.CreateFolder("Assets", "DeepSeekAssistant");
                }

                // 2. Конфиг
                string fullPath = $"{ConfigFolder}/{ConfigFileName}";

                var existing = AssetDatabase.LoadAssetAtPath<DeepSeekConfig>(fullPath);
                if (existing != null)
                {
                    bool overwrite = EditorUtility.DisplayDialog(
                        "DeepSeek",
                        $"Конфиг уже существует: {fullPath}\nПерезаписать ключ?",
                        "Да", "Отмена");

                    if (!overwrite) return;
                    AssetDatabase.DeleteAsset(fullPath);
                }

                var cfg = ScriptableObject.CreateInstance<DeepSeekConfig>();

                // Поля приватные — используем SerializedObject
                var so = new SerializedObject(cfg);
                so.FindProperty("apiKey").stringValue = _apiKeyInput.Trim();
                so.FindProperty("baseUrl").stringValue = DefaultBaseUrl;
                so.FindProperty("model").stringValue = DefaultModel;
                so.FindProperty("temperature").floatValue = 0.7f;
                so.FindProperty("maxTokens").intValue = 4096;
                so.FindProperty("timeoutSeconds").intValue = 180;
                so.FindProperty("useStreaming").boolValue = true;
                so.FindProperty("debugLogging").boolValue = false;
                so.ApplyModifiedPropertiesWithoutUndo();

                AssetDatabase.CreateAsset(cfg, fullPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // 3. Сохраняем guid
                string guid = AssetDatabase.AssetPathToGUID(fullPath);
                EditorPrefs.SetString(ConfigGuidKey, guid);

                // 4. Опционально — .gitignore
                OfferGitignore(fullPath);

                _step = Step.Done;

                Debug.Log($"[DeepSeek] Конфиг создан: {fullPath}");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                EditorUtility.DisplayDialog("DeepSeek",
                    "Ошибка при создании конфига: " + e.Message, "OK");
            }
        }

        private static void OfferGitignore(string configPath)
        {
            string gitignorePath = System.IO.Path.Combine(
                System.IO.Directory.GetParent(Application.dataPath).FullName,
                ".gitignore");

            if (!System.IO.File.Exists(gitignorePath))
                return; // нет .gitignore — не создаём

            string content = System.IO.File.ReadAllText(gitignorePath);

            string entry = System.IO.Path.GetFileName(configPath);
            if (content.Contains(entry))
                return; // уже есть

            bool add = EditorUtility.DisplayDialog(
                "DeepSeek · Безопасность",
                "Добавить запись в .gitignore, чтобы API-ключ не попал в Git?\n\n" +
                "Это рекомендуется, если проект в публичном репозитории.",
                "Да, добавить", "Нет");

            if (add)
            {
                System.IO.File.AppendAllText(gitignorePath,
                    "\n# DeepSeek API key\n" + entry + "\n" + entry + ".meta\n");
                AssetDatabase.Refresh();
                Debug.Log("[DeepSeek] Запись добавлена в .gitignore");
            }
        }
    }
}