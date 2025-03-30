using AIInterviewAssistant.WPF.Services.Interfaces;
using GigaChatAdapter;
using System.Windows;

namespace AIInterviewAssistant.WPF.Services
{
    public class GigaChatService : IAIService
    {
        private Authorization? _auth;
        private Completion _completion = new();

        public async Task<bool> AuthAsync()
        {
            var authData = Application.Current.Properties["GigaChatToken"] as string ?? string.Empty;
            _auth = new Authorization(authData, GigaChatAdapter.Auth.RateScope.GIGACHAT_API_PERS);
            var authResult = await _auth.SendRequest();
            return authResult.AuthorizationSuccess;
        }
    
        public async Task<string> SendQuestionAsync(string question)
        {
            if (_auth is null)
                return string.Empty;

            await _auth.UpdateToken();
            var result = await _completion.SendRequest(_auth.LastResponse.GigaChatAuthorizationResponse?.AccessToken, question);

            return result.RequestSuccessed ? result.GigaChatCompletionResponse.Choices.LastOrDefault()?.Message.Content ?? string.Empty
                : result.ErrorTextIfFailed;
        }
    }
}