using System;

namespace AIInterviewAssistant.WPF.Models
{
    public class AiResponse
    {
        // The source AI service that generated this response
        public AiProvider Provider { get; set; }
        
        // The solution code
        public string Solution { get; set; } = string.Empty;
        
        // The explanation of the solution (if available)
        public string Explanation { get; set; } = string.Empty;
        
        // Timestamp when the response was received
        public DateTime Timestamp { get; set; } = DateTime.Now;
        
        // Flag to indicate if this is a solution only or full explanation
        public bool IsExplanation { get; set; }
        
        // The prompt that was sent to the AI service
        public string OriginalPrompt { get; set; } = string.Empty;
    }
    
    public enum AiProvider
    {
        GigaChat,
        Claude
    }
}