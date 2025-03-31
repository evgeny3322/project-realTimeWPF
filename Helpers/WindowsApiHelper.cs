using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;

namespace AIInterviewAssistant.WPF.Helpers
{
    public static class WindowsApiHelper
    {
        // Константы для работы с окнами
        private const int GWL_EXSTYLE = -20;
        private const int WS_EX_LAYERED = 0x80000;
        private const int WS_EX_TRANSPARENT = 0x20;
        private const int WS_EX_TOOLWINDOW = 0x80;
        private const int WS_EX_TOPMOST = 0x8;

        // Импорт Windows API функций
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
        
        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hwnd, int index);
        
        [DllImport("user32.dll")]
        private static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);
        
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);
        
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        
        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
        
        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
        
        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hWnd, ref RECT rect);

        // Константы для SetLayeredWindowAttributes
        private const uint LWA_ALPHA = 0x2;
        private const uint LWA_COLORKEY = 0x1;

        // Константы для SetWindowPos
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_SHOWWINDOW = 0x0040;

        // Структуры для работы с API
        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;

            public POINT(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        /// <summary>
        /// Делает окно прозрачным для событий мыши (клики проходят сквозь окно на нижележащие окна)
        /// </summary>
        /// <param name="window">Окно WPF для изменения</param>
        public static void MakeWindowClickThrough(Window window)
        {
            try
            {
                IntPtr hwnd = new WindowInteropHelper(window).Handle;
                int style = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, style | WS_EX_LAYERED | WS_EX_TRANSPARENT);
                Debug.WriteLine("[INFO] Window made click-through");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Failed to make window click-through: {ex.Message}");
            }
        }

        /// <summary>
        /// Делает окно полупрозрачным
        /// </summary>
        /// <param name="window">Окно WPF для изменения</param>
        /// <param name="opacity">Уровень прозрачности (0-255)</param>
        public static void MakeWindowTransparent(Window window, byte opacity)
        {
            try
            {
                IntPtr hwnd = new WindowInteropHelper(window).Handle;
                int style = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, style | WS_EX_LAYERED);
                SetLayeredWindowAttributes(hwnd, 0, opacity, LWA_ALPHA);
                Debug.WriteLine($"[INFO] Window opacity set to {opacity}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Failed to set window opacity: {ex.Message}");
            }
        }

        /// <summary>
        /// Делает окно невидимым в панели задач
        /// </summary>
        /// <param name="window">Окно WPF для изменения</param>
        public static void MakeWindowToolWindow(Window window)
        {
            try
            {
                IntPtr hwnd = new WindowInteropHelper(window).Handle;
                int style = GetWindowLong(hwnd, GWL_EXSTYLE);
                SetWindowLong(hwnd, GWL_EXSTYLE, style | WS_EX_TOOLWINDOW);
                Debug.WriteLine("[INFO] Window made tool window");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Failed to make window tool window: {ex.Message}");
            }
        }

        /// <summary>
        /// Устанавливает окно поверх всех других окон
        /// </summary>
        /// <param name="window">Окно WPF для изменения</param>
        public static void MakeWindowTopmost(Window window)
        {
            try
            {
                IntPtr hwnd = new WindowInteropHelper(window).Handle;
                SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW);
                Debug.WriteLine("[INFO] Window made topmost");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Failed to make window topmost: {ex.Message}");
            }
        }

        /// <summary>
        /// Получает текущее положение курсора мыши
        /// </summary>
        /// <returns>Координаты курсора</returns>
        public static POINT GetCursorPosition()
        {
            POINT point;
            GetCursorPos(out point);
            return point;
        }

        /// <summary>
        /// Получает заголовок активного окна
        /// </summary>
        /// <returns>Строка с заголовком окна</returns>
        public static string GetActiveWindowTitle()
        {
            const int nChars = 256;
            StringBuilder titleBuilder = new StringBuilder(nChars);
            
            IntPtr handle = GetForegroundWindow();
            
            if (GetWindowText(handle, titleBuilder, nChars) > 0)
                return titleBuilder.ToString();
                
            return string.Empty;
        }

        /// <summary>
        /// Получает размеры и положение окна
        /// </summary>
        /// <param name="hWnd">Дескриптор окна</param>
        /// <returns>Прямоугольник с размерами окна</returns>
        public static RECT GetWindowRect(IntPtr hWnd)
        {
            RECT rect = new RECT();
            GetWindowRect(hWnd, ref rect);
            return rect;
        }
    }
}