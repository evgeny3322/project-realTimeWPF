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
using Windows.Media.Ocr;
using Windows.Graphics.Imaging;
using System.Diagnostics;

namespace AIInterviewAssistant.WPF.Services
{
    public class ScreenCaptureService : IScreenCaptureService
    {
        // Windows API imports для определения позиции курсора и активного окна
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
                // Получаем размеры экрана
                int screenWidth = (int)SystemParameters.PrimaryScreenWidth;
                int screenHeight = (int)SystemParameters.PrimaryScreenHeight;
                
                // Создаем скриншот экрана
                using (Bitmap bitmap = new Bitmap(screenWidth, screenHeight))
                {
                    using (Graphics g = Graphics.FromImage(bitmap))
                    {
                        g.CopyFromScreen(0, 0, 0, 0, new System.Drawing.Size(screenWidth, screenHeight));
                    }
                    
                    // Конвертируем в BitmapSource для WPF
                    return ConvertBitmapToBitmapSource(bitmap);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Ошибка захвата экрана: {ex.Message}");
                return null;
            }
        }
        
        public async Task<ScreenshotData> CaptureAndProcessScreenAsync()
        {
            try
            {
                // Создаем скриншот
                var screenBitmap = CaptureScreen();
                if (screenBitmap == null)
                    return null;
                
                // Получаем позицию курсора
                GetCursorPos(out POINT cursorPos);
                var cursorPosition = new Point(cursorPos.X, cursorPos.Y);
                
                // Получаем заголовок активного окна
                string activeWindowTitle = GetActiveWindowTitle();
                
                // Создаем объект с данными скриншота
                var screenshotData = new ScreenshotData
                {
                    Image = screenBitmap,
                    CursorPosition = cursorPosition,
                    ActiveWindowTitle = activeWindowTitle,
                    Timestamp = DateTime.Now
                };
                
                // Извлекаем текст в фоновом режиме
                screenshotData.DetectedText = await ExtractTextFromImageAsync(screenBitmap);
                
                return screenshotData;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Ошибка обработки скриншота: {ex.Message}");
                return null;
            }
        }
        
        public async Task<string> ExtractTextFromImageAsync(BitmapSource image)
        {
            try
            {
                // Конвертируем BitmapSource в SoftwareBitmap для OCR
                var softwareBitmap = await ConvertBitmapSourceToSoftwareBitmapAsync(image);
                
                // Инициализируем OCR двигатель
                var ocrEngine = OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en-US"));
                if (ocrEngine == null)
                {
                    Debug.WriteLine("[ERROR] Не удалось создать OCR двигатель");
                    return string.Empty;
                }
                
                // Распознаем текст
                var ocrResult = await ocrEngine.RecognizeAsync(softwareBitmap);
                
                // Объединяем результат в одну строку
                StringBuilder resultText = new StringBuilder();
                foreach (var line in ocrResult.Lines)
                {
                    resultText.AppendLine(line.Text);
                }
                
                return resultText.ToString();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Ошибка распознавания текста: {ex.Message}");
                return string.Empty;
            }
        }
        
        public string SaveScreenshot(ScreenshotData screenshot, string filePath = null)
        {
            if (screenshot?.Image == null)
                return null;
                
            try
            {
                // Генерируем имя файла, если не указано
                if (string.IsNullOrEmpty(filePath))
                {
                    string folder = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                        "AIInterviewAssistant");
                        
                    // Создаем директорию, если не существует
                    if (!Directory.Exists(folder))
                        Directory.CreateDirectory(folder);
                        
                    filePath = Path.Combine(folder, $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png");
                }
                
                // Сохраняем изображение
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
                Debug.WriteLine($"[ERROR] Ошибка сохранения скриншота: {ex.Message}");
                return null;
            }
        }
        
        #region Вспомогательные методы
        
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
        
        private async Task<SoftwareBitmap> ConvertBitmapSourceToSoftwareBitmapAsync(BitmapSource bitmapSource)
        {
            // Сохраняем BitmapSource во временный файл
            var tempFilePath = Path.Combine(Path.GetTempPath(), $"ocr_temp_{Guid.NewGuid()}.png");
            
            using (var fileStream = new FileStream(tempFilePath, FileMode.Create))
            {
                BitmapEncoder encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
                encoder.Save(fileStream);
            }
            
            try
            {
                // Открываем файл как RandomAccessStream
                using (var stream = await Windows.Storage.StorageFile.GetFileFromPathAsync(tempFilePath))
                using (var randomAccessStream = await stream.OpenAsync(Windows.Storage.FileAccessMode.Read))
                {
                    // Декодируем в SoftwareBitmap
                    var decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
                    return await decoder.GetSoftwareBitmapAsync();
                }
            }
            finally
            {
                // Удаляем временный файл
                try
                {
                    if (File.Exists(tempFilePath))
                        File.Delete(tempFilePath);
                }
                catch { /* Игнорируем ошибки при удалении временных файлов */ }
            }
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