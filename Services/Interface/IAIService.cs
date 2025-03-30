namespace AIInterviewAssistant.WPF.Services.Interfaces
{
    public interface IAIService
    {
        Task<string> SendQuestionAsync(string question);
        Task<bool> AuthAsync();
    }
}