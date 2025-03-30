using AIInterviewAssistant.WPF.Services.Interfaces;
using Vosk;
using System;
using System.IO;
using System.Threading.Tasks;

namespace AIInterviewAssistant.WPF.Services
{
    public class RecognizeService : IRecognizeService, IDisposable
    {
        private Model? _model;
        private VoskRecognizer? _recognizer;
        private readonly object _lockObject = new object();
        private bool _disposed = false;

        public void LoadModel(string modelPath)
        {
            if (string.IsNullOrWhiteSpace(modelPath))
                throw new ArgumentException("Model path cannot be empty", nameof(modelPath));
                
            if (!Directory.Exists(modelPath))
                throw new DirectoryNotFoundException($"Model directory not found: {modelPath}");
                
            try
            {
                // Освобождаем старую модель, если она была загружена
                lock (_lockObject)
                {
                    if (_recognizer != null)
                    {
                        _recognizer.Dispose();
                        _recognizer = null;
                    }
                    
                    if (_model != null)
                    {
                        _model.Dispose();
                        _model = null;
                    }
                }
                
                // Загружаем новую модель
                var newModel = new Model(modelPath);
                
                // Создаем новый распознаватель с загруженной моделью в защищенном блоке
                lock (_lockObject)
                {
                    _model = newModel;
                    _recognizer = new VoskRecognizer(_model, 16000.0f);
                    _recognizer.SetMaxAlternatives(0);
                    _recognizer.SetWords(true);
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Failed to load Vosk model: {ex.Message}", ex);
            }
        }
    
        public Task<string> RecognizeSpeechAsync(string filePath)
        {
            // Возвращаем задачу, которая выполняет распознавание асинхронно
            return Task.Run(() => {
                if (_disposed)
                    throw new ObjectDisposedException(nameof(RecognizeService));
                    
                if (_recognizer == null || _model == null)
                    throw new InvalidOperationException("Model not loaded");
                    
                if (!File.Exists(filePath))
                    throw new FileNotFoundException($"Audio file not found: {filePath}");
                
                // Создаем локальный распознаватель для текущего потока
                VoskRecognizer localRecognizer;
                
                lock (_lockObject)
                {
                    if (_model == null) 
                        throw new InvalidOperationException("Model has been disposed");
                    
                    // Создаем новый экземпляр распознавателя для этого потока
                    localRecognizer = new VoskRecognizer(_model, 16000.0f);
                    localRecognizer.SetMaxAlternatives(0);
                    localRecognizer.SetWords(true);
                }
                
                try
                {
                    using (var stream = File.OpenRead(filePath))
                    {
                        byte[] buffer = new byte[4096];
                        int bytesRead;
                        
                        while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            if (localRecognizer.AcceptWaveform(buffer, bytesRead))
                            {
                                // Промежуточный результат, игнорируем
                            }
                        }
                    }
                    
                    // Получаем итоговый результат
                    string result = localRecognizer.FinalResult();
                    return result;
                }
                finally
                {
                    // Освобождаем локальный распознаватель
                    localRecognizer.Dispose();
                }
            });
        }
        
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
                    lock (_lockObject)
                    {
                        if (_recognizer != null)
                        {
                            _recognizer.Dispose();
                            _recognizer = null;
                        }
                        
                        if (_model != null)
                        {
                            _model.Dispose();
                            _model = null;
                        }
                    }
                }
                
                _disposed = true;
            }
        }
        
        ~RecognizeService()
        {
            Dispose(false);
        }
    }
}