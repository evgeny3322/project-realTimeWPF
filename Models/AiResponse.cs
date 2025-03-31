using System;

namespace AIInterviewAssistant.WPF.Models
{
    public class AiResponse
    {
        // Источник ответа AI
        public AiProvider Provider { get; set; }
        
        // Код решения
        public string Solution { get; set; } = string.Empty;
        
        // Объяснение решения (если доступно)
        public string Explanation { get; set; } = string.Empty;
        
        // Время получения ответа
        public DateTime Timestamp { get; set; } = DateTime.Now;
        
        // Флаг, указывающий, содержит ли ответ объяснение
        public bool IsExplanation { get; set; }
        
        // Исходный запрос, отправленный в AI
        public string OriginalPrompt { get; set; } = string.Empty;
        
        // Время выполнения запроса в миллисекундах
        public long ExecutionTimeMs { get; set; }
    }
    
    public enum AiProvider
    {
        GigaChat
    }
}