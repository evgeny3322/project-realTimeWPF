using AIInterviewAssistant.WPF.Models;
using System;
using System.Windows;
using System.Windows.Media;

namespace AIInterviewAssistant.WPF.UI
{
    public partial class OverlayWindow : Window
    {
        private AiResponse _currentResponse;
        private bool _isExplanationVisible;
        private Action<AiResponse> _showExplanationCallback;
        
        public OverlayWindow()
        {
            InitializeComponent();
            
            // Set window to be click-through except for its controls
            this.SourceInitialized += (s, e) =>
            {
                WindowsApiHelper.MakeWindowClickThrough(this);
            };
            
            // Position the window near cursor when shown
            this.Loaded += (s, e) =>
            {
                PositionWindowNearCursor();
            };
        }
        
        public void ShowSolution(AiResponse response, Action<AiResponse> showExplanationCallback = null)
        {
            _currentResponse = response;
            _isExplanationVisible = false;
            _showExplanationCallback = showExplanationCallback;
            
            // Update UI elements
            ProviderLabel.Text = $"{response.Provider} Solution";
            ContentTextBox.Text = response.Solution;
            ExplanationButton.Visibility = string.IsNullOrEmpty(response.Explanation) 
                ? Visibility.Collapsed 
                : Visibility.Visible;
            
            // Show window if not already visible
            if (!this.IsVisible)
            {
                this.Show();
                PositionWindowNearCursor();
            }
        }
        
        public void ShowExplanation(AiResponse response)
        {
            _currentResponse = response;
            _isExplanationVisible = true;
            
            // Update UI elements
            ProviderLabel.Text = $"{response.Provider} Explanation";
            ContentTextBox.Text = response.Explanation;
            ExplanationButton.Content = "Show Solution";
            
            // Show window if not already visible
            if (!this.IsVisible)
            {
                this.Show();
                PositionWindowNearCursor();
            }
        }
        
        public void UpdateAppearance(AppSettings settings)
        {
            // Update colors
            if (TryParseColor(settings.OverlayBackgroundColor, out var bgColor))
                MainBorder.Background = new SolidColorBrush(bgColor);
                
            if (TryParseColor(settings.OverlayTextColor, out var textColor))
            {
                var textBrush = new SolidColorBrush(textColor);
                ProviderLabel.Foreground = textBrush;
                ContentTextBox.Foreground = textBrush;
                CopyButton.Foreground = textBrush;
                CopyButton.BorderBrush = textBrush;
                ExplanationButton.Foreground = textBrush;
                ExplanationButton.BorderBrush = textBrush;
            }
            
            // Update font size
            ContentTextBox.FontSize = settings.OverlayTextSize;
        }
        
        private void PositionWindowNearCursor()
        {
            // Get cursor position
            var cursorPosition = System.Windows.Forms.Cursor.Position;
            
            // Position window to the right of cursor
            this.Left = cursorPosition.X + 20;
            this.Top = cursorPosition.Y;
            
            // Make sure the window is visible on screen
            EnsureWindowIsOnScreen();
        }
        
        private void EnsureWindowIsOnScreen()
        {
            // Get screen dimensions
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            
            // Update window position if needed to keep it on screen
            if (this.Left + this.ActualWidth > screenWidth)
                this.Left = screenWidth - this.ActualWidth;
                
            if (this.Top + this.ActualHeight > screenHeight)
                this.Top = screenHeight - this.ActualHeight;
                
            if (this.Left < 0)
                this.Left = 0;
                
            if (this.Top < 0)
                this.Top = 0;
        }
        
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
        }
        
        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Clipboard.SetText(ContentTextBox.Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy text: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        private void ExplanationButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isExplanationVisible)
            {
                // Switch back to solution
                _isExplanationVisible = false;
                ProviderLabel.Text = $"{_currentResponse.Provider} Solution";
                ContentTextBox.Text = _currentResponse.Solution;
                ExplanationButton.Content = "Show Explanation";
            }
            else
            {
                // Show explanation or request it if not available
                if (!string.IsNullOrEmpty(_currentResponse.Explanation))
                {
                    _isExplanationVisible = true;
                    ProviderLabel.Text = $"{_currentResponse.Provider} Explanation";
                    ContentTextBox.Text = _currentResponse.Explanation;
                    ExplanationButton.Content = "Show Solution";
                }
                else if (_showExplanationCallback != null)
                {
                    _showExplanationCallback(_currentResponse);
                }
            }
        }
        
        private bool TryParseColor(string colorHex, out Color color)
        {
            try
            {
                color = (Color)ColorConverter.ConvertFromString(colorHex);
                return true;
            }
            catch
            {
                color = Colors.Transparent;
                return false;
            }
        }
    }
    
    // Helper class for Windows API interaction
    public static class WindowsApiHelper
    {
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
        
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);
        
        public static void MakeWindowClickThrough(Window window)
        {
            // Get window handle
            IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
            
            // Make window click-through (transparent to mouse events)
            int style = GetWindowLong(hwnd, -20); // GWL_EXSTYLE
            SetWindowLong(hwnd, -20, style | 0x80000 | 0x20); // WS_EX_LAYERED | WS_EX_TRANSPARENT
        }
    }
}