using NAudio.Wave;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AIInterviewAssistant.WPF.Services
{
    public class AudioRecordingService
    {
        private WaveInEvent _waveIn;
        private WaveFileWriter _waveWriter;
        private string _outputFilePath;
        private bool _isRecording;
        private readonly int _maxRecordLengthMs;
        private CancellationTokenSource _recordingCancellation;

        public event EventHandler<string> RecordingCompleted;
        public event EventHandler<TimeSpan> RecordingTimeUpdated;

        public bool IsRecording => _isRecording;

        public AudioRecordingService(int maxRecordLengthInSeconds = 20)
        {
            _maxRecordLengthMs = maxRecordLengthInSeconds * 1000;
        }

        public async Task StartRecordingAsync()
        {
            if (_isRecording)
                return;

            try
            {
                // Create temp file for recording
                string tempFolder = Path.Combine(Path.GetTempPath(), "AIInterviewAssistant");
                if (!Directory.Exists(tempFolder))
                    Directory.CreateDirectory(tempFolder);

                _outputFilePath = Path.Combine(tempFolder, $"recording_{DateTime.Now:yyyyMMdd_HHmmss}.wav");
                
                _waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(16000, 1)
                };

                _waveWriter = new WaveFileWriter(_outputFilePath, _waveIn.WaveFormat);
                
                _waveIn.DataAvailable += OnDataAvailable;
                _waveIn.RecordingStopped += OnRecordingStopped;
                
                _recordingCancellation = new CancellationTokenSource();
                
                _isRecording = true;
                _waveIn.StartRecording();
                
                Debug.WriteLine($"[INFO] Recording started to {_outputFilePath}");
                
                // Start a timer to update recording time
                _ = UpdateRecordingTimeAsync(_recordingCancellation.Token);
                
                // Auto-stop recording after max length
                _ = Task.Delay(_maxRecordLengthMs, _recordingCancellation.Token)
                    .ContinueWith(t => 
                    {
                        if (!t.IsCanceled && _isRecording)
                        {
                            StopRecording();
                        }
                    }, TaskScheduler.Default);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Failed to start recording: {ex.Message}");
                CleanupRecording();
                throw;
            }
        }

        private async Task UpdateRecordingTimeAsync(CancellationToken cancellationToken)
        {
            var startTime = DateTime.Now;
            
            while (!cancellationToken.IsCancellationRequested && _isRecording)
            {
                var elapsed = DateTime.Now - startTime;
                RecordingTimeUpdated?.Invoke(this, elapsed);
                
                try
                {
                    await Task.Delay(100, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        public void StopRecording()
        {
            if (!_isRecording)
                return;

            _waveIn?.StopRecording();
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            if (_waveWriter != null)
            {
                _waveWriter.Write(e.Buffer, 0, e.BytesRecorded);
                _waveWriter.Flush();
            }
        }

        private void OnRecordingStopped(object sender, StoppedEventArgs e)
        {
            CleanupRecording();
            
            if (e.Exception != null)
            {
                Debug.WriteLine($"[ERROR] Recording stopped with error: {e.Exception.Message}");
            }
            else
            {
                Debug.WriteLine("[INFO] Recording completed successfully");
                RecordingCompleted?.Invoke(this, _outputFilePath);
            }
        }

        private void CleanupRecording()
        {
            _isRecording = false;
            
            if (_recordingCancellation != null)
            {
                try
                {
                    _recordingCancellation.Cancel();
                    _recordingCancellation.Dispose();
                    _recordingCancellation = null;
                }
                catch { }
            }
            
            if (_waveIn != null)
            {
                _waveIn.DataAvailable -= OnDataAvailable;
                _waveIn.RecordingStopped -= OnRecordingStopped;
                _waveIn.Dispose();
                _waveIn = null;
            }
            
            if (_waveWriter != null)
            {
                _waveWriter.Dispose();
                _waveWriter = null;
            }
        }
    }
}