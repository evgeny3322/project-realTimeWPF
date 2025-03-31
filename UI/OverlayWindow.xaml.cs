using AIInterviewAssistant.WPF.Helpers;
using AIInterviewAssistant.WPF.Models;
using System;
using System.Windows;
using System.Windows.Media;
using Point = System.Drawing.Point;

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
                WindowsApiHelper.MakeWindowTopmost(this);
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
            ExplanationButton.Content = "Show Explanation";
            
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
        
        public void UpdateAppearance(string backgroundColor, string textColor, int fontSize)
        {
            // Update colors
            if (TryParseColor(backgroundColor, out var bgColor))
                MainBorder.Background = new SolidColorBrush(bgColor);
                
            if (TryParseColor(textColor, out var txtColor))
            {
                var textBrush = new SolidColorBrush(txtColor);
                ProviderLabel.Foreground = textBrush;
                ContentTextBox.Foreground = textBrush;
                CopyButton.Foreground = textBrush;
                CopyButton.BorderBrush = textBrush;
                ExplanationButton.Foreground = textBrush;
                ExplanationButton.BorderBrush = textBrush;
            }
            
            // Update font size
            ContentTextBox.FontSize = fontSize;
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
        
        public void PositionWindow(Point position)
        {
            this.Left = position.X + 20;
            this.Top = position.Y;
            
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
}