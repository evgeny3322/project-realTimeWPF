using System.Windows;
using AIInterviewAssistant.WPF.Models;
using AIInterviewAssistant.WPF.Services;
using AIInterviewAssistant.WPF.Services.Interfaces;
using NAudio.Wave;
using SharpHook;
using SharpHook.Native;
using System.Text.Json;
using System.IO;

namespace AIInterviewAssistant.WPF
{
    public partial class MainWindow : Window
    {
        private readonly IRecognizeService _recognizeService;
        private readonly IAIService _aiService;
        private IWaveIn? _micCapture;
        private WasapiLoopbackCapture? _desktopCapture;
        private WaveFileWriter? _desktopAudioWriter;
        private WaveFileWriter? _micAudioWriter;
        private bool _inProgress;
        private bool _modelLoaded;
        
        public MainWindow()
        {
            InitializeComponent();
            
            // Инициализация сервисов
            _recognizeService = new RecognizeService();
            _aiService = new GigaChatService();
            
            // Настройка глобальных хоткеев
            var hook = new TaskPoolGlobalHook();
            hook.KeyPressed += OnKeyPressed;
            hook.KeyReleased += OnKeyReleased;
            hook.RunAsync();
            
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
            
            LoadButton.IsEnabled = false;
            StatusLabel.Content = "Model is loading, please wait...";
            
            try
            {
                await Task.Run(() => _recognizeService.LoadModel(ModelPathTextBox.Text));
                
                Dispatcher.Invoke(() => {
                    StatusLabel.Content = "Model loaded";
                });
                
                var authSuccess = await _aiService.AuthAsync();
                if (!authSuccess)
                {
                    Dispatcher.Invoke(() => {
                        StatusLabel.Content = "AI helper auth fail";
                        LoadButton.IsEnabled = true;
                    });
                    return;
                }
                
                string template = Application.Current.Properties["InitialPromptTemplate"] as string ?? 
                    "Ты профессиональный {0}. Ты проходишь собеседование. Сейчас я буду задавать вопросы, а тебе нужно на них давать ответ.";
                
                await _aiService.SendQuestionAsync(string.Format(template, PositionTextBox.Text));
                
                _modelLoaded = true;
                
                Dispatcher.Invoke(() => {
                    SendManuallyButton.IsEnabled = true;
                    PrepareDesktopRecording();
                    PrepareMicRecording();
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => {
                    StatusLabel.Content = $"Error: {ex.Message}";
                    LoadButton.IsEnabled = true;
                });
                _modelLoaded = false;
            }
        }

        private async void SendManuallyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_modelLoaded && !string.IsNullOrWhiteSpace(InputTextBox.Text))
            {
                SendManuallyButton.IsEnabled = false;
                try
                {
                    var response = await _aiService.SendQuestionAsync(InputTextBox.Text);
                    Dispatcher.Invoke(() => {
                        OutputTextBox.Text = response;
                        SendManuallyButton.IsEnabled = true;
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => {
                        StatusLabel.Content = $"Error sending message: {ex.Message}";
                        SendManuallyButton.IsEnabled = true;
                    });
                }
            }
        }
        
