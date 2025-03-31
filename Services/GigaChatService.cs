using AIInterviewAssistant.WPF.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Diagnostics;

namespace AIInterviewAssistant.WPF.Services
{
    public class ClaudeService : IAIService
    {
        private const string ClaudeApiUrl = "https://api.anthropic.com/v1/messages";
        
        private string _apiKey;
        private readonly HttpClient _httpClient;
        
        // Классы для десериализации ответа API
        private class ClaudeResponse
        {
            public string id { get; set; }
            public string type { get; set; }
            public string role { get; set; }
            public string model { get; set; }
            public Content content { get; set; }
            
            public class Content
            {
                public string type { get; set; }
                public string text { get; set; }
            }
        }
        
        public ClaudeService()
        {
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
            _httpClient.Timeout = TimeSpan.FromSeconds(60); // Увеличиваем таймаут до 60 секунд
        }
        
        public async Task<bool> AuthAsync()
        {
            try
            {
                // Получаем API ключ из настроек приложения
                _apiKey = Application.Current.Properties["ClaudeApiKey"] as string;
                
                if (string.IsNullOrWhiteSpace(_apiKey))
                {
                    Debug.WriteLine("[ERROR] Claude API Key is empty");
                    MessageBox.Show("API ключ Claude отсутствует или пустой.", "Ошибка авторизации", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
                
                // Устанавливаем API ключ в заголовки
                _httpClient.DefaultRequestHeaders.Remove("x-api-key");
                _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
                
                Debug.WriteLine("[INFO] Claude API ключ установлен успешно");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Ошибка авторизации Claude: {ex.Message}");
                MessageBox.Show($"Ошибка авторизации Claude: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
        
        public async Task<string> SendQuestionAsync(string question)
        {
            if (string.IsNullOrWhiteSpace(question))
            {
                return string.Empty;
            }
            
            try
            {
                // Проверяем наличие API ключа
                if (string.IsNullOrEmpty(_apiKey))
                {
                    Debug.WriteLine("[ERROR] Claude API Key не установлен");
                    if (!await AuthAsync())
                    {
                        return "Ошибка авторизации. Проверьте настройки Claude API.";
                    }
                }
                
                // Формируем данные запроса в формате JSON
                var requestData = new
                {
                    model = "claude-3-opus-20240229",
                    messages = new[]
                    {
                        new { role = "user", content = question }
                    },
                    max_tokens = 4000,
                    temperature = 0.7
                };
                
                string jsonRequest = JsonSerializer.Serialize(requestData);
                Debug.WriteLine($"[DEBUG] Claude запрос: {jsonRequest}");
                
                var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                
                // Устанавливаем API ключ в заголовки для этого запроса
                _httpClient.DefaultRequestHeaders.Remove("x-api-key");
                _httpClient.DefaultRequestHeaders.Add("x-api-key", _apiKey);
                
                Debug.WriteLine($"[DEBUG] Отправка запроса к Claude API: {ClaudeApiUrl}");
                
                // Отправляем запрос на генерацию ответа
                HttpResponseMessage response = await _httpClient.PostAsync(ClaudeApiUrl, content);
                
                string responseContent = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[DEBUG] Статус ответа Claude: {response.StatusCode}");
                Debug.WriteLine($"[DEBUG] Предпросмотр ответа Claude: {(responseContent.Length > 100 ? responseContent.Substring(0, 100) + "..." : responseContent)}");
                
                // Обрабатываем ответ
                if (response.IsSuccessStatusCode)
                {
                    try
                    {
                        var claudeResponse = JsonSerializer.Deserialize<ClaudeResponse>(responseContent);
                        
                        if (claudeResponse != null && claudeResponse.content != null)
                        {
                            string result = claudeResponse.content.text;
                            Debug.WriteLine($"[DEBUG] Успешно получен ответ от Claude, длина: {result.Length}");
                            return result;
                        }
                        
                        Debug.WriteLine("[ERROR] Ответ Claude не содержит текста");
                        return "Нет ответа от Claude.";
                    }
                    catch (JsonException jsonEx)
                    {
                        Debug.WriteLine($"[ERROR] Ошибка разбора JSON от Claude: {jsonEx.Message}");
                        return $"Ошибка разбора ответа Claude: {jsonEx.Message}";
                    }
                }
                else
                {
                    Debug.WriteLine($"[ERROR] Запрос к Claude не удался: {response.StatusCode}");
                    return $"Ошибка запроса к Claude: {response.StatusCode} - {responseContent}";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Исключение при запросе к Claude: {ex.Message}");
                return $"Исключение при запросе к Claude: {ex.Message}";
            }
        }
    }
}