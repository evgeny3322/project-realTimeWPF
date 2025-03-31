using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using AIInterviewAssistant.WPF.Models;
using AIInterviewAssistant.WPF.Services.Interfaces;
using Tesseract;
using Point = System.Drawing.Point;

namespace AIInterviewAssistant.WPF.Services
{
    public class ScreenCaptureService : IScreenCaptureService, IDisposable
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

        private TesseractEngine? _tesseractEngine;
        private readonly string _tessdataPath;
        private bool _isOcrInitialized;
        private bool _disposed;

        public ScreenCaptureService()
        {
            // Путь к папке с данными для Tesseract
            _tessdataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tessdata");

            InitializeTesseract();
        }

        private void InitializeTesseract()
        {
            try
            {
                // Проверяем наличие языковых файлов
                string[] requiredLanguageFiles = new[] {
                    Path.Combine(_tessdataPath, "eng.traineddata"),
                    Path.Combine(_tessdataPath, "rus.traineddata")
                };

                bool allFilesExist = true;
                foreach (var langFile in requiredLanguageFiles)
                {
                    if (!File.Exists(langFile))
                    {
                        Debug.WriteLine($"[ERROR] Language file not found: {langFile}");
                        allFilesExist = false;
                    }
                }

                // Если все файлы существуют, инициализируем OCR
                if (allFilesExist)
                {
                    _tesseractEngine = new TesseractEngine(
                        _tessdataPath,
                        "eng+rus",
                        EngineMode.Default
                    );
                    _isOcrInitialized = true;
                    Debug.WriteLine("[INFO] Tesseract OCR initialized successfully");
                }
                else
                {
                    _isOcrInitialized = false;
                    _tesseractEngine = null;
                    Debug.WriteLine("[WARN] Not all language files are present");
                }
            }
            catch (Exception ex)
            {
                _isOcrInitialized = false;
                _tesseractEngine = null;
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
                        g.CopyFromScreen(
                            0, 0, 0, 0, 
                            new System.Drawing.Size(screenWidth, screenHeight)
                        );
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
                    Timestamp = DateTime.Now,
                };

                // Извлекаем текст в фоновом режиме
                screenshotData.DetectedText = await ExtractTextFromImageAsync(screenBitmap);

                return screenshotData;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Ошибка обработки скриншота: {ex.Message}");
                return null!;
            }
        }

        public async Task<string> ExtractTextFromImageAsync(BitmapSource image)
        {
            return await Task.Run(() => ExtractTextFromImage(image));
        }

        private string ExtractTextFromImage(BitmapSource image)
        {
            if (image == null)
            {
                Debug.WriteLine("[ERROR] Null image passed to text extraction");
                return string.Empty;
            }

            if (!_isOcrInitialized || _tesseractEngine == null)
            {
                Debug.WriteLine("[WARN] OCR is not initialized or Tesseract engine is null");
                return string.Empty;
            }

            try
            {
                // Конвертируем BitmapSource в Bitmap для Tesseract
                Bitmap bitmap = BitmapSourceToBitmap(image);

                // Конвертируем Bitmap в Pix
                using (var pix = ConvertBitmapToPix(bitmap))
                {
                    // Обработка распознавания
                    using (var page = _tesseractEngine.Process(pix))
                    {
                        string text = page.GetText()?.Trim() ?? string.Empty;
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
            if (bitmap == null)
            {
                Debug.WriteLine("[ERROR] Null bitmap passed to conversion");
                return null!;
            }

            string tempFile = Path.Combine(
                Path.GetTempPath(), 
                $"ocr_temp_{Guid.NewGuid()}.png"
            );

            try
            {
                // Сохраняем bitmap с повышенным качеством
                bitmap.Save(tempFile, System.Drawing.Imaging.ImageFormat.Png);

                // Загружаем изображение как Pix
                var pix = Pix.LoadFromFile(tempFile);

                // Удаляем временный файл
                try 
                { 
                    File.Delete(tempFile); 
                }
                catch (Exception deleteEx)
                {
                    Debug.WriteLine($"[WARN] Failed to delete temp file: {deleteEx.Message}");
                }

                return pix;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Failed to convert bitmap to Pix: {ex.Message}");
                
                // Очищаем временный файл если он остался
                try 
                { 
                    if (File.Exists(tempFile)) 
                        File.Delete(tempFile); 
                }
                catch { }

                return null!;
            }
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
                        "AIInterviewAssistant"
                    );

                    // Создаем директорию, если не существует
                    if (!Directory.Exists(folder))
                        Directory.CreateDirectory(folder);

                    filePath = Path.Combine(
                        folder,
                        $"Screenshot_{DateTime.Now:yyyyMMdd_HHmmss}.png"
                    );
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
                ImageLockMode.ReadOnly,
                bitmap.PixelFormat
            );

            var bitmapSource = BitmapSource.Create(
                bitmapData.Width,
                bitmapData.Height,
                bitmap.HorizontalResolution,
                bitmap.VerticalResolution,
                System.Windows.Media.PixelFormats.Bgr24,
                null,
                bitmapData.Scan0,
                bitmapData.Stride * bitmapData.Height,
                bitmapData.Stride
            );

            bitmap.UnlockBits(bitmapData);
            return bitmapSource;
        }

        private Bitmap BitmapSourceToBitmap(BitmapSource source)
        {
            Bitmap bitmap = new Bitmap(
                source.PixelWidth,
                source.PixelHeight,
                System.Drawing.Imaging.PixelFormat.Format32bppPArgb
            );

            BitmapData data = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.WriteOnly,
                System.Drawing.Imaging.PixelFormat.Format32bppPArgb
            );

            source.CopyPixels(
                Int32Rect.Empty, 
                data.Scan0, 
                data.Height * data.Stride, 
                data.Stride
            );

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

        #region IDisposable Implementation
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Освобождаем управляемые ресурсы
                    if (_tesseractEngine != null)
                    {
                        _tesseractEngine.Dispose();
                        _tesseractEngine = null;
                    }
                }

                _disposed = true;
            }
        }

        ~ScreenCaptureService()
        {
            Dispose(false);
        }
        #endregion
    }
}