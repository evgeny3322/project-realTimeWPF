using System;
using System.Drawing;
using System.Windows.Media.Imaging;

namespace AIInterviewAssistant.WPF.Models
{
    public class ScreenshotData
    {
        // The raw screenshot image
        public BitmapSource Image { get; set; }
        
        // The text detected in the image (if any)
        public string DetectedText { get; set; } = string.Empty;
        
        // The timestamp when the screenshot was taken
        public DateTime Timestamp { get; set; } = DateTime.Now;
        
        // The mouse position at the time of capture
        public Point CursorPosition { get; set; }
        
        // The active window title at time of capture
        public string ActiveWindowTitle { get; set; } = string.Empty;
    }
}