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
    public class GigaChatService : IAIService
    {
        private const string OAuthUrl = "https://ngw.devices.sberbank.ru:9443/api/v2/oauth";
        private const string ChatCompletionsUrl = "https://gigachat.devices.sberbank.ru/api/v1/chat/completions";
        
        private string _accessToken;
        private DateTime _lastAuthTime = DateTime.MinValue;
        private readonly object _lockObject = new object();
        
        // Класс для десериализации ответа с токеном
        private class TokenResponse
        {
            public string access_token { get; set; }
            public long expires_at { get; set; }
        }
        
        // Классы для десериализации ответа API
        private class CompletionResponse
        {
            public Choice[] choices { get; set; }
            
            public class Choice
            {
                public Message message { get; set; }
                public string finish_reason { get; set; }
                public int index { get; set; }
            }
            
            public class Message
            {
                public string role { get; set; }
                public string content { get; set; }
            }
        }
        
        public async Task<bool> AuthAsync()
        {
            try
            {
                // Получаем данные аутентификации из настроек приложения
                string clientId = Application.Current.Properties["GigaChatClientId"] as string;
                string clientSecret = Application.Current.Properties["GigaChatClientSecret"] as string;
                string scope = Application.Current.Properties["GigaChatScope"] as string ?? "GIGACHAT_API_PERS";
                
                // Логируем информацию о настройках для отладки
                Debug.WriteLine($"[DEBUG] Auth settings - ClientID: {clientId}, Secret: {clientSecret?.Substring(0, 4)}***, Scope: {scope}");
                
                // Проверяем наличие необходимых данных
                if (string.IsNullOrWhiteSpace(clientId))
                {
                    Debug.WriteLine("[ERROR] ClientId is empty");
                    MessageBox.Show("ClientId отсутствует или пустой.", "Ошибка авторизации", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
                
                if (string.IsNullOrWhiteSpace(clientSecret))
                {
                    Debug.WriteLine("[ERROR] ClientSecret is empty");
                    MessageBox.Show("ClientSecret отсутствует или пустой.", "Ошибка авторизации", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
                
                // Формируем Base64 ключ авторизации из client_id и client_secret
                string authKey = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{clientId}:{clientSecret}"));
                Debug.WriteLine($"[DEBUG] Base64 Auth Key: {authKey}");
                
                // Создаем HTTP клиент с отключенной проверкой сертификата для отладки
                using (HttpClient client = new HttpClient(GetInsecureHandler()))
                {
                    // Генерируем уникальный RqUID
                    string rquid = Guid.NewGuid().ToString();
                    Debug.WriteLine($"[DEBUG] Using RqUID: {rquid}");
                    
                    // Настраиваем заголовки запроса согласно документации
                    client.DefaultRequestHeaders.Clear();
                    client.DefaultRequestHeaders.Add("Authorization", $"Basic {authKey}");
                    client.DefaultRequestHeaders.Add("Accept", "application/json");
                    client.DefaultRequestHeaders.Add("RqUID", rquid);
                    
                    // Формируем данные запроса
                    var content = new FormUrlEncodedContent(new Dictionary<string, string>
                    {
                        { "scope", scope }
                    });
                    
                    Debug.WriteLine($"[DEBUG] Sending auth request to {OAuthUrl}");
                    
                    // Отправляем запрос на авторизацию
                    HttpResponseMessage response = await client.PostAsync(OAuthUrl, content);
                    
                    string responseContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[DEBUG] Response status: {response.StatusCode}");
                    Debug.WriteLine($"[DEBUG] Response content: {responseContent}");
                    
                    // Обрабатываем ответ
                    if (response.IsSuccessStatusCode)
                    {
                        try
                        {
                            var tokenData = JsonSerializer.Deserialize<TokenResponse>(responseContent);
                            
                            if (tokenData != null && !string.IsNullOrEmpty(tokenData.access_token))
                            {
                                _accessToken = tokenData.access_token;
                                _lastAuthTime = DateTime.Now;
                                Debug.WriteLine($"[DEBUG] Received access token: {_accessToken.Substring(0, 15)}...");
                                Debug.WriteLine($"[DEBUG] Token expires at: {tokenData.expires_at}");
                                return true;
                            }
                            else
                            {
                                Debug.WriteLine("[ERROR] Token data is null or access_token is empty");
                                MessageBox.Show("Получен пустой токен от сервера.", "Ошибка авторизации", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                        catch (JsonException jsonEx)
                        {
                            Debug.WriteLine($"[ERROR] JSON parsing error: {jsonEx.Message}");
                            MessageBox.Show($"Ошибка разбора ответа сервера: {jsonEx.Message}", "Ошибка JSON", MessageBoxButton.OK, MessageBoxImage.Error);
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"[ERROR] Auth failed with status code: {response.StatusCode}");
                        MessageBox.Show($"Авторизация не удалась. Код: {response.StatusCode}\nОтвет: {responseContent}", "Ошибка авторизации", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EXCEPTION] Auth Exception: {ex.Message}");
                Debug.WriteLine($"[EXCEPTION] Stack trace: {ex.StackTrace}");
                MessageBox.Show($"Исключение при авторизации: {ex.Message}", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
        
        public async Task<string> SendQuestionAsync(string question)
        {
            if (string.IsNullOrWhiteSpace(question))
            {
                return string.Empty;
            }
            
            // Проверяем наличие токена и его срок действия (токен действует 30 минут)
            if (string.IsNullOrEmpty(_accessToken) || (DateTime.Now - _lastAuthTime).TotalMinutes > 29)
            {
                Debug.WriteLine("[DEBUG] Token is missing or expired, requesting new token");
                if (!await AuthAsync())
                {
                    return "Ошибка авторизации. Проверьте настройки GigaChat.";
                }
            }
            
            try
            {
                // Создаем HTTP клиент
                using (HttpClient client = new HttpClient(GetInsecureHandler()))
                {
                    // Настраиваем заголовки запроса согласно документации
                    client.DefaultRequestHeaders.Clear();
                    client.DefaultRequestHeaders.Add("Authorization", $"Bearer {_accessToken}");
                    client.DefaultRequestHeaders.Add("Accept", "application/json");
                    
                    // Формируем данные запроса в формате JSON согласно документации
                    var requestData = new
                    {
                        model = "GigaChat",
                        messages = new[]
                        {
                            new { role = "user", content = question }
                        },
                        temperature = 0.7,
                        max_tokens = 2048
                    };
                    
                    string jsonRequest = JsonSerializer.Serialize(requestData);
                    Debug.WriteLine($"[DEBUG] Request data: {jsonRequest}");
                    
                    var content = new StringContent(jsonRequest, Encoding.UTF8, "application/json");
                    
                    Debug.WriteLine($"[DEBUG] Sending question to {ChatCompletionsUrl}");
                    
                    // Отправляем запрос на генерацию ответа
                    HttpResponseMessage response = await client.PostAsync(ChatCompletionsUrl, content);
                    
                    string responseContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[DEBUG] Response status: {response.StatusCode}");
                    Debug.WriteLine($"[DEBUG] Response content preview: {(responseContent.Length > 100 ? responseContent.Substring(0, 100) + "..." : responseContent)}");
                    
                    // Обрабатываем ответ
                    if (response.IsSuccessStatusCode)
                    {
                        try
                        {
                            var completionResponse = JsonSerializer.Deserialize<CompletionResponse>(responseContent);
                            
                            if (completionResponse != null && 
                                completionResponse.choices != null && 
                                completionResponse.choices.Length > 0)
                            {
                                string result = completionResponse.choices[0].message.content;
                                Debug.WriteLine($"[DEBUG] Successfully parsed response, content length: {result.Length}");
                                return result;
                            }
                            
                            Debug.WriteLine("[ERROR] No valid choices in completion response");
                            return "Нет ответа от GigaChat.";
                        }
                        catch (JsonException jsonEx)
                        {
                            Debug.WriteLine($"[ERROR] JSON parsing error: {jsonEx.Message}");
                            return $"Ошибка разбора ответа: {jsonEx.Message}";
                        }
                    }
                    else
                    {
                        // Если статус 401 (Unauthorized), пробуем обновить токен и повторить запрос
                        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                        {
                            Debug.WriteLine("[DEBUG] Unauthorized error, trying to refresh token");
                            if (await AuthAsync())
                            {
                                // Рекурсивно вызываем метод снова после обновления токена
                                return await SendQuestionAsync(question);
                            }
                        }
                        
                        Debug.WriteLine($"[ERROR] Request failed with status code: {response.StatusCode}");
                        return $"Ошибка запроса: {response.StatusCode} - {responseContent}";
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[EXCEPTION] Send question exception: {ex.Message}");
                Debug.WriteLine($"[EXCEPTION] Stack trace: {ex.StackTrace}");
                return $"Исключение: {ex.Message}";
            }
        }
        
        // Вспомогательный метод для отключения проверки SSL сертификатов (для отладки)
        private HttpClientHandler GetInsecureHandler()
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => 
                {
                    Debug.WriteLine($"[DEBUG] SSL Certificate validation bypassed, errors: {string.Join(", ", errors)}");
                    return true;
                }
            };
            return handler;
        }
    }
}