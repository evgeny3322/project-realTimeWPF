using AIInterviewAssistant.WPF.Models;
using System.Drawing;

namespace AIInterviewAssistant.WPF.Services.Interfaces
{
    public interface IOverlayService
    {
        /// <summary>
        /// Показывает решение задачи рядом с курсором
        /// </summary>
        /// <param name="response">Ответ AI для отображения</param>
        /// <param name="position">Позиция для отображения, если null, используется текущая позиция курсора</param>
        void ShowSolution(AiResponse response, Point? position = null);
        
        /// <summary>
        /// Показывает объяснение решения рядом с курсором
        /// </summary>
        /// <param name="response">Ответ AI с объяснением для отображения</param>
        /// <param name="position">Позиция для отображения, если null, используется текущая позиция курсора</param>
        void ShowExplanation(AiResponse response, Point? position = null);
        
        /// <summary>
        /// Скрывает все видимые оверлеи
        /// </summary>
        void HideOverlay();
        
        /// <summary>
        /// Показывает уведомление о том, что ответ готов
        /// </summary>
        /// <param name="message">Текст уведомления</param>
        void ShowNotification(string message);
        
        /// <summary>
        /// Получает, видим ли оверлей в настоящее время
        /// </summary>
        bool IsOverlayVisible { get; }
    }
}