        private async Task RecognizeSpeechAsync(string audioFilePath)
        {
            Dispatcher.Invoke(() => {
                StatusLabel.Content = "Processing audio...";
            });
            
            try
            {
                var result = await _recognizeService.RecognizeSpeechAsync(audioFilePath);
                var recognizeSpeech = JsonSerializer.Deserialize<RecognizeSpeechDto>(result);
                
                Dispatcher.Invoke(() => {
                    InputTextBox.Text = recognizeSpeech?.Text ?? string.Empty;
                    StatusLabel.Content = "Audio processed, generating response...";
                    
                    if (!string.IsNullOrWhiteSpace(InputTextBox.Text))
                    {
                        SendManuallyButton_Click(this, new RoutedEventArgs());
                    }
                    else
                    {
                        StatusLabel.Content = "No text recognized";
                    }
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => {
                    StatusLabel.Content = $"Recognition error: {ex.Message}";
                });
            }
        }
        
        public void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
        {
            if (!_modelLoaded)
                return;
        
            switch (e.Data.KeyCode)
            {
                case KeyCode.VcLeft:
                    // Record audio desktop
                    if (_inProgress)
                        break;
                
                    _inProgress = true;
                    
                    Dispatcher.Invoke(() => {
                        PrepareDesktopRecording();
                        StatusLabel.Content = "Recording desktop audio...";
                        
                        var outputDesktopFilePath = GetTempFileName();
                        _desktopAudioWriter = new WaveFileWriter(outputDesktopFilePath, _desktopCapture!.WaveFormat);
                        
                        _desktopCapture!.DataAvailable += (s, args) =>
                        {
                            if (_desktopAudioWriter != null)
                            {
                                _desktopAudioWriter.Write(args.Buffer, 0, args.BytesRecorded);
                                int maxLength = Application.Current.Properties["MaximumRecordLengthInSeconds"] is int length ? length : 20;
                                if (_desktopAudioWriter.Position > _desktopCapture.WaveFormat.AverageBytesPerSecond * maxLength)
                                {
                                    _desktopCapture.StopRecording();
                                }
                            }
                        };

                        _desktopCapture!.RecordingStopped += async (s, args) =>
                        {
                            var captureDevice = _desktopCapture;
                            var writer = _desktopAudioWriter;
                            var filePath = outputDesktopFilePath;
                            
                            _desktopCapture = null;
                            _desktopAudioWriter = null;
                            
                            if (captureDevice != null)
                                captureDevice.Dispose();
                                
                            if (writer != null)
                                await writer.DisposeAsync();
                                
                            await RecognizeSpeechAsync(filePath);
                        };

                        _desktopCapture!.StartRecording();
                    });
                    break;
                
                case KeyCode.VcRight:
                    // Record audio mic
                    if (_inProgress)
                        break;
                
                    _inProgress = true;
                    
                    Dispatcher.Invoke(() => {
                        PrepareMicRecording();
                        StatusLabel.Content = "Recording microphone...";
                        
                        var outputMicFilePath = GetTempFileName();
                        _micAudioWriter = new WaveFileWriter(outputMicFilePath, _micCapture!.WaveFormat);
                        
                        _micCapture!.DataAvailable += (s, args) =>
                        {
                            if (_micAudioWriter != null)
                            {
                                _micAudioWriter.Write(args.Buffer, 0, args.BytesRecorded);
                                int maxLength = Application.Current.Properties["MaximumRecordLengthInSeconds"] is int length ? length : 20;
                                if (_micAudioWriter.Position > _micCapture.WaveFormat.AverageBytesPerSecond * maxLength)
                                {
                                    _micCapture.StopRecording();
                                }
                            }
                        };

                        _micCapture!.RecordingStopped += async (s, args) =>
                        {
                            var captureDevice = _micCapture;
                            var writer = _micAudioWriter;
                            var filePath = outputMicFilePath;
                            
                            _micCapture = null;
                            _micAudioWriter = null;
                            
                            if (captureDevice != null)
                                captureDevice.Dispose();
                                
                            if (writer != null)
                                await writer.DisposeAsync();
                                
                            await RecognizeSpeechAsync(filePath);
                        };

                        _micCapture!.StartRecording();
                    });
                    break;
            }
        }

        public void OnKeyReleased(object? sender, KeyboardHookEventArgs e)
        {
            if (!_modelLoaded)
                return;
                
            switch (e.Data.KeyCode)
            {
                case KeyCode.VcLeft:
                    // Stop audio desktop
                    if (_inProgress)
                    {
                        _inProgress = false;
                        Dispatcher.Invoke(() => {
                            StatusLabel.Content = "Stopping desktop recording...";
                            _desktopCapture?.StopRecording();
                        });
                    }
                    break;
                    
                case KeyCode.VcRight:
                    // Stop audio mic
                    if (_inProgress)
                    {
                        _inProgress = false;
                        Dispatcher.Invoke(() => {
                            StatusLabel.Content = "Stopping microphone recording...";
                            _micCapture?.StopRecording();
                        });
                    }
                    break;
            }
        }
        
        private string GetTempFileName()
        {
            var outputFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "NAudio");
            Directory.CreateDirectory(outputFolder);
            return Path.Combine(outputFolder, $"{Guid.NewGuid()}.wav");
        }

        private void PrepareMicRecording()
        {
            var waveIn = new WaveInEvent();
            waveIn.DeviceNumber = -1; // default system device
            waveIn.WaveFormat = new WaveFormat(16000, 1);
            _micCapture = waveIn;
        }

        private void PrepareDesktopRecording()
        {
            var capture = new WasapiLoopbackCapture();
            capture.WaveFormat = new WaveFormat(16000, 1);
            _desktopCapture = capture;
        }
    }
}