using AIInterviewAssistant.WPF.Helpers;
using AIInterviewAssistant.WPF.Models;
using AIInterviewAssistant.WPF.Services.Interfaces;
using AIInterviewAssistant.WPF.UI;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;
using Point = System.Drawing.Point;

namespace AIInterviewAssistant.WPF.Services
{
    public class OverlayService : IOverlayService
    {
        private OverlayWindow _overlayWindow;
        private NotificationWindow _notificationWindow;
        private readonly ScreenRecordingDetector _recordingDetector;
        private bool _hideWhenScreenRecording;

        public bool IsOverlayVisible => _overlayWindow?.IsVisible ?? false;

        public OverlayService(bool hideWhenScreenRecording = true)
        {
            // Инициализация детектора записи экрана
            _recordingDetector = new ScreenRecordingDetector(1000); // Проверка каждую секунду
            _recordingDetector.RecordingStatusChanged += OnRecordingStatusChanged;
            _hideWhenScreenRecording = hideWhenScreenRecording;
            _recordingDetector.StartMonitoring();

            InitializeOverlayWindow();
            InitializeNotificationWindow();
        }

        private void InitializeOverlayWindow()
        {
            // Создаем окно в потоке UI
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    _overlayWindow = new OverlayWindow();
                    
                    // Подписываемся на событие закрытия окна
                    _overlayWindow.Closed += (s, e) =>
                    {
                        // Создаем новое окно, если текущее закрыто
                        _overlayWindow = new OverlayWindow();
                    };
                    
                    // Устанавливаем внешний вид
                    _overlayWindow.UpdateAppearance("#80000000", "#FF00FF00", 12);
                    
                    Debug.WriteLine("[INFO] Overlay window initialized");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ERROR] Failed to initialize overlay window: {ex.Message}");
                    MessageBox.Show($"Failed to initialize overlay: {ex.Message}",
                        "Overlay Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            });
        }
        
        private void InitializeNotificationWindow()
        {
            // Создаем окно уведомлений в потоке UI
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    _notificationWindow = new NotificationWindow();
                    
                    // Подписываемся на событие закрытия окна
                    _notificationWindow.Closed += (s, e) =>
                    {
                        // Создаем новое окно, если текущее закрыто
                        _notificationWindow = new NotificationWindow();
                    };
                    
                    Debug.WriteLine("[INFO] Notification window initialized");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ERROR] Failed to initialize notification window: {ex.Message}");
                }
            });
        }

        public void ShowSolution(AiResponse response, Point? position = null)
        {
            if (response == null || string.IsNullOrWhiteSpace(response.Solution))
            {
                Debug.WriteLine("[WARN] Attempted to show empty solution");
                return;
            }

            // Проверяем, не идет ли запись экрана
            if (_hideWhenScreenRecording && _recordingDetector.IsRecordingDetected)
            {
                Debug.WriteLine("[INFO] Screen recording detected, not showing solution");
                return;
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    // Создаем новое окно, если текущее было закрыто
                    if (_overlayWindow == null)
                    {
                        InitializeOverlayWindow();
                    }

                    // Показываем решение
                    _overlayWindow.ShowSolution(response);
                    
                    // Если указана позиция, позиционируем окно
                    if (position.HasValue)
                    {
                        _overlayWindow.PositionWindow(position.Value);
                    }
                    
                    Debug.WriteLine($"[INFO] Solution overlay shown for provider: {response.Provider}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ERROR] Failed to show solution overlay: {ex.Message}");
                }
            });
        }

        public void ShowExplanation(AiResponse response, Point? position = null)
        {
            if (response == null || string.IsNullOrWhiteSpace(response.Explanation))
            {
                Debug.WriteLine("[WARN] Attempted to show empty explanation");
                return;
            }

            // Проверяем, не идет ли запись экрана
            if (_hideWhenScreenRecording && _recordingDetector.IsRecordingDetected)
            {
                Debug.WriteLine("[INFO] Screen recording detected, not showing explanation");
                return;
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    // Создаем новое окно, если текущее было закрыто
                    if (_overlayWindow == null)
                    {
                        InitializeOverlayWindow();
                    }

                    // Показываем объяснение
                    _overlayWindow.ShowExplanation(response);
                    
                    // Если указана позиция, позиционируем окно
                    if (position.HasValue)
                    {
                        _overlayWindow.PositionWindow(position.Value);
                    }
                    
                    Debug.WriteLine($"[INFO] Explanation overlay shown for provider: {response.Provider}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ERROR] Failed to show explanation overlay: {ex.Message}");
                }
            });
        }

        public void HideOverlay()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    if (_overlayWindow != null && _overlayWindow.IsVisible)
                    {
                        _overlayWindow.Hide();
                        Debug.WriteLine("[INFO] Overlay hidden");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ERROR] Failed to hide overlay: {ex.Message}");
                }
            });
        }
        
        public void ShowNotification(string message)
        {
            // Проверяем, не идет ли запись экрана
            if (_hideWhenScreenRecording && _recordingDetector.IsRecordingDetected)
            {
                Debug.WriteLine("[INFO] Screen recording detected, not showing notification");
                return;
            }
            
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    if (_notificationWindow == null)
                    {
                        InitializeNotificationWindow();
                    }
                    
                    _notificationWindow.ShowNotification(message);
                    Debug.WriteLine($"[INFO] Notification shown: {message}");
                    
                    // Автоматически скрываем уведомление через 3 секунды
                    Task.Delay(3000).ContinueWith(_ =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (_notificationWindow?.IsVisible == true)
                            {
                                _notificationWindow.Hide();
                            }
                        });
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ERROR] Failed to show notification: {ex.Message}");
                }
            });
        }

        private void OnRecordingStatusChanged(object sender, bool isRecording)
        {
            if (_hideWhenScreenRecording && isRecording)
            {
                HideOverlay();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    if (_notificationWindow?.IsVisible == true)
                    {
                        _notificationWindow.Hide();
                    }
                });
                Debug.WriteLine("[INFO] Screen recording detected, overlays hidden");
            }
        }
    }
}