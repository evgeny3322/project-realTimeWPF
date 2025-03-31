using AIInterviewAssistant.WPF.Models;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace AIInterviewAssistant.WPF.Services.Interfaces
{
    public interface IScreenCaptureService
    {
        /// <summary>
        /// Captures the current screen as a bitmap
        /// </summary>
        /// <returns>The captured screen as a BitmapSource</returns>
        BitmapSource CaptureScreen();
        
        /// <summary>
        /// Captures the screen and attempts to extract programming task text
        /// </summary>
        /// <returns>Screenshot data with extracted text</returns>
        Task<ScreenshotData> CaptureAndProcessScreenAsync();
        
        /// <summary>
        /// Extracts text from the provided image
        /// </summary>
        /// <param name="image">The image to extract text from</param>
        /// <returns>The extracted text</returns>
        Task<string> ExtractTextFromImageAsync(BitmapSource image);
        
        /// <summary>
        /// Saves the screenshot to disk
        /// </summary>
        /// <param name="screenshot">The screenshot to save</param>
        /// <param name="filePath">Optional file path, if null a temp file will be created</param>
        /// <returns>The path to the saved file</returns>
        string SaveScreenshot(ScreenshotData screenshot, string? filePath = null);
    }
}