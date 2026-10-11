using System.Threading.Tasks;

namespace DeepSeekUnity.Editor.Tools
{
    /// <summary>
    /// Интерфейс инструмента, который может вызывать AI через function calling.
    /// </summary>
    public interface IDeepSeekTool
    {
        /// <summary>Уникальное имя функции (латиница, snake_case).</summary>
        string Name { get; }

        /// <summary>Описание для модели — что делает и когда вызывать.</summary>
        string Description { get; }

        /// <summary>JSON-схема параметров (объект OpenAI-формата).</summary>
        object ParametersSchema { get; }

        /// <summary>
        /// Выполнить инструмент. Возвращает строку-результат,
        /// которая вернётся модели как tool message.
        /// </summary>
        Task<string> ExecuteAsync(string jsonArguments);
    }
}