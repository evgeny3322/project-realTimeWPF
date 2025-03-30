using System.Threading.Tasks;

namespace AIInterviewAssistant.WPF.Services.Interfaces
{
    public interface IAIService
    {
        /// <summary>
        /// Выполняет аутентификацию в сервисе AI
        /// </summary>
        /// <returns>True если аутентификация успешна, иначе False</returns>
        Task<bool> AuthAsync();
        
        /// <summary>
        /// Отправляет вопрос в сервис AI и получает ответ
        /// </summary>
        /// <param name="question">Текст вопроса</param>
        /// <returns>Текст ответа от AI</returns>
        Task<string> SendQuestionAsync(string question);
    }
}