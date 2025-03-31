using System;
using System.Diagnostics;
using System.Text;
using System.Windows;
using AIInterviewAssistant.WPF.Helpers;
using AIInterviewAssistant.WPF.Models;
using AIInterviewAssistant.WPF.Services;
using AIInterviewAssistant.WPF.Services.Interfaces;

namespace AIInterviewAssistant.WPF
{
    public partial class MainWindow : Window
    {
        private IAIService _aiService;
        private IOverlayService _overlayService;
        private IRecognizeService _recognizeService;
        private IScreenCaptureService _screenCaptureService;
        private AudioRecordingService _audioRecordingService;
        private HotkeyManager _hotkeyManager;

        private AiResponse _lastSolution;
        private bool _isRecording;

        public MainWindow()
        {
            InitializeComponent();

            _aiService = new GigaChatService();
            _overlayService = new OverlayService();
            _recognizeService = new RecognizeService();
            _screenCaptureService = new ScreenCaptureService();
            _audioRecordingService = new AudioRecordingService();

            // Load Vosk model
            string modelPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "vosk-model");
            _recognizeService.LoadModel(modelPath);

            // Setup hotkeys
            SetupHotkeys();

            // Update UI state
            UpdateUIState();
        }

        private void SetupHotkeys()
        {
            _hotkeyManager = new HotkeyManager();

            // Get hotkey settings from application properties
            var settings = AppSettings.LoadFromApplicationProperties();

            _hotkeyManager.RegisterHotkey(settings.TakeScreenshotHotkey, CaptureButton_Click);
            _hotkeyManager.RegisterHotkey(settings.ShowSolutionHotkey, ShowLastSolution); // Change this line

            _hotkeyManager.KeyPressed += (s, e) => Debug.WriteLine($"Key Pressed: {e.Data.KeyCode}");

            // Start hotkey manager
            _hotkeyManager.Start();
        }

        // Add this method
        private void ShowLastSolution()
        {
            _overlayService.ShowSolution(_lastSolution);
        }

        async void SolveVoiceButton_Click(object sender, RoutedEventArgs e)
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

                // Try to detect problem type
                string problemType = DetectProblemType(TranscribedTextBox.Text);
                VoiceProblemTypeBox.Text = $"Detected Problem Type: {problemType}";
                VoiceProblemTypeBox.Visibility = Visibility.Visible;

                // Solve the problem
                _lastSolution = await _aiService.SolveProgrammingProblemAsync(
                    TranscribedTextBox.Text, VoiceWithExplanationCheckBox.IsChecked ?? false);

                // Update UI
                StatusBarText.Text = "Solution ready";

                // Show the result
                VoiceSolutionTextBox.Text = _lastSolution.Solution;
                VoiceSolutionTextBox.Visibility = Visibility.Visible;

                // Show the solution
                _overlayService.ShowSolution(_lastSolution);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Problem solving error: {ex.Message}");
                StatusBarText.Text = $"Error: {ex.Message}";

