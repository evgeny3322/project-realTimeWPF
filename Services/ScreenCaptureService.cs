using AIInterviewAssistant.WPF.Models;
using AIInterviewAssistant.WPF.Services.Interfaces;
using System;
using System.Drawing;
using System.Drawing.Imaging; // Explicit import for System.Drawing.Imaging
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using Tesseract;
using Point = System.Drawing.Point; // Explicit import for System.Drawing.Point

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

        private readonly TesseractEngine? _tesseractEngine;
        private readonly string _tessdataPath;
        private bool _isOcrInitialized;
        
        public ScreenCaptureService()
        {
            // Путь к папке с данными для Tesseract (можно изменить на нужный)
            _tessdataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");
            
            try
            {
                // Если папка с данными Tesseract существует, инициализируем OCR
                if (Directory.Exists(_tessdataPath))
                {
                    _tesseractEngine = new TesseractEngine(_tessdataPath, "eng", EngineMode.Default);
                    _isOcrInitialized = true;
                    Debug.WriteLine("[INFO] Tesseract OCR initialized successfully");
                }
                else
                {
                    _isOcrInitialized = false;
                    Debug.WriteLine("[WARN] Tesseract tessdata path not found: " + _tessdataPath);
                }
            }
            catch (Exception ex)
            {
                _isOcrInitialized = false;
                Debug.WriteLine($"[ERROR] Failed to initialize Tesseract OCR: {ex.Message}");
            }
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
                return null!;
            }
        }
        
        public async Task<ScreenshotData> CaptureAndProcessScreenAsync()
        {
            try
            {
                // Создаем скриншот
                var screenBitmap = CaptureScreen();
                if (screenBitmap == null)
                    return null!;
                
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
                screenshotData.DetectedText = await Task.Run(() => ExtractTextFromImage(screenBitmap));
                
                return screenshotData;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Ошибка обработки скриншота: {ex.Message}");
                return null!;
            }
        }
        
        private string ExtractTextFromImage(BitmapSource image)
        {
            if (!_isOcrInitialized || _tesseractEngine == null)
            {
                Debug.WriteLine("[WARN] OCR is not initialized, skipping text extraction");
                return string.Empty;
            }
            
            try
            {
                // Конвертируем BitmapSource в Bitmap для Tesseract
                Bitmap bitmap = BitmapSourceToBitmap(image);
                
                // Конвертируем Bitmap в Pix, который требуется для Tesseract
                using (var pix = ConvertBitmapToPix(bitmap))
                {
                    if (pix == null)
                    {
                        Debug.WriteLine("[ERROR] Failed to convert bitmap to Pix format");
                        return string.Empty;
                    }
                    
                    using (var page = _tesseractEngine.Process(pix))
                    {
                        // Получаем распознанный текст
                        string text = page.GetText();
                        Debug.WriteLine($"[INFO] OCR extracted {text.Length} characters");
                        return text;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] OCR text extraction failed: {ex.Message}");
                return string.Empty;
            }
        }

        private Pix ConvertBitmapToPix(Bitmap bitmap)
        {
            try
            {
                // Сохраняем Bitmap во временный файл
                string tempFile = Path.Combine(Path.GetTempPath(), $"ocr_temp_{Guid.NewGuid()}.png");
                // Use fully qualified name to resolve ambiguity
                bitmap.Save(tempFile, System.Drawing.Imaging.ImageFormat.Png);
                
                // Загружаем изображение как Pix
                var pix = Pix.LoadFromFile(tempFile);
                
                // Удаляем временный файл
                try 
                { 
                    File.Delete(tempFile); 
                }
                catch 
                { 
                    // Игнорируем ошибки при удалении временного файла
                }
                
                return pix;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Failed to convert bitmap to Pix: {ex.Message}");
                return null!;
            }
        }

        public async Task<string> ExtractTextFromImageAsync(BitmapSource image)
        {
            return await Task.Run(() => ExtractTextFromImage(image));
        }
        
        public string SaveScreenshot(ScreenshotData screenshot, string? filePath = null)
        {
            if (screenshot?.Image == null)
                return null!;
                
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
                return null!;
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
        
        private Bitmap BitmapSourceToBitmap(BitmapSource source)
        {
            Bitmap bitmap = new Bitmap(
                source.PixelWidth,
                source.PixelHeight,
                System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                
            BitmapData data = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.WriteOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppPArgb);
                
            source.CopyPixels(
                Int32Rect.Empty,
                data.Scan0,
                data.Height * data.Stride,
                data.Stride);
                
            bitmap.UnlockBits(data);
            return bitmap;
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