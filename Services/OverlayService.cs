using AIInterviewAssistant.WPF.Helpers;
using AIInterviewAssistant.WPF.Models;
using AIInterviewAssistant.WPF.Services.Interfaces;
using AIInterviewAssistant.WPF.UI;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows;
using System.Windows.Media;
using Point = System.Drawing.Point;

namespace AIInterviewAssistant.WPF.Services
{
    public class OverlayService : IOverlayService
    {
        private OverlayWindow _overlayWindow;
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
                        PositionOverlayWindow(position.Value);
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
                        PositionOverlayWindow(position.Value);
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

        public void UpdateSettings(AppSettings settings)
        {
            _hideWhenScreenRecording = settings.HideWhenScreenRecording;

            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    if (_overlayWindow != null)
                    {
                        _overlayWindow.UpdateAppearance(settings);
                        Debug.WriteLine("[INFO] Overlay appearance updated");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ERROR] Failed to update overlay settings: {ex.Message}");
                }
            });
        }

        private void PositionOverlayWindow(Point position)
        {
            if (_overlayWindow != null)
            {
                // Позиционируем окно справа от курсора, но при этом проверяем,
                // чтобы оно не выходило за границы экрана
                _overlayWindow.Left = position.X + 20;
                _overlayWindow.Top = position.Y;
            }
        }

        private void OnRecordingStatusChanged(object sender, bool isRecording)
        {
            if (_hideWhenScreenRecording && isRecording)
            {
                HideOverlay();
                Debug.WriteLine("[INFO] Screen recording detected, overlay hidden");
            }
        }
    }
}