using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace AIInterviewAssistant.WPF.Helpers
{
    public class ScreenRecordingDetector
    {
        // Список известных процессов для записи экрана
        private static readonly List<string> _recordingProcessNames = new List<string>
        {
            "obs64", "obs", "obs-studio",                           // OBS Studio
            "streamlabs obs", "slobs",                              // Streamlabs OBS
            "xsplit.core", "xsplitbroadcaster", "xsplitgamecaster", // XSplit
            "fraps",                                                 // Fraps
            "bandicam",                                              // Bandicam
            "action",                                                // Action!
            "camtasia",                                              // Camtasia
            "dxtory",                                                // Dxtory
            "screencastomatic",                                      // Screencast-O-Matic
            "nvidia shadowplay", "nvcontainer", "nvsphelper64",     // NVIDIA ShadowPlay
            "radeon-si", "amdow",                                    // AMD ReLive
            "wmcap", "wmenc",                                        // Windows Media Encoder
            "msteams", "teams",                                      // Microsoft Teams
            "zoom",                                                  // Zoom
            "discord",                                               // Discord (screen sharing)
            "skype",                                                 // Skype
            "slack",                                                 // Slack
            "webexmta",                                              // Webex
            "googlemeet",                                            // Google Meet
            "teamviewer",                                            // TeamViewer
            "anydesk"                                                // AnyDesk
        };

        // Windows API для получения списка всех окон
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);
        
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
        
        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);
        
        [DllImport("dwmapi.dll")]
        private static extern int DwmGetWindowAttribute(IntPtr hwnd, int dwAttribute, out bool pvAttribute, int cbAttribute);

        // Константы для DwmGetWindowAttribute
        private const int DWMWA_CLOAKED = 14;

        // Делегат для перечисления окон
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private CancellationTokenSource _cts;
        private Task _monitoringTask;
        private bool _isRecordingDetected;
        private readonly int _checkIntervalMs;

        public event EventHandler<bool> RecordingStatusChanged;

        public bool IsRecordingDetected => _isRecordingDetected;

        public ScreenRecordingDetector(int checkIntervalMs = 1000)
        {
            _checkIntervalMs = checkIntervalMs;
        }

        public void StartMonitoring()
        {
            // Если мониторинг уже запущен, сначала остановим его
            StopMonitoring();

            _cts = new CancellationTokenSource();
            _monitoringTask = Task.Run(async () =>
            {
                try
                {
                    while (!_cts.Token.IsCancellationRequested)
                    {
                        bool currentStatus = DetectScreenRecording();
                        
                        // Если статус изменился, вызываем событие
                        if (currentStatus != _isRecordingDetected)
                        {
                            _isRecordingDetected = currentStatus;
                            OnRecordingStatusChanged(_isRecordingDetected);
                        }

                        await Task.Delay(_checkIntervalMs, _cts.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Нормальное завершение при отмене задачи
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ERROR] Screen recording monitoring error: {ex.Message}");
                }
            });
        }

        public void StopMonitoring()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            if (_monitoringTask != null)
            {
                // Дождемся завершения задачи, но не более 1 секунды
                try
                {
                    _monitoringTask.Wait(1000);
                }
                catch { /* Игнорируем исключения */ }
                
                _monitoringTask = null;
            }
        }

        private bool DetectScreenRecording()
        {
            try
            {
                // Проверка по запущенным процессам
                Process[] processes = Process.GetProcesses();
                
                foreach (Process process in processes)
                {
                    try
                    {
                        string processName = process.ProcessName.ToLower();
                        
                        if (_recordingProcessNames.Any(name => processName.Contains(name)))
                        {
                            return true;
                        }
                    }
                    catch
                    {
                        // Игнорируем ошибки доступа к отдельным процессам
                    }
                }

                // Проверка через перечисление окон (для Windows 10 - проверка на скрытые окна захвата)
                bool recordingWindowFound = false;
                EnumWindows((hWnd, lParam) =>
                {
                    if (IsWindowVisible(hWnd))
                    {
                        uint processId;
                        GetWindowThreadProcessId(hWnd, out processId);
                        
                        try
                        {
                            Process process = Process.GetProcessById((int)processId);
                            string processName = process.ProcessName.ToLower();
                            
                            if (_recordingProcessNames.Any(name => processName.Contains(name)))
                            {
                                // Проверка на скрытое окно (для Windows 10)
                                bool isCloaked = false;
                                int result = DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out isCloaked, sizeof(bool));
                                
                                if (result == 0 && !isCloaked)
                                {
                                    recordingWindowFound = true;
                                    return false; // Прерываем перечисление
                                }
                            }
                        }
                        catch
                        {
                            // Игнорируем ошибки доступа к отдельным процессам
                        }
                    }
                    
                    return true; // Продолжаем перечисление
                }, IntPtr.Zero);

                return recordingWindowFound;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Error detecting screen recording: {ex.Message}");
                return false;
            }
        }

        protected virtual void OnRecordingStatusChanged(bool isRecording)
        {
            RecordingStatusChanged?.Invoke(this, isRecording);
        }
    }
}