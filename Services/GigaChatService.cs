using AIInterviewAssistant.WPF.Models;
using AIInterviewAssistant.WPF.Services.Interfaces;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Diagnostics;
using System.Text;
using System.Net.Http;
using System.Text.Json;

namespace AIInterviewAssistant.WPF.Services
{
    public class GigaChatService : IAIService
    {
        private HttpClient _httpClient;
        private string _authToken;
        private bool _isAuthenticated;
        private readonly Regex _codeBlockRegex = new Regex(@"```(?:[\w\-+#]*\n)?([\s\S]*?)```", RegexOptions.Compiled);
        
        private class ChatMessage
        {
            public string Role { get; set; }
            public string Content { get; set; }
        }
        
        private class ChatRequest
        {
            public ChatMessage[] Messages { get; set; }
            public double Temperature { get; set; }
            public int MaxTokens { get; set; }
        }
        
        private class ChatChoice
        {
            public ChatMessage Message { get; set; }
        }
        
        private class ChatResponse
        {
            public ChatChoice[] Choices { get; set; }
        }
        
        public GigaChatService()
        {
            _isAuthenticated = false;
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(60);
        }
        
        public async Task<bool> AuthAsync()
        {
            try
            {
                // Получаем настройки из Application.Current.Properties
                string clientId = Application.Current.Properties["GigaChatClientId"] as string;
                string clientSecret = Application.Current.Properties["GigaChatClientSecret"] as string;
                string scope = Application.Current.Properties["GigaChatScope"] as string;
                
                if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                {
                    Debug.WriteLine("[ERROR] GigaChat credentials are empty");
                    MessageBox.Show("Отсутствуют учетные данные GigaChat. Проверьте настройки.", 
                        "Ошибка авторизации", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                // Запрос на авторизацию к GigaChat API
                var authData = new
                {
                    client_id = clientId,
                    client_secret = clientSecret,
                    scope = scope
                };
                
                var content = new StringContent(
                    JsonSerializer.Serialize(authData),
                    Encoding.UTF8,
                    "application/json");
                
                // Здесь должен быть реальный URL для авторизации GigaChat API
                var response = await _httpClient.PostAsync("https://gigachat.api/v1/auth", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    // Предполагаем, что API возвращает токен в формате JSON с полем "access_token"
                    var tokenData = JsonSerializer.Deserialize<JsonElement>(jsonResponse);
                    
                    if (tokenData.TryGetProperty("access_token", out var token))
                    {
                        _authToken = token.GetString();
                        _isAuthenticated = true;
                        _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _authToken);
                        Debug.WriteLine("[INFO] GigaChat успешно авторизован");
                        return true;
                    }
                }
                
                Debug.WriteLine("[ERROR] Не удалось авторизоваться в GigaChat");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Ошибка авторизации GigaChat: {ex.Message}");
                MessageBox.Show($"Ошибка авторизации GigaChat: {ex.Message}", 
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                _isAuthenticated = false;
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
                // Проверяем авторизацию
                if (!_isAuthenticated)
                {
                    if (!await AuthAsync())
                    {
                        return "Ошибка авторизации GigaChat. Проверьте настройки.";
                    }
                }
                
                // Отправляем запрос
                var chatRequest = new ChatRequest
                {
                    Messages = new[]
                    {
                        new ChatMessage
                        {
                            Role = "user",
                            Content = question
                        }
                    },
                    Temperature = 0.7,
                    MaxTokens = 1500
                };
                
                var content = new StringContent(
                    JsonSerializer.Serialize(chatRequest),
                    Encoding.UTF8,
                    "application/json");
                
                // Здесь должен быть реальный URL для API GigaChat
                var response = await _httpClient.PostAsync("https://gigachat.api/v1/chat/completions", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var chatResponse = JsonSerializer.Deserialize<ChatResponse>(jsonResponse);
                    
                    if (chatResponse != null && chatResponse.Choices != null && chatResponse.Choices.Length > 0)
                    {
                        return chatResponse.Choices[0].Message.Content;
                    }
                }
                
                Debug.WriteLine("[ERROR] Пустой ответ от GigaChat");
                return "Ошибка: Пустой ответ от GigaChat";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Ошибка отправки запроса GigaChat: {ex.Message}");
                return $"Ошибка: {ex.Message}";
            }
        }
        
        public async Task<AiResponse> SolveProgrammingProblemAsync(string problem, bool needExplanation)
        {
            try
            {
                // Проверяем авторизацию
                if (!_isAuthenticated)
                {
                    if (!await AuthAsync())
                    {
                        return new AiResponse 
                        { 
                            Provider = AiProvider.GigaChat,
                            Solution = "Ошибка авторизации GigaChat. Проверьте настройки."
                        };
                    }
                }
                
                // Формируем запрос в зависимости от типа ответа
                string promptTemplate = needExplanation
                    ? "Реши задачу программирования и объясни решение:\n{0}\n\nФормат ответа:\n1. Код решения внутри блока ```\n2. Подробное объяснение алгоритма и кода"
                    : "Реши задачу программирования (только код решения):\n{0}\n\nВерни ТОЛЬКО код решения внутри блока ```";
                
                string prompt = string.Format(promptTemplate, problem);
                
                // Отправляем запрос
                var chatRequest = new ChatRequest
                {
                    Messages = new[]
                    {
                        new ChatMessage
                        {
                            Role = "user",
                            Content = prompt
                        }
                    },
                    Temperature = 0.2,  // Низкая температура для более точных ответов на технические вопросы
                    MaxTokens = 2500
                };
                
                var content = new StringContent(
                    JsonSerializer.Serialize(chatRequest),
                    Encoding.UTF8,
                    "application/json");
                
                // Здесь должен быть реальный URL для API GigaChat
                var response = await _httpClient.PostAsync("https://gigachat.api/v1/chat/completions", content);
                
                if (response.IsSuccessStatusCode)
                {
                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    var chatResponse = JsonSerializer.Deserialize<ChatResponse>(jsonResponse);
                    
                    if (chatResponse != null && chatResponse.Choices != null && chatResponse.Choices.Length > 0)
                    {
                        string fullResponse = chatResponse.Choices[0].Message.Content;
                        return ParseProgrammingResponse(fullResponse, problem, needExplanation);
                    }
                }
                
                Debug.WriteLine("[ERROR] Пустой ответ от GigaChat");
                return new AiResponse 
                { 
                    Provider = AiProvider.GigaChat,
                    Solution = "Ошибка: Пустой ответ от GigaChat",
                    OriginalPrompt = problem
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Ошибка решения задачи GigaChat: {ex.Message}");
                return new AiResponse 
                { 
                    Provider = AiProvider.GigaChat,
                    Solution = $"Ошибка: {ex.Message}",
                    OriginalPrompt = problem
                };
            }
        }
        
        private AiResponse ParseProgrammingResponse(string response, string originalPrompt, bool isExplanation)
        {
            var aiResponse = new AiResponse
            {
                Provider = AiProvider.GigaChat,
                OriginalPrompt = originalPrompt,
                IsExplanation = isExplanation,
                Timestamp = DateTime.Now
            };
            
            // Ищем блоки кода в ответе
            var codeBlockMatches = _codeBlockRegex.Matches(response);
            
            if (codeBlockMatches.Count > 0)
            {
                // Извлекаем первый блок кода как решение
                aiResponse.Solution = codeBlockMatches[0].Groups[1].Value.Trim();
                
                // Если требуется объяснение, то все, что не является блоком кода - это объяснение
                if (isExplanation)
                {
                    // Удаляем все блоки кода из ответа для получения чистого объяснения
                    string explanation = _codeBlockRegex.Replace(response, "").Trim();
                    aiResponse.Explanation = explanation;
                }
            }
            else
            {
                // Если блоков кода не найдено, считаем весь ответ решением
                aiResponse.Solution = response.Trim();
            }
            
            return aiResponse;
        }
    }
}
{
    public class GigaChatService : IAIService
    {
        private GigaChatClient _client;
        private bool _isAuthenticated;
        private readonly Regex _codeBlockRegex = new Regex(@"```(?:[\w\-+#]*\n)?([\s\S]*?)```", RegexOptions.Compiled);
        
        public GigaChatService()
        {
            _isAuthenticated = false;
        }
        
        public async Task<bool> AuthAsync()
        {
            try
            {
                // Получаем настройки из Application.Current.Properties
                string clientId = Application.Current.Properties["GigaChatClientId"] as string;
                string clientSecret = Application.Current.Properties["GigaChatClientSecret"] as string;
                string scope = Application.Current.Properties["GigaChatScope"] as string;
                
                if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                {
                    Debug.WriteLine("[ERROR] GigaChat credentials are empty");
                    MessageBox.Show("Отсутствуют учетные данные GigaChat. Проверьте настройки.", 
                        "Ошибка авторизации", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                // Создаем конфигурацию
                var config = new GigaChatClientConfig
                {
                    ClientId = clientId,
                    ClientSecret = clientSecret,
                    Scope = scope
                };
                
                // Создаем клиента и авторизуемся
                _client = new GigaChatClient(config);
                await _client.AuthorizeAsync();
                
                _isAuthenticated = true;
                Debug.WriteLine("[INFO] GigaChat успешно авторизован");
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Ошибка авторизации GigaChat: {ex.Message}");
                MessageBox.Show($"Ошибка авторизации GigaChat: {ex.Message}", 
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                _isAuthenticated = false;
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
                // Проверяем авторизацию
                if (!_isAuthenticated)
                {
                    if (!await AuthAsync())
                    {
                        return "Ошибка авторизации GigaChat. Проверьте настройки.";
                    }
                }
                
                // Отправляем запрос
                var chatRequest = new ChatRequest
                {
                    Messages = new[]
                    {
                        new ChatMessage
                        {
                            Role = ChatRole.User,
                            Content = question
                        }
                    },
                    Temperature = 0.7,
                    MaxTokens = 1500
                };
                
                var response = await _client.CompletionAsync(chatRequest);
                
                if (response != null && response.Choices != null && response.Choices.Length > 0)
                {
                    return response.Choices[0].Message.Content;
                }
                else
                {
                    Debug.WriteLine("[ERROR] Пустой ответ от GigaChat");
                    return "Ошибка: Пустой ответ от GigaChat";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Ошибка отправки запроса GigaChat: {ex.Message}");
                return $"Ошибка: {ex.Message}";
            }
        }
        
        public async Task<AiResponse> SolveProgrammingProblemAsync(string problem, bool needExplanation)
        {
            try
            {
                // Проверяем авторизацию
                if (!_isAuthenticated)
                {
                    if (!await AuthAsync())
                    {
                        return new AiResponse 
                        { 
                            Provider = AiProvider.GigaChat,
                            Solution = "Ошибка авторизации GigaChat. Проверьте настройки."
                        };
                    }
                }
                
                // Формируем запрос в зависимости от типа ответа
                string promptTemplate = needExplanation
                    ? "Реши задачу программирования и объясни решение:\n{0}\n\nФормат ответа:\n1. Код решения внутри блока ```\n2. Подробное объяснение алгоритма и кода"
                    : "Реши задачу программирования (только код решения):\n{0}\n\nВерни ТОЛЬКО код решения внутри блока ```";
                
                string prompt = string.Format(promptTemplate, problem);
                
                // Отправляем запрос
                var chatRequest = new ChatRequest
                {
                    Messages = new[]
                    {
                        new ChatMessage
                        {
                            Role = ChatRole.User,
                            Content = prompt
                        }
                    },
                    Temperature = 0.2,  // Низкая температура для более точных ответов на технические вопросы
                    MaxTokens = 2500
                };
                
                var response = await _client.CompletionAsync(chatRequest);
                
                if (response != null && response.Choices != null && response.Choices.Length > 0)
                {
                    string fullResponse = response.Choices[0].Message.Content;
                    return ParseProgrammingResponse(fullResponse, problem, needExplanation);
                }
                else
                {
                    Debug.WriteLine("[ERROR] Пустой ответ от GigaChat");
                    return new AiResponse 
                    { 
                        Provider = AiProvider.GigaChat,
                        Solution = "Ошибка: Пустой ответ от GigaChat",
                        OriginalPrompt = problem
                    };
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Ошибка решения задачи GigaChat: {ex.Message}");
                return new AiResponse 
                { 
                    Provider = AiProvider.GigaChat,
                    Solution = $"Ошибка: {ex.Message}",
                    OriginalPrompt = problem
                };
            }
        }
        
        private AiResponse ParseProgrammingResponse(string response, string originalPrompt, bool isExplanation)
        {
            var aiResponse = new AiResponse
            {
                Provider = AiProvider.GigaChat,
                OriginalPrompt = originalPrompt,
                IsExplanation = isExplanation,
                Timestamp = DateTime.Now
            };
            
            // Ищем блоки кода в ответе
            var codeBlockMatches = _codeBlockRegex.Matches(response);
            
            if (codeBlockMatches.Count > 0)
            {
                // Извлекаем первый блок кода как решение
                aiResponse.Solution = codeBlockMatches[0].Groups[1].Value.Trim();
                
                // Если требуется объяснение, то все, что не является блоком кода - это объяснение
                if (isExplanation)
                {
                    // Удаляем все блоки кода из ответа для получения чистого объяснения
                    string explanation = _codeBlockRegex.Replace(response, "").Trim();
                    aiResponse.Explanation = explanation;
                }
            }
            else
            {
                // Если блоков кода не найдено, считаем весь ответ решением
                aiResponse.Solution = response.Trim();
            }
            
            return aiResponse;
        }
    }
}