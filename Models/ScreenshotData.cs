using System;
using System.Drawing;
using System.Windows.Media.Imaging;

namespace AIInterviewAssistant.WPF.Models
{
    public class ScreenshotData
    {
        // Изображение скриншота
        public BitmapSource Image { get; set; }
        
        // Текст, обнаруженный на изображении
        public string DetectedText { get; set; } = string.Empty;
        
        // Время создания скриншота
        public DateTime Timestamp { get; set; } = DateTime.Now;
        
        // Позиция курсора в момент захвата
        public Point CursorPosition { get; set; }
        
        // Заголовок активного окна в момент захвата
        public string ActiveWindowTitle { get; set; } = string.Empty;
    }
}