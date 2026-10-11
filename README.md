# DeepSeek Unity Assistant

Бесплатный AI-ассистент для Unity Editor на базе **DeepSeek**.  
Поддержка русского языка, function calling, работа со сценой, скриптами, ассетами, рельефом и префабами — прямо из редактора.

![Version](https://img.shields.io/badge/version-2.0.0-blue)
![License](https://img.shields.io/badge/license-MIT-green)
![Unity](https://img.shields.io/badge/unity-2021.3%2B-black)

**Автор:** Галавинов Давид Викторович  
**Лицензия:** MIT  
**Версия:** 2.0.0

---

## ✨ Возможности

### 🎯 49 инструментов — ассистент реально рулит проектом

| Категория | Инструменты |
|---|---|
| **Сцена** | `inspect_scene`, `create_gameobject`, `delete_gameobject`, `set_transform`, `inspect_object`, `list_root_objects` |
| **Скрипты** | `read_script`, `create_script` |
| **Ассеты** | `find_assets` |
| **Материалы и компоненты** | `create_material`, `assign_material`, `add_component`, `remove_component`, `create_prefab`, `set_light` |
| **Рельеф и спавн** | `move_asset`, `duplicate_object`, `align_to_ground`, `terrain_get_height`, `terrain_place_on_surface`, `scatter_objects`, `spawn_loot_on_terrain`, `list_terrains` |
| **Выделение** | `get_selection`, `align_selection_to_ground`, `delete_selection`, `duplicate_selection`, `add_component_to_selection`, `align_all_to_ground` |
| **Префабы** | `find_prefab_instances`, `open_prefab`, `apply_prefab_overrides`, `revert_prefab_overrides`, `unpack_prefab` |
| **Пакетные операции** | `batch_rename`, `batch_texture_import`, `batch_change_shader`, `find_duplicate_materials`, `find_assets_in_folder` |

### 💬 Продвинутый чат
- **Стриминг ответов** — текст печатается в реальном времени
- **Кнопка Stop** — мгновенно прервать генерацию
- **Русский язык** — исправлена проблема UTF-8, кириллица работает без кракозябр
- **Выделение текста мышью** — как в обычном поле ввода
- **Ctrl+C / Ctrl+A** — нативное копирование выделенного фрагмента
- **Кнопка C** у каждого сообщения — скопировать целиком
- **Ctrl+Shift+C** — копировать весь диалог
- **Правый клик** — контекстное меню (копировать / с заголовком / весь диалог / очистить)

### 📷 Скриншоты
- **Scene View** — снять текущий вид сцены
- **Game View** — снять рендер главной камеры
- Файлы сохраняются в `Library/DeepSeek/screenshots/`

### ↶ Undo AI
- Все изменения за **один запрос** склеиваются в **одну Undo-группу**
- **Ctrl+Z** один раз — откатились все действия ассистента
- Кнопка **↶ Undo AI** в шапке — то же самое

### 📚 Макросы
- Набор сохранённых промптов в `Assets/DeepSeekAssistant/DeepSeekMacros.asset`
- Клик в выпадающем меню **Macros ▾** — промпт вставляется в поле ввода
- Можно делиться с командой через репозиторий

### 💾 История
- Автосохранение после каждого ответа в `Library/DeepSeek/history.json`
- Автозагрузка при открытии окна
- Кнопки **Save** / **Load** / **New** / **Clear**

### 💰 Дешёвый API
- DeepSeek берёт ~$0.14 за миллион входных токенов
- $2 хватит на **тысячи** запросов
- Модели: `deepseek-chat` (V3) и `deepseek-reasoner` (R1)

---

## 📦 Установка

### Вариант 1: Git URL

1. Открой **Window → Package Manager**
2. Нажми **+ → Add package from git URL...**
3. Вставь ссылку:
https://github.com/ТВОЙ_РЕПОЗИТОРИЙ/DeepSeekUnityAssistant.git

text

### Вариант 2: Вручную

1. Скопируй папку пакета в `[https://github.com/David00711/DeepSeekUnityAssistant](https://github.com/David00711/DeepSeekUnityAssistant.git)`
2. Открой Unity, дождись компиляции

### Зависимости

Пакет требует **Newtonsoft.Json 3.2.1** — устанавливается автоматически через `package.json`.

Если по какой-то причине не установился:
**Window → Package Manager → + → Add package by name...** → `com.unity.nuget.newtonsoft-json`

---

## 🚀 Быстрый старт

### Шаг 1: Получи API-ключ

1. Зарегистрируйся на [platform.deepseek.com](https://platform.deepseek.com)
2. Перейди в [API Keys](https://platform.deepseek.com/api_keys) → **Create new key**
3. Скопируй ключ (начинается с `sk-`)
4. Пополни баланс — минимум **$2**

### Шаг 2: Запусти Setup Wizard

**Он откроется сам** при первом импорте пакета. Если пропустил — **Window → DeepSeek → Setup Wizard**.

Мастер проведёт через 4 шага: приветствие → ввод ключа → проверка → создание конфига.

### Шаг 3: Открой ассистента

**Window → DeepSeek → Assistant** (или **Ctrl+Shift+A**)

### Шаг 4: Попробуй
Создай куб на позиции 0 5 0 и назови его TestCube
Прижми всё выделенное к земле
Разбросай 30 копий Tree в радиусе 50 вокруг 0 0
Найди дубликаты материалов в проекте

text

---

## 🛠️ Примеры команд

### Работа со сценой
Создай куб на позиции 0 5 0
Удали объект Old_Box
Перемести Player в позицию 100 0 200
Покажи все объекты сцены

text

### Рельеф и спавн
Какая высота рельефа в точке 500 300?
Помести куб на рельеф в точку 500 300
Разбросай 100 копий Tree в радиусе 200 вокруг 0 0
Прижми Player к земле со смещением 0.1

text

### Выделение
Что сейчас выделено?
Прижми всё выделенное к земле
Удали всё выделенное
Дублируй выделенные объекты со сдвигом 5 по X
Добавь Rigidbody на всё выделенное

text

### Материалы и свет
Создай материал MyRed с цветом #FF0000
Назначь MyRed на объект Cube
Настрой Directional Light интенсивность 1.5 и цвет #FFF5E0

text

### Скрипты
Прочитай Assets/Scripts/Player.cs и объясни, что он делает
Создай скрипт Assets/Scripts/MyTool.cs с классом MyTool
Найди все скрипты, содержащие слово "Damage"

text

### Префабы
Найди все инстансы Assets/Prefabs/Tree.prefab на сцене
Открой префаб Assets/Prefabs/Enemy.prefab
Примени оверрайды выделенных объектов к их префабам
Распакуй выделенные префабы

text

### Пакетные операции
Переименуй все ассеты в Assets/Old с "Old_" на "New_"
Измени max_size всех текстур в Assets/UI на 1024
Смени шейдер всех материалов в Assets/Legacy на Universal Render Pipeline/Lit
Найди дубликаты материалов в проекте

text

---

## ⌨️ Горячие клавиши

| Комбинация | Действие |
|---|---|
| **Ctrl+Shift+A** | Открыть/сфокусировать Assistant |
| **Ctrl+Shift+G** | Открыть Config |
| **Enter** | Отправить сообщение |
| **Shift+Enter** | Перенос строки |
| **Ctrl+C** | Копировать выделенный фрагмент текста |
| **Ctrl+A** | Выделить всё в поле ввода |
| **Ctrl+Shift+C** | Копировать весь диалог |
| **Ctrl+Z** | Откатить последний запрос ассистента (все изменения разом) |

---

## ⚙️ Настройки

### `DeepSeekConfig` — поля

| Поле | Описание |
|---|---|
| `apiKey` | API-ключ DeepSeek (обязательно) |
| `baseUrl` | URL API, по умолчанию `https://api.deepseek.com` |
| `model` | Модель: `deepseek-chat` (V3) или `deepseek-reasoner` (R1) |
| `temperature` | Креативность: 0 — детерминированно, 2 — хаотично |
| `maxTokens` | Максимум токенов в ответе |
| `timeoutSeconds` | Таймаут запроса (по умолчанию 180) |
| `useStreaming` | Включить стриминг ответа (SSE) |
| `debugLogging` | Логировать запросы/ответы в Console |

### Макросы

Создай файл через кнопку **Macros ▾ → Создать файл макросов**:
`Assets/DeepSeekAssistant/DeepSeekMacros.asset`

Открой в инспекторе, добавь свои промпты. Формат:
```csharp
name = "🌲 Разбросать деревья"
prompt = "Разбросай 100 копий Tree в радиусе 150 вокруг 0 0"
🧩 Добавление своих инструментов
Создай класс, наследующий IDeepSeekTool:

csharp
using System.Threading.Tasks;
using DeepSeekUnity.Editor.Tools;

public sealed class MyCustomTool : IDeepSeekTool
{
    public string Name => "my_custom_action";
    public string Description => "Что делает инструмент (видит модель)";
    public object ParametersSchema => new
    {
        type = "object",
        properties = new
        {
            message = new { type = "string", description = "Что передать" }
        },
        required = new[] { "message" }
    };

    public Task<string> ExecuteAsync(string jsonArguments)
    {
        var args = Newtonsoft.Json.Linq.JObject.Parse(jsonArguments ?? "{}");
        string msg = args["message"]?.ToString();

        // Твоя логика...

        return Task.FromResult("Готово!");
    }
}
Зарегистрируй в DeepSeekAssistantWindow.RegisterTools():

csharp
_tools.Add(new MyCustomTool());
Перезапусти Unity — инструмент появится в списке.

🔐 Безопасность
API-ключ хранится в ScriptableObject (DeepSeekConfig.asset)

Никогда не коммить этот файл в публичный Git

Setup Wizard опционально предлагает добавить запись в .gitignore

Ключ передаётся только в заголовке Authorization: Bearer ... к api.deepseek.com

Все запросы идут напрямую, без посредников

Рекомендуемый .gitignore:

gitignore
# DeepSeek
DeepSeekConfig.asset
DeepSeekConfig.asset.meta
*.apikey
.env
🐛 Известные ограничения
Стриминг не работает на WebGL — редактор Unity и так не экспортируется в WebGL

Максимум 8 шагов в одном запросе (agent loop) — защита от зацикливания

Контекст растёт — длинные диалоги съедают токены. Жми Clear почаще

deepseek-reasoner (R1) иногда отвечает медленно из-за длинных цепочек рассуждений

Работа с рельефом требует, чтобы Terrains были загружены в сцене

Скриншоты не отправляются в модель — DeepSeek V3 не поддерживает vision. Их можно смотреть вручную или использовать как логи

🗺️ Roadmap
☑ Клиент DeepSeek с UTF-8 fix
☑ Function calling — 40 инструментов
☑ Окно ассистента с agent loop
☑ Setup Wizard
☑ SSE-стриминг
☑ Копирование текста (выделение + кнопки)
☑ Undo-транзакции (откат одного запроса)
☑ Макросы
☑ Скриншоты Scene/Game View
☑ Работа с выделением
☑ Работа с префабами
☑ Пакетные операции с ассетами
□ Хранение ключа в EditorPrefs (вместо ассета)
□ Многосессионность (список диалогов)
□ Поддержка vision-моделей (скриншот → анализ)
□ Play Mode: запуск игры, чтение Console, эмуляция ввода
□ Интеграция с Technogen (спавн лута, предметы, инвентарь)
❓ FAQ
Q: Сколько стоит использование?
A: DeepSeek берёт ~$0.14 за миллион входных токенов. Один запрос с 40 инструментами стоит меньше цента. $2 хватит на 1000+ запросов.

Q: Модель путается, отвечает криво на русском.
A: Используй deepseek-chat (V3). Если что-то ломается — напиши «отвечай по-английски», латиница работает точно.

Q: Ассистент не вызывает инструменты, просто болтает.
A: Проверь модель — deepseek-chat или deepseek-reasoner. Включи Debug Logging в конфиге, посмотри, что уходит в запрос.

Q: Ошибка 401 Authentication failed.
A: Ключ неверный / истёк / с пробелом. Пересоздай на platform.deepseek.com.

Q: Ошибка 402 Insufficient Balance.
A: Пополни баланс.

Q: Скриншоты сохраняются, но что с ними делать?
A: Смотри вручную. DeepSeek V3 не поддерживает vision. В roadmap — интеграция с vision-моделями.

Q: Как откатить действия ассистента?
A: Ctrl+Z один раз — откатывается весь запрос целиком. Или кнопка ↶ Undo AI в шапке.

Q: Можно ли использовать в коммерческом проекте?
A: Да, MIT позволяет всё — включая коммерческое использование, модификацию, распространение. Сохраняй уведомление об авторстве.

Q: Могу ли я добавить свои инструменты?
A: Да, смотри раздел «Добавление своих инструментов» выше.

Q: Конфликт с UNAI?
A: Namespace разные (DeepSeekUnity vs UnAI). Но лучше держать активным только один плагин.

🤝 Вклад
Pull requests приветствуются. Перед крупными изменениями — открой Issue.

Стиль кода:

C# 9+, .NET Standard 2.1

Namespace: DeepSeekUnity.*

4 пробела, не табы

XML-комментарии на публичных методах

📄 Лицензия
MIT License © 2026 Галавинов Давид Викторович



Полный текст — в файле LICENSE.

📞 Контакты
Автор: Галавинов Давид Викторович

Issues: GitHub Issues

Discussions: GitHub Discussions

⭐ Благодарности
DeepSeek — за дешёвый и мощный API



Если плагин помог — поставь ⭐ на GitHub!
