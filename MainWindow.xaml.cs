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
        private IWaveIn _micCapture;
        private WasapiLoopbackCapture _desktopCapture;
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
                StatusLabel.Content = "Model loaded";
                
                var authSuccess = await _aiService.AuthAsync();
                if (!authSuccess)
                {
                    StatusLabel.Content = "AI helper auth fail";
                    LoadButton.IsEnabled = true;
                    return;
                }
                
                await _aiService.SendQuestionAsync(
                    string.Format(Application.Current.Properties["InitialPromptTemplate"] as string, PositionTextBox.Text));
                
                _modelLoaded = true;
                SendManuallyButton.IsEnabled = true;
                PrepareDesktopRecording();
                PrepareMicRecording();
            }
            catch (Exception ex)
            {
                StatusLabel.Content = $"Error: {ex.Message}";
                LoadButton.IsEnabled = true;
                _modelLoaded = false;
            }
        }

        private async void SendManuallyButton_Click(object sender, RoutedEventArgs e)
        {
            if (_modelLoaded && !string.IsNullOrWhiteSpace(InputTextBox.Text))
            {
                OutputTextBox.Text = await _aiService.SendQuestionAsync(InputTextBox.Text);
            }
        }
        
        private async Task RecognizeSpeechAsync(string audioFilePath)
        {
            var result = await _recognizeService.RecognizeSpeechAsync(audioFilePath);
            try
            {
                var recognizeSpeech = JsonSerializer.Deserialize<RecognizeSpeechDto>(result);
                InputTextBox.Dispatcher.Invoke(() => {
                    InputTextBox.Text = recognizeSpeech?.Text ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(InputTextBox.Text))
                    {
                        SendManuallyButton_Click(this, new RoutedEventArgs());
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
                    PrepareDesktopRecording();
                    var outputDesktopFilePath = GetTempFileName();
                    _desktopAudioWriter = new WaveFileWriter(outputDesktopFilePath, _desktopCapture.WaveFormat);
                    _desktopCapture.DataAvailable += (sender, e) =>
                    {
                        _desktopAudioWriter.Write(e.Buffer, 0, e.BytesRecorded);
                        if (_desktopAudioWriter.Position > _desktopCapture.WaveFormat.AverageBytesPerSecond * 
                            (int)Application.Current.Properties["MaximumRecordLengthInSeconds"])
                        {
                            _desktopCapture.StopRecording();
                        }
                    };

                    _desktopCapture.RecordingStopped += async (sender, e) =>
                    {
                        _desktopCapture.Dispose();
                        await _desktopAudioWriter.DisposeAsync();
                        _desktopAudioWriter = null;
                        await RecognizeSpeechAsync(outputDesktopFilePath);
                    };

                    _desktopCapture.StartRecording();
                    Dispatcher.Invoke(() => StatusLabel.Content = "Recording desktop audio...");
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
                        _micAudioWriter.Write(e.Buffer, 0, e.BytesRecorded);
                        if (_micAudioWriter.Position > _micCapture.WaveFormat.AverageBytesPerSecond * 
                            (int)Application.Current.Properties["MaximumRecordLengthInSeconds"])
                        {
                            _micCapture.StopRecording();
                        }
                    };

                    _micCapture.RecordingStopped += async (sender, e) =>
                    {
                        _micCapture.Dispose();
                        await _micAudioWriter.DisposeAsync();
                        _micAudioWriter = null;
                        await RecognizeSpeechAsync(outputMicFilePath);
                    };

                    _micCapture.StartRecording();
                    Dispatcher.Invoke(() => StatusLabel.Content = "Recording microphone...");
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
                        _desktopCapture?.StopRecording();
                        Dispatcher.Invoke(() => StatusLabel.Content = "Processing desktop audio...");
                    }
                    break;
                    
                case KeyCode.VcRight:
                    // Stop audio mic
                    if (_inProgress)
                    {
                        _inProgress = false;
                        _micCapture?.StopRecording();
                        Dispatcher.Invoke(() => StatusLabel.Content = "Processing microphone audio...");
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