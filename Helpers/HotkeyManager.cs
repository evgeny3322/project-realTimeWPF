using AIInterviewAssistant.WPF.Models;
using SharpHook;
using SharpHook.Native;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;

namespace AIInterviewAssistant.WPF.Helpers
{
    public class HotkeyManager : IDisposable
    {
        private readonly TaskPoolGlobalHook _hook;
        private readonly Dictionary<KeyCode, Action> _hotkeyActions;
        private bool _disposed;

        public event EventHandler<KeyboardHookEventArgs> KeyPressed;
        public event EventHandler<KeyboardHookEventArgs> KeyReleased;

        public HotkeyManager()
        {
            _hook = new TaskPoolGlobalHook();
            _hotkeyActions = new Dictionary<KeyCode, Action>();

            _hook.KeyPressed += OnKeyPressed;
            _hook.KeyReleased += OnKeyReleased;
        }

        public void Start()
        {
            try
            {
                _hook.RunAsync();
                Debug.WriteLine("[INFO] Hotkey manager started successfully");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ERROR] Failed to start hotkey manager: {ex.Message}");
                MessageBox.Show($"Failed to initialize hotkey functionality: {ex.Message}",
                    "Hotkey Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        public void RegisterHotkey(KeyCode keyCode, Action action)
        {
            if (_hotkeyActions.ContainsKey(keyCode))
            {
                _hotkeyActions[keyCode] = action;
            }
            else
            {
                _hotkeyActions.Add(keyCode, action);
            }
        }

        public void UnregisterHotkey(KeyCode keyCode)
        {
            if (_hotkeyActions.ContainsKey(keyCode))
            {
                _hotkeyActions.Remove(keyCode);
            }
        }

        public void ClearHotkeys()
        {
            _hotkeyActions.Clear();
        }

        private void OnKeyPressed(object sender, KeyboardHookEventArgs e)
        {
            if (_hotkeyActions.TryGetValue(e.Data.KeyCode, out var action))
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        action?.Invoke();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ERROR] Error executing hotkey action: {ex.Message}");
                    }
                });
            }

            KeyPressed?.Invoke(this, e);
        }

        private void OnKeyReleased(object sender, KeyboardHookEventArgs e)
        {
            KeyReleased?.Invoke(this, e);
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
                    _hook.KeyPressed -= OnKeyPressed;
                    _hook.KeyReleased -= OnKeyReleased;
                    _hook.Dispose();
                }

                _disposed = true;
            }
        }
    }
}