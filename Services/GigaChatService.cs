using AIInterviewAssistant.WPF.Models;
using AIInterviewAssistant.WPF.Services.Interfaces;
using GigaChatAdapter.Models;
using GigaChatAdapter;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Diagnostics;
using System.Text;

namespace AIInterviewAssistant.WPF.Services
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