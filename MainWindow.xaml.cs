using System;
using System.Windows;
using AIInterviewAssistant.WPF.Models;
using AIInterviewAssistant.WPF.Services;
using AIInterviewAssistant.WPF.Services.Interfaces;
using NAudio.Wave;
using SharpHook;
using SharpHook.Native;
using System.Text.Json;
using System.IO;
using System.Threading.Tasks;

namespace AIInterviewAssistant.WPF
{
    public partial class MainWindow : Window
    {
        private readonly IRecognizeService _recognizeService;
        private readonly IAIService _aiService;
        private IWaveIn _micCapture;
        private WasapiLoopbackCapture _desktopCapture;
        private WaveFileWriter? _desktopAudioWriter;
        private WaveFileWriter? _micAudioWriter;
        private bool _inProgress;
        private bool _modelLoaded;
        private TaskPoolGlobalHook _hook;
        
        public MainWindow()
        {
            InitializeComponent();
            
            // Инициализация сервисов
            _recognizeService = new RecognizeService();
            _aiService = new GigaChatService();
            
            // Настройка глобальных хоткеев
            _hook = new TaskPoolGlobalHook();
            _hook.KeyPressed += OnKeyPressed;
            _hook.KeyReleased += OnKeyReleased;
            _hook.RunAsync();
            
            // Инициализация состояния UI
            LoadButton.IsEnabled = true;
            SendManuallyButton.IsEnabled = false;
            StatusLabel.Content = "Ready";
        }

        private async void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(PositionTextBox.Text) || string.IsNullOrWhiteSpace(ModelPathTextBox.Text))
            {
                MessageBox.Show("Please fill in both Position and ModelPath fields.", "Input required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            try
            {
                // В UI потоке отключаем кнопку и обновляем статус
                LoadButton.IsEnabled = false;
                StatusLabel.Content = "Проверка авторизации в GigaChat...";
                
                // Сначала проверяем авторизацию GigaChat
                var authSuccess = await _aiService.AuthAsync();
                
                // В UI потоке обновляем статус
                if (!authSuccess)
                {
                    Dispatcher.Invoke(() => {
                        StatusLabel.Content = "Ошибка авторизации в GigaChat. Проверьте настройки.";
                        LoadButton.IsEnabled = true;
                    });
                    return;
                }
                
                // В UI потоке обновляем статус перед загрузкой модели
                Dispatcher.Invoke(() => {
                    StatusLabel.Content = "Авторизация в GigaChat успешна. Загрузка модели...";
                });
                
                // Загружаем модель Vosk в отдельном потоке, но не обновляем UI в этом потоке
                string modelPath = ModelPathTextBox.Text;
                await Task.Run(() => {
                    try {
                        _recognizeService.LoadModel(modelPath);
                    }
                    catch (Exception loadEx) {
                        throw new Exception($"Ошибка загрузки модели: {loadEx.Message}", loadEx);
                    }
                });
                
                // После загрузки модели обновляем UI в UI потоке
                Dispatcher.Invoke(() => {
                    StatusLabel.Content = "Модель загружена. Отправка начального промпта...";
                });
                
                // Отправляем начальный промпт
                string prompt = string.Format(
                    Application.Current.Properties["InitialPromptTemplate"] as string ?? 
                    "Ты профессиональный {0}. Ты проходишь собеседование.", 
                    PositionTextBox.Text);
                
                string response = await _aiService.SendQuestionAsync(prompt);
                
                // Обрабатываем ответ и обновляем UI в UI потоке
                Dispatcher.Invoke(() => {
                    if (string.IsNullOrWhiteSpace(response) || response.StartsWith("Ошибка"))
                    {
                        StatusLabel.Content = $"Не удалось отправить начальный промпт: {response}";
                        LoadButton.IsEnabled = true;
                        return;
                    }
                    
                    // Устанавливаем состояние готовности
                    _modelLoaded = true;
                    SendManuallyButton.IsEnabled = true;
                    StatusLabel.Content = "Готово к использованию";
                    OutputTextBox.Text = "Начальный ответ ИИ:\n" + response;
                    
                    // Подготавливаем системы записи
                    PrepareDesktopRecording();
                    PrepareMicRecording();
                });
            }
            catch (Exception ex)
            {
                // В случае исключения обновляем UI в UI потоке
                Dispatcher.Invoke(() => {
                    StatusLabel.Content = $"Ошибка: {ex.Message}";
                    LoadButton.IsEnabled = true;
                    _modelLoaded = false;
                });
            }
        }

        private async void SendManuallyButton_Click(object sender, RoutedEventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => SendManuallyButton_Click(sender, e));
                return;
            }
            
            if (_modelLoaded && !string.IsNullOrWhiteSpace(InputTextBox.Text))
            {
                try
                {
                    // Отключаем кнопку во время обработки запроса
                    SendManuallyButton.IsEnabled = false;
                    StatusLabel.Content = "Отправка запроса...";
                    
                    // Отправляем запрос к GigaChat
                    string response = await _aiService.SendQuestionAsync(InputTextBox.Text);
                    
                    // Проверяем ответ и обновляем UI (мы уже в UI потоке)
                    if (string.IsNullOrWhiteSpace(response) || response.StartsWith("Ошибка"))
                    {
                        StatusLabel.Content = $"Ошибка получения ответа: {response}";
                    }
                    else
                    {
                        OutputTextBox.Text = response;
                        StatusLabel.Content = "Ответ получен";
                    }
                }
                catch (Exception ex)
                {
                    StatusLabel.Content = $"Исключение: {ex.Message}";
                }
                finally
                {
                    // Всегда включаем кнопку обратно
                    SendManuallyButton.IsEnabled = true;
                }
            }
        }
        
        private async Task RecognizeSpeechAsync(string audioFilePath)
        {
            try
            {
                // Обновляем UI в UI потоке
                await Dispatcher.InvokeAsync(() => {
                    StatusLabel.Content = "Распознавание речи...";
                });
                
                // Выполняем распознавание речи в фоновом потоке
                string result = await Task.Run(() => _recognizeService.RecognizeSpeechAsync(audioFilePath));
                
                // Обрабатываем результат в UI потоке
                await Dispatcher.InvokeAsync(async () => {
                    try
                    {
                        // Десериализуем результат распознавания
                        var recognizeSpeech = JsonSerializer.Deserialize<RecognizeSpeechDto>(result);
                        
                        if (recognizeSpeech != null && !string.IsNullOrWhiteSpace(recognizeSpeech.Text))
                        {
                            InputTextBox.Text = recognizeSpeech.Text;
                            
                            // Если есть текст, отправляем его в AI
                            if (_modelLoaded && SendManuallyButton.IsEnabled)
                            {
                                SendManuallyButton_Click(this, new RoutedEventArgs());
                            }
                        }
                        else
                        {
                            StatusLabel.Content = "Распознавание не дало результатов";
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        StatusLabel.Content = $"Ошибка десериализации: {jsonEx.Message}";
                    }
                });
            }
            catch (Exception ex)
            {
                // Обрабатываем исключения в UI потоке
                await Dispatcher.InvokeAsync(() => {
                    StatusLabel.Content = $"Ошибка распознавания: {ex.Message}";
                });
            }
            finally
            {
                // Пытаемся удалить временный файл
                try
                {
                    if (File.Exists(audioFilePath))
                    {
                        File.Delete(audioFilePath);
                    }
                }
                catch
                {
                    // Игнорируем ошибки при удалении временных файлов
                }
            }
        }
        
        public void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
        {
            if (!_modelLoaded)
                return;
        
            // Все операции с UI элементами должны выполняться в UI потоке
            Dispatcher.Invoke(() => {
                switch (e.Data.KeyCode)
                {
                    case KeyCode.VcLeft:
                        // Record audio desktop
                        if (_inProgress)
                            break;
                
                        _inProgress = true;
                        PrepareDesktopRecording();
                        var outputDesktopFilePath = GetTempFileName();
                        _desktopAudioWriter = new WaveFileWriter(outputDesktopFilePath, _desktopCapture.WaveFormat);
                        _desktopCapture.DataAvailable += (sender, e) =>
                        {
                            if (_desktopAudioWriter == null)
                                return;
                                
                            _desktopAudioWriter.Write(e.Buffer, 0, e.BytesRecorded);
                            if (_desktopAudioWriter.Position > _desktopCapture.WaveFormat.AverageBytesPerSecond * 
                                (int)Application.Current.Properties["MaximumRecordLengthInSeconds"])
                            {
                                _desktopCapture.StopRecording();
                            }
                        };

                        _desktopCapture.RecordingStopped += async (sender, e) =>
                        {
                            string filePath = outputDesktopFilePath;
                            
                            try
                            {
                                if (_desktopAudioWriter != null)
                                {
                                    await _desktopAudioWriter.DisposeAsync();
                                    _desktopAudioWriter = null;
                                }
                                
                                if (_desktopCapture != null)
                                {
                                    _desktopCapture.Dispose();
                                }
                                
                                // Обновляем статус в UI потоке
                                await Dispatcher.InvokeAsync(() => {
                                    StatusLabel.Content = "Обработка аудио с рабочего стола...";
                                });
                                
                                // Выполняем распознавание
                                await RecognizeSpeechAsync(filePath);
                            }
                            catch (Exception ex)
                            {
                                await Dispatcher.InvokeAsync(() => {
                                    StatusLabel.Content = $"Ошибка обработки записи: {ex.Message}";
                                });
                            }
                        };

                        _desktopCapture.StartRecording();
                        StatusLabel.Content = "Запись аудио с рабочего стола...";
                        break;
                    
                    case KeyCode.VcRight:
                        // Record audio mic
                        if (_inProgress)
                            break;
                
                        _inProgress = true;
                        PrepareMicRecording();
                        var outputMicFilePath = GetTempFileName();
                        _micAudioWriter = new WaveFileWriter(outputMicFilePath, _micCapture.WaveFormat);
                        _micCapture.DataAvailable += (sender, e) =>
                        {
                            if (_micAudioWriter == null)
                                return;
                                
                            _micAudioWriter.Write(e.Buffer, 0, e.BytesRecorded);
                            if (_micAudioWriter.Position > _micCapture.WaveFormat.AverageBytesPerSecond * 
                                (int)Application.Current.Properties["MaximumRecordLengthInSeconds"])
                            {
                                _micCapture.StopRecording();
                            }
                        };

                        _micCapture.RecordingStopped += async (sender, e) =>
                        {
                            string filePath = outputMicFilePath;
                            
                            try
                            {
                                if (_micAudioWriter != null)
                                {
                                    await _micAudioWriter.DisposeAsync();
                                    _micAudioWriter = null;
                                }
                                
                                if (_micCapture != null)
                                {
                                    _micCapture.Dispose();
                                }
                                
                                // Обновляем статус в UI потоке
                                await Dispatcher.InvokeAsync(() => {
                                    StatusLabel.Content = "Обработка аудио с микрофона...";
                                });
                                
                                // Выполняем распознавание
                                await RecognizeSpeechAsync(filePath);
                            }
                            catch (Exception ex)
                            {
                                await Dispatcher.InvokeAsync(() => {
                                    StatusLabel.Content = $"Ошибка обработки записи: {ex.Message}";
                                });
                            }
                        };

                        _micCapture.StartRecording();
                        StatusLabel.Content = "Запись аудио с микрофона...";
                        break;
                }
            });
        }

        public void OnKeyReleased(object? sender, KeyboardHookEventArgs e)
        {
            if (!_modelLoaded)
                return;
            
            // Все операции с UI элементами должны выполняться в UI потоке
            Dispatcher.Invoke(() => {
                switch (e.Data.KeyCode)
                {
                    case KeyCode.VcLeft:
                        // Stop audio desktop
                        if (_inProgress)
                        {
                            _inProgress = false;
                            _desktopCapture?.StopRecording();
                        }
                        break;
                        
                    case KeyCode.VcRight:
                        // Stop audio mic
                        if (_inProgress)
                        {
                            _inProgress = false;
                            _micCapture?.StopRecording();
                        }
                        break;
                }
            });
        }
        
        private string GetTempFileName()
        {
            var outputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "NAudio");
            Directory.CreateDirectory(outputFolder);
            return Path.Combine(outputFolder, $"{Guid.NewGuid()}.wav");
        }

        private void PrepareMicRecording()
        {
            DisposeMicCapture();
            
            var waveIn = new WaveInEvent();
            waveIn.DeviceNumber = -1; // default system device
            waveIn.WaveFormat = new WaveFormat(16000, 1);
            _micCapture = waveIn;
        }

        private void PrepareDesktopRecording()
        {
            DisposeDesktopCapture();
            
            var capture = new WasapiLoopbackCapture();
            capture.WaveFormat = new WaveFormat(16000, 1);
            _desktopCapture = capture;
        }
        
        private void DisposeMicCapture()
        {
            if (_micCapture != null)
            {
                _micCapture.Dispose();
                _micCapture = null;
            }
        }
        
        private void DisposeDesktopCapture()
        {
            if (_desktopCapture != null)
            {
                _desktopCapture.Dispose();
                _desktopCapture = null;
            }
        }
        
        protected override void OnClosed(EventArgs e)
        {
            // Остановка и освобождение глобального хука
            if (_hook != null)
            {
                try
                {
                    _hook.KeyPressed -= OnKeyPressed;
                    _hook.KeyReleased -= OnKeyReleased;
                    _hook.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error disposing hook: {ex.Message}");
                }
            }
            
            // Освобождаем ресурсы при закрытии приложения
            if (_recognizeService is IDisposable disposable)
            {
                disposable.Dispose();
            }
            
            DisposeMicCapture();
            DisposeDesktopCapture();
            
            if (_micAudioWriter != null)
            {
                _micAudioWriter.Dispose();
                _micAudioWriter = null;
            }
            
            if (_desktopAudioWriter != null)
            {
                _desktopAudioWriter.Dispose();
                _desktopAudioWriter = null;
            }
            
            base.OnClosed(e);
        }
    }
}