                // Clear previous results
                VoiceSolutionTextBox.Text = "Failed to get solution";
                VoiceSolutionTextBox.Visibility = Visibility.Visible;
            }
            finally
            {
                // Re-enable button
                SolveVoiceButton.IsEnabled = true;
            }
        }

        string DetectProblemType(string text)
        {
            text = text.ToLower();

            // Simple rules to detect problem type  
            if (text.Contains("algorithm") || text.Contains("sort") || text.Contains("search"))
                return "Algorithmic Problem";

            if (text.Contains("class") || text.Contains("object") || text.Contains("inheritance"))
                return "Object-Oriented Programming";

            if (text.Contains("web") || text.Contains("http") || text.Contains("api"))
                return "Web Development";

            if (text.Contains("data") && (text.Contains("structure") || text.Contains("list")))
                return "Data Structures";

            if (text.Contains("recursive") || text.Contains("recursion"))
                return "Recursive Problem";

            if (text.Contains("dynamic") && text.Contains("programming"))
                return "Dynamic Programming";

            return "General Programming";
        }

        private void UpdateUIState()
        {
            // Update GigaChat auth state
            if (string.IsNullOrEmpty((string)Application.Current.Properties["GigaChatClientId"]) ||
                string.IsNullOrEmpty((string)Application.Current.Properties["GigaChatClientSecret"]))
            {
                StatusTextBlock.Text = "GigaChat API keys not set";
            }
            else
            {
                StatusTextBlock.Text = "Ready";
            }

            // Update hotkey list
            HotkeysListView.ItemsSource = new[]
            {
                new { Action = "Take Screenshot", Shortcut = Application.Current.Properties["CaptureScreenHotkey"] },
                new { Action = "Show Solution", Shortcut = Application.Current.Properties["ShowSolutionHotkey"] },
                new { Action = "Show Explanation", Shortcut = Application.Current.Properties["ShowExplanationHotkey"] },
                new { Action = "Alternative Solution", Shortcut = Application.Current.Properties["AlternativeSolutionHotkey"] }
            };
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Update UI on window load
            UpdateUIState();
        }

        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Open settings window
            MessageBox.Show("Settings not implemented yet", "TODO", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void AuthButton_Click(object sender, RoutedEventArgs e)
        {
            // Authenticate GigaChat
            if (await _aiService.AuthAsync())
            {
                StatusTextBlock.Text = "GigaChat ready";
            }
            else
            {
                StatusTextBlock.Text = "GigaChat auth failed";
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
                // Disable button during solving
                SolveButton.IsEnabled = false;
                StatusBarText.Text = "Solving problem...";

                // Solve the problem
                _lastSolution = await _aiService.SolveProgrammingProblemAsync(
                    ProblemTextBox.Text, WithExplanationCheckBox.IsChecked ?? false);

                // Show the solution
                _overlayService.ShowSolution(_lastSolution);

                StatusBarText.Text = "Solution ready";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Problem solving error: {ex.Message}");
                StatusBarText.Text = $"Error: {ex.Message}";
                MessageBox.Show($"Failed to solve problem: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Re-enable button
                SolveButton.IsEnabled = true;
            }
        }

        private async void CaptureButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var screenshot = await _screenCaptureService.CaptureAndProcessScreenAsync();

                // Display screenshot
                ScreenshotImage.Source = screenshot.Image;

                // Display extracted text  
                ExtractedTextBox.Text = screenshot.DetectedText;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Screen capture failed: {ex.Message}");
                StatusBarText.Text = $"Error: {ex.Message}";
                MessageBox.Show($"Screen capture failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void SolveCapturedButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ExtractedTextBox.Text) ||
                ExtractedTextBox.Text == "Extracted text will appear here...")
            {
                MessageBox.Show("Please capture a screenshot with a problem first", "Input required",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                // Disable button during solving 
                SolveCapturedButton.IsEnabled = false;
                StatusBarText.Text = "Solving captured problem...";

                // Solve the problem
                _lastSolution = await _aiService.SolveProgrammingProblemAsync(ExtractedTextBox.Text, false);

                // Show the solution
                _overlayService.ShowSolution(_lastSolution);

                StatusBarText.Text = "Solution ready";
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Problem solving error: {ex.Message}");
                StatusBarText.Text = $"Error: {ex.Message}";
                MessageBox.Show($"Failed to solve problem: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                // Re-enable button
                SolveCapturedButton.IsEnabled = true;
            }
        }

        private void HotkeyItem_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // TODO: Allow hotkey rebinding
            MessageBox.Show("Hotkey rebinding not implemented yet", "TODO", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void RecordButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isRecording)
            {
                try
                {
                    // Start recording
                    await _audioRecordingService.StartRecordingAsync();

                    _isRecording = true;
                    RecordButton.Content = "Stop Recording";
                    TranscribedTextBox.Text = "Recording...";
                    StatusBarText.Text = "Recording audio...";
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ERROR] Failed to start recording: {ex.Message}");
                    StatusBarText.Text = $"Error: {ex.Message}";
                    MessageBox.Show($"Failed to start recording: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                try
                {
                    // Stop recording
                    _audioRecordingService.StopRecording();

                    _isRecording = false;
                    RecordButton.Content = "Start Recording";
                    TranscribedTextBox.Text = "Transcribing...";
                    StatusBarText.Text = "Transcribing audio...";

                    // Transcribe audio
                    string audioFilePath = System.IO.Path.Combine(
                        System.IO.Path.GetTempPath(), "AIInterviewAssistant", "last_recording.wav");

                    var transcription = await _recognizeService.RecognizeSpeechAsync(audioFilePath);

                    Debug.WriteLine($"Transcription result: {transcription}");

                    // Extract text from JSON result
                    var dto = System.Text.Json.JsonSerializer.Deserialize<RecognizeSpeechDto>(transcription);

                    TranscribedTextBox.Text = dto?.Text ?? "Failed to recognize speech";
                    StatusBarText.Text = "Transcription ready";
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ERROR] Failed to transcribe audio: {ex.Message}");
                    StatusBarText.Text = $"Error: {ex.Message}";
                    TranscribedTextBox.Text = "Failed to transcribe audio";
                    MessageBox.Show($"Failed to transcribe audio: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}