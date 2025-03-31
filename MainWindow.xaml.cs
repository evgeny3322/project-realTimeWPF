using AIInterviewAssistant.WPF.Helpers;
using AIInterviewAssistant.WPF.Models;
using AIInterviewAssistant.WPF.Services;
using AIInterviewAssistant.WPF.Services.Interfaces;
using AIInterviewAssistant.WPF.UI;
using SharpHook;
using SharpHook.Native;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace AIInterviewAssistant.WPF
{
    public partial class MainWindow : Window
    {
        private readonly IAIService _aiService;
        private readonly IOverlayService _overlayService;
        private readonly IScreenCaptureService _screenCaptureService;
        private readonly HotkeyManager _hotkeyManager;
        private readonly AudioRecordingService _audioRecordingService;
        private readonly IRecognizeService _recognizeService;

        private ScreenshotData _lastScreenshot;
        private AiResponse _lastSolution;
        private ObservableCollection<HotkeyItem> _hotkeys;
        private string _lastRecordingPath;

        public MainWindow()
        {
            InitializeComponent();

            // Инициализируем сервисы
            _aiService = new GigaChatService();
            _overlayService = new OverlayService(true); // Скрывать при записи экрана
            _screenCaptureService = new ScreenCaptureService();
            _hotkeyManager = new HotkeyManager();

            // Initialize voice recognition services
            _audioRecordingService = new AudioRecordingService(
                (int)Application.Current.Properties["MaximumRecordLengthInSeconds"]);
            _recognizeService = new RecognizeService();

            // Subscribe to recording events
            _audioRecordingService.RecordingCompleted += OnRecordingCompleted;
            _audioRecordingService.RecordingTimeUpdated += OnRecordingTimeUpdated;

            // Try to load the Vosk model
            try
            {
                string voskModelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "vosk-model");
                if (Directory.Exists(voskModelPath))
                {
                    _recognizeService.LoadModel(voskModelPath);
                    Debug.WriteLine("[INFO] Vosk model loaded successfully");
                }
                else
                {
                    Debug.WriteLine("[WARN] Vosk model directory not found: " + voskModelPath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Failed to load Vosk model: {ex.Message}");
            }

            // Инициализируем коллекцию горячих клавиш
            _hotkeys = new ObservableCollection<HotkeyItem>
            {
                new HotkeyItem
                {
                    Action = "Capture Screen",
                    Shortcut = "PrintScreen",
                    KeyCode = KeyCode.VcPrintScreen
                },
                new HotkeyItem
                {
                    Action = "Show Solution",
                    Shortcut = "F9",
                    KeyCode = KeyCode.VcF9
                },
                new HotkeyItem
                {
                    Action = "Show Explanation",
                    Shortcut = "F10",
                    KeyCode = KeyCode.VcF10
                }
            };

            HotkeysListView.ItemsSource = _hotkeys;

            // Регистрируем обработчики горячих клавиш
            RegisterHotkeys();

            // Запускаем менеджер горячих клавиш
            _hotkeyManager.Start();

            // Обновляем интерфейс
            UpdateUI();
        }

        private void RegisterHotkeys()
        {
            // Регистрируем захват экрана
            _hotkeyManager.RegisterHotkey(KeyCode.VcPrintScreen, async () =>
            {
                await CaptureScreenAsync();
            });

            // Регистрируем отображение решения
            _hotkeyManager.RegisterHotkey(KeyCode.VcF9, () =>
            {
                if (_lastSolution != null)
                {
                    _overlayService.ShowSolution(_lastSolution);
                }
                else
                {
                    _overlayService.ShowNotification("No solution available yet");
                }
            });

            // Регистрируем отображение объяснения
            _hotkeyManager.RegisterHotkey(KeyCode.VcF10, async () =>
            {
                if (_lastSolution != null)
                {
                    // Если нет объяснения, запрашиваем его
                    if (string.IsNullOrEmpty(_lastSolution.Explanation))
                    {
                        // Запрашиваем объяснение в фоновом режиме
                        Task.Run(async () =>
                        {
                            try
                            {
                                var explanation = await _aiService.SolveProgrammingProblemAsync(
                                    _lastSolution.OriginalPrompt, true);

                                // Обновляем последнее решение
                                _lastSolution.Explanation = explanation.Explanation;

                                // Показываем объяснение
                                Dispatcher.Invoke(() =>
                                {
                                    _overlayService.ShowExplanation(_lastSolution);
                                });
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[ERROR] Failed to get explanation: {ex.Message}");
                                Dispatcher.Invoke(() =>
                                {
                                    _overlayService.ShowNotification("Failed to get explanation");
                                });
                            }
                        });
                    }
                    else
                    {
                        _overlayService.ShowExplanation(_lastSolution);
                    }
                }
                else
                {
                    _overlayService.ShowNotification("No solution available yet");
                }
            });
        }

        private async Task CaptureScreenAsync()
        {
            try
            {
                UpdateStatus("Capturing screen...");

                // Показываем уведомление
                _overlayService.ShowNotification("Capturing screen...");

                // Захватываем скриншот
                _lastScreenshot = await _screenCaptureService.CaptureAndProcessScreenAsync();

                if (_lastScreenshot != null)
                {
                    // Обновляем интерфейс
                    Dispatcher.Invoke(() =>
                    {
                        ScreenshotImage.Source = _lastScreenshot.Image;
                        ExtractedTextBox.Text = _lastScreenshot.DetectedText;
                        StatusBarText.Text = "Screenshot captured";
                    });

                    // Автоматически решаем задачу
                    await SolveCapturedProblemAsync();
                }
                else
                {
                    UpdateStatus("Failed to capture screenshot");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Screen capture error: {ex.Message}");
                UpdateStatus($"Error: {ex.Message}");
            }
        }

        private async Task SolveCapturedProblemAsync()
        {
            if (_lastScreenshot == null || string.IsNullOrWhiteSpace(_lastScreenshot.DetectedText))
            {
                _overlayService.ShowNotification("No text to solve");
                return;
            }

            try
            {
                // Показываем уведомление
                _overlayService.ShowNotification("Solving problem...");
                UpdateStatus("Solving problem...");

                // Засекаем время начала
                var startTime = DateTime.Now;

                // Решаем задачу
                _lastSolution = await _aiService.SolveProgrammingProblemAsync(
                    _lastScreenshot.DetectedText, WithExplanationCheckBox.IsChecked ?? false);

                // Вычисляем время выполнения
                _lastSolution.ExecutionTimeMs = (long)(DateTime.Now - startTime).TotalMilliseconds;

                // Обновляем статус
                UpdateStatus($"Solution ready ({_lastSolution.ExecutionTimeMs / 1000.0:F1} sec)");

                // Показываем уведомление
                _overlayService.ShowNotification("Solution ready! Press F9 to view");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Problem solving error: {ex.Message}");
                UpdateStatus($"Error: {ex.Message}");
                _overlayService.ShowNotification("Failed to solve problem");
            }
        }

        private async void AuthButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Отключаем кнопку на время авторизации
                AuthButton.IsEnabled = false;
                StatusTextBlock.Text = "Authenticating...";

                // Авторизуемся
                bool success = await _aiService.AuthAsync();

                // Обновляем интерфейс
                if (success)
                {
                    StatusTextBlock.Text = "Ready";
                    StatusBarText.Text = "GigaChat authenticated successfully";
                }
                else
                {
                    StatusTextBlock.Text = "Authentication failed";
                    StatusBarText.Text = "GigaChat authentication failed";
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Authentication error: {ex.Message}");
                StatusTextBlock.Text = "Error";
                StatusBarText.Text = $"Error: {ex.Message}";
            }
            finally
            {
                // Включаем кнопку обратно
                AuthButton.IsEnabled = true;
            }
        }

        private async void SolveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ProblemTextBox.Text))
            {
                MessageBox.Show("Please enter a problem description", "Input required",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Отключаем кнопку на время решения
                SolveButton.IsEnabled = false;
                StatusBarText.Text = "Solving problem...";

                // Решаем задачу
                _lastSolution = await _aiService.SolveProgrammingProblemAsync(
                    ProblemTextBox.Text, WithExplanationCheckBox.IsChecked ?? false);

                // Обновляем интерфейс
                StatusBarText.Text = "Solution ready";

                // Показываем решение
                _overlayService.ShowSolution(_lastSolution);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Problem solving error: {ex.Message}");
                StatusBarText.Text = $"Error: {ex.Message}";
            }
            finally
            {
                // Включаем кнопку обратно
                SolveButton.IsEnabled = true;
            }
        }

        private async void CaptureButton_Click(object sender, RoutedEventArgs e)
        {
            await CaptureScreenAsync();
        }

        private async void SolveCapturedButton_Click(object sender, RoutedEventArgs e)
        {
            await SolveCapturedProblemAsync();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // Здесь будет открываться окно настроек
            MessageBox.Show("Settings functionality will be implemented later", "Coming Soon",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void HotkeyItem_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            var item = HotkeysListView.SelectedItem as HotkeyItem;
            if (item != null)
            {
                // Здесь будет открываться диалог изменения горячей клавиши
                MessageBox.Show($"Editing hotkey for '{item.Action}' will be implemented later",
                    "Coming Soon", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void UpdateStatus(string message)
        {
            Dispatcher.Invoke(() =>
            {
                StatusBarText.Text = message;
            });
        }

        private void UpdateUI()
        {
            // Изначально отключаем некоторые кнопки
            SolveCapturedButton.IsEnabled = false;
            SolveVoiceButton.IsEnabled = false;

            // Обновляем обработчики событий
            ScreenshotImage.SourceUpdated += (s, e) =>
            {
                SolveCapturedButton.IsEnabled = true;
            };
        }

        // Voice Recognition Methods
        private async void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            if (_audioRecordingService.IsRecording)
            {
                // Stop recording
                RecordButton.IsEnabled = false;
                RecordButton.Content = "Processing...";
                _audioRecordingService.StopRecording();
            }
            else
            {
                // Start recording
                try
                {
                    RecordButton.Content = "Stop Recording";
                    RecordingProgressBar.Value = 0;
                    RecordingProgressBar.Maximum = (int)Application.Current.Properties["MaximumRecordLengthInSeconds"];
                    RecordingTimeText.Text = "00:00";
                    TranscribedTextBox.Text = "Recording...";
                    SolveVoiceButton.IsEnabled = false;

                    await _audioRecordingService.StartRecordingAsync();
                }
                catch (Exception ex)
                {
                    RecordButton.Content = "Start Recording";
                    TranscribedTextBox.Text = $"Error starting recording: {ex.Message}";
                    StatusBarText.Text = $"Error: {ex.Message}";
                }
            }
        }

        private async void OnRecordingCompleted(object sender, string filePath)
        {
            _lastRecordingPath = filePath;

            // Update UI
            await Dispatcher.InvokeAsync(() =>
            {
                RecordButton.Content = "Start Recording";
                RecordButton.IsEnabled = true;
                TranscribedTextBox.Text = "Transcribing...";
                StatusBarText.Text = "Transcribing audio...";
            });

            try
            {
                // Check if Vosk model is loaded
                if (_recognizeService == null)
                {
                    TranscribedTextBox.Text = "Speech recognition is not available. Vosk model not loaded.";
                    return;
                }

                // Recognize speech
                var jsonResult = await _recognizeService.RecognizeSpeechAsync(filePath);

                // Parse JSON result to extract text
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                var result = JsonSerializer.Deserialize<RecognizeSpeechDto>(jsonResult, options);

                // Update UI with recognized text
                await Dispatcher.InvokeAsync(() =>
                {
                    if (!string.IsNullOrWhiteSpace(result?.Text))
                    {
                        TranscribedTextBox.Text = result.Text;
                        SolveVoiceButton.IsEnabled = true;
                        StatusBarText.Text = "Transcription complete";
                    }
                    else
                    {
                        TranscribedTextBox.Text = "No speech detected. Please try again.";
                        StatusBarText.Text = "No speech detected";
                    }
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    TranscribedTextBox.Text = $"Error transcribing audio: {ex.Message}";
                    StatusBarText.Text = $"Error: {ex.Message}";
                });
                Debug.WriteLine($"[ERROR] Speech recognition error: {ex.Message}");
            }
        }

        private void OnRecordingTimeUpdated(object sender, TimeSpan elapsed)
        {
            Dispatcher.Invoke(() =>
            {
                RecordingTimeText.Text = $"{elapsed.Minutes:00}:{elapsed.Seconds:00}";
                RecordingProgressBar.Value = elapsed.TotalSeconds;
            });
        }

        private async void SolveVoiceButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TranscribedTextBox.Text) ||
                TranscribedTextBox.Text == "Transcribed text will appear here..." ||
                TranscribedTextBox.Text == "Recording..." ||
                TranscribedTextBox.Text == "Transcribing...")
            {
                MessageBox.Show("Please record and transcribe your problem first", "Input required",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Disable button during solving
                SolveVoiceButton.IsEnabled = false;
                StatusBarText.Text = "Solving problem...";

                // Solve the problem
                _lastSolution = await _aiService.SolveProgrammingProblemAsync(
                    TranscribedTextBox.Text, VoiceWithExplanationCheckBox.IsChecked ?? false);

                // Update UI
                StatusBarText.Text = "Solution ready";

                // Show solution
                _overlayService.ShowSolution(_lastSolution);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Problem solving error: {ex.Message}");
                StatusBarText.Text = $"Error: {ex.Message}";
            }
            finally
            {
                // Re-enable button
                SolveVoiceButton.IsEnabled = true;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);

            // Освобождаем ресурсы
            _hotkeyManager?.Dispose();

            // Unsubscribe from recording events
            if (_audioRecordingService != null)
            {
                _audioRecordingService.RecordingCompleted -= OnRecordingCompleted;
                _audioRecordingService.RecordingTimeUpdated -= OnRecordingTimeUpdated;
            }

            // Dispose recognition service
            _recognizeService?.Dispose();
        }
    }

    public class HotkeyItem
    {
        public string Action { get; set; }
        public string Shortcut { get; set; }
        public KeyCode KeyCode { get; set; }
    }
}