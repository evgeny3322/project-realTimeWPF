using SharpHook.Native;
using System;

namespace AIInterviewAssistant.WPF.Models
{
    public class AppSettings
    {
        // GigaChat API Settings
        public string GigaChatClientId { get; set; } = string.Empty;
        public string GigaChatClientSecret { get; set; } = string.Empty;
        public string GigaChatScope { get; set; } = "GIGACHAT_API_PERS";
        
        // Hotkey Settings
        public KeyCode TakeScreenshotHotkey { get; set; } = KeyCode.VcPrintScreen;
        public KeyCode ShowSolutionHotkey { get; set; } = KeyCode.VcF9;
        public KeyCode ShowExplanationHotkey { get; set; } = KeyCode.VcF10;
        public KeyCode AlternativeSolutionHotkey { get; set; } = KeyCode.VcF11;
        
        // UI Settings
        public int OverlayTextSize { get; set; } = 12;
        public string OverlayTextColor { get; set; } = "#FF00FF00"; // Green
        public string OverlayBackgroundColor { get; set; } = "#80000000"; // Semi-transparent black
        
        // Prompt Templates
        public string SolutionPromptTemplate { get; set; } = "Реши задачу программирования:\n{0}\nДай только код решения без объяснений.";
        public string ExplanationPromptTemplate { get; set; } = "Реши задачу программирования:\n{0}\nДай код решения и детальное объяснение подхода.";
        
        // Screen Recording Protection
        public bool HideWhenScreenRecording { get; set; } = true;
        
        // Default values constructor
        public AppSettings()
        {
            // Default values are set in property initializers
        }
        
        // Load settings from application properties
        public static AppSettings LoadFromApplicationProperties()
        {
            var settings = new AppSettings();
            var app = System.Windows.Application.Current;
            
            if (app.Properties.Contains("GigaChatClientId"))
                settings.GigaChatClientId = app.Properties["GigaChatClientId"] as string ?? string.Empty;
                
            if (app.Properties.Contains("GigaChatClientSecret"))
                settings.GigaChatClientSecret = app.Properties["GigaChatClientSecret"] as string ?? string.Empty;
                
            if (app.Properties.Contains("GigaChatScope"))
                settings.GigaChatScope = app.Properties["GigaChatScope"] as string ?? "GIGACHAT_API_PERS";
            
            // Load other settings from properties if they exist
            
            return settings;
        }
        
        // Save settings to application properties
        public void SaveToApplicationProperties()
        {
            var app = System.Windows.Application.Current;
            
            app.Properties["GigaChatClientId"] = GigaChatClientId;
            app.Properties["GigaChatClientSecret"] = GigaChatClientSecret;
            app.Properties["GigaChatScope"] = GigaChatScope;
            
            // Save other settings to properties
        }
    }
}