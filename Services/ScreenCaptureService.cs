using AIInterviewAssistant.WPF.Models;
using AIInterviewAssistant.WPF.Services.Interfaces;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;

namespace AIInterviewAssistant.WPF.Services
{
    public class ScreenCaptureService : IScreenCaptureService
    {
        // Windows API imports for getting cursor position and active window
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);
        
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        
        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
        
        private struct POINT
        {
            public int X;
            public int Y;
        }
        
        public BitmapSource CaptureScreen()
        {
            try
            {
                // Get screen dimensions
                int screenWidth = (int)SystemParameters.PrimaryScreenWidth;
                int screenHeight = (int)SystemParameters.PrimaryScreenHeight;
                
                // Capture the screen
                using (Bitmap bitmap = new Bitmap(screenWidth, screenHeight))
                {
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        g.CopyFromScreen(0, 0, 0, 0, new System.Drawing.Size(screenWidth, screenHeight));
                    }
                    
                    // Convert to BitmapSource for WPF
                    return ConvertBitmapToBitmapSource(bitmap);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Screen capture failed: {ex.Message}");
                return null;
            }
        }
        
        public async Task<ScreenshotData> CaptureAndProcessScreenAsync()
        {
            try
            {
                // Capture screen
                var screenBitmap = CaptureScreen();
                if (screenBitmap == null)
                    return null;
                
                // Get cursor position
                GetCursorPos(out POINT cursorPos);
                var cursorPosition = new Point(cursorPos.X, cursorPos.Y);
                
                // Get active window title
                string activeWindowTitle = GetActiveWindowTitle();
                
                // Create screenshot data
                var screenshotData = new ScreenshotData
                {
                    Image = screenBitmap,
                    CursorPosition = cursorPosition,
                    ActiveWindowTitle = activeWindowTitle,
                    Timestamp = DateTime.Now
                };
                
                // Extract text in background
                screenshotData.DetectedText = await ExtractTextFromImageAsync(screenBitmap);
                
                return screenshotData;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Screenshot processing failed: {ex.Message}");
                return null;
            }
        }
        
        public async Task<string> ExtractTextFromImageAsync(BitmapSource image)
        {
            // For now, we'll use a simple placeholder implementation
            // In a real implementation, you would use OCR here (like Windows.Media.Ocr or Tesseract)
            
            // Simulate a delay for async processing
            await Task.Delay(500);
            
            // Placeholder return - in real implementation you'd extract text from the image
            return "// This would be the extracted programming problem text";
        }
        
        public string SaveScreenshot(ScreenshotData screenshot, string filePath = null)
        {
            if (screenshot?.Image == null)
                return null;
                
            try
            {
                // Generate a file path if not provided
                if (string.IsNullOrEmpty(filePath))
                {
                    string folder = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                        "AIInterviewAssistant");
                        
                    // Create directory if it doesn't exist
                    if (!Directory.Exists(folder))
                        Directory.CreateDirectory(folder);
                        
                    filePath = Path.Combine(folder, $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                }
                
                // Save the image
                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    BitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(screenshot.Image));
                    encoder.Save(fileStream);
                }
                
                return filePath;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Saving screenshot failed: {ex.Message}");
                return null;
            }
        }
        
        #region Helper Methods
        
        private BitmapSource ConvertBitmapToBitmapSource(Bitmap bitmap)
        {
            var bitmapData = bitmap.LockBits(
                new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly, bitmap.PixelFormat);
                
            var bitmapSource = BitmapSource.Create(
                bitmapData.Width, bitmapData.Height,
                bitmap.HorizontalResolution, bitmap.VerticalResolution,
                System.Windows.Media.PixelFormats.Bgr24, null,
                bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);
                
            bitmap.UnlockBits(bitmapData);
            return bitmapSource;
        }
        
        private string GetActiveWindowTitle()
        {
            const int nChars = 256;
            StringBuilder titleBuilder = new StringBuilder(nChars);
            
            IntPtr handle = GetForegroundWindow();
            
            if (GetWindowText(handle, titleBuilder, nChars) > 0)
                return titleBuilder.ToString();
                
            return string.Empty;
        }
        
        #endregion
    }
}