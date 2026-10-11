using System.Text;
using UnityEngine.Networking;

namespace DeepSeekUnity.Shared
{
    /// <summary>
    /// Утилиты для корректной работы с UTF-8 (кириллица).
    /// Без явного указания кодировки Unity по умолчанию использует
    /// системную, что ломает русские буквы в запросах/ответах.
    /// </summary>
    public static class Utf8Helper
    {
        /// <summary>
        /// Создаёт UploadHandlerRaw с явным UTF-8 и правильным Content-Type.
        /// Используется для отправки JSON-запросов в DeepSeek API.
        /// </summary>
        public static UploadHandlerRaw CreateJsonUpload(string json)
        {
            if (string.IsNullOrEmpty(json))
                json = "{}";

            byte[] bytes = Encoding.UTF8.GetBytes(json);
            var handler = new UploadHandlerRaw(bytes)
            {
                contentType = "application/json; charset=utf-8"
            };
            return handler;
        }

        /// <summary>
        /// Читает тело ответа как UTF-8.
        /// Возвращает пустую строку, если данных нет.
        /// </summary>
        public static string ReadJson(DownloadHandler handler)
        {
            if (handler == null) return string.Empty;

            byte[] data = handler.data;
            if (data == null || data.Length == 0) return string.Empty;

            return Encoding.UTF8.GetString(data);
        }

        /// <summary>
        /// Безопасно декодирует произвольный массив байт как UTF-8.
        /// </summary>
        public static string Decode(byte[] bytes, int length = -1)
        {
            if (bytes == null || bytes.Length == 0) return string.Empty;
            if (length < 0 || length > bytes.Length) length = bytes.Length;

            return Encoding.UTF8.GetString(bytes, 0, length);
        }

        /// <summary>
        /// Кодирует строку в UTF-8 байты.
        /// </summary>
        public static byte[] Encode(string text)
        {
            if (string.IsNullOrEmpty(text)) return new byte[0];
            return Encoding.UTF8.GetBytes(text);
        }
    }
}