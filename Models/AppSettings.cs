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
            
            // Load hotkeys
            if (app.Properties.Contains("TakeScreenshotHotkey"))
                settings.TakeScreenshotHotkey = (KeyCode)app.Properties["TakeScreenshotHotkey"];
                
            if (app.Properties.Contains("ShowSolutionHotkey"))
                settings.ShowSolutionHotkey = (KeyCode)app.Properties["ShowSolutionHotkey"];
                
            if (app.Properties.Contains("ShowExplanationHotkey"))
                settings.ShowExplanationHotkey = (KeyCode)app.Properties["ShowExplanationHotkey"];
                
            if (app.Properties.Contains("AlternativeSolutionHotkey"))
                settings.AlternativeSolutionHotkey = (KeyCode)app.Properties["AlternativeSolutionHotkey"];
            
            // Load UI settings
            if (app.Properties.Contains("OverlayTextSize"))
                settings.OverlayTextSize = (int)app.Properties["OverlayTextSize"];
                
            if (app.Properties.Contains("OverlayTextColor"))
                settings.OverlayTextColor = app.Properties["OverlayTextColor"] as string ?? "#FF00FF00";
                
            if (app.Properties.Contains("OverlayBackgroundColor"))
                settings.OverlayBackgroundColor = app.Properties["OverlayBackgroundColor"] as string ?? "#80000000";
            
            // Load prompt templates
            if (app.Properties.Contains("SolutionPromptTemplate"))
                settings.SolutionPromptTemplate = app.Properties["SolutionPromptTemplate"] as string ?? settings.SolutionPromptTemplate;
                
            if (app.Properties.Contains("ExplanationPromptTemplate"))
                settings.ExplanationPromptTemplate = app.Properties["ExplanationPromptTemplate"] as string ?? settings.ExplanationPromptTemplate;
            
            // Load other settings
            if (app.Properties.Contains("HideWhenScreenRecording"))
                settings.HideWhenScreenRecording = (bool)app.Properties["HideWhenScreenRecording"];
            
            return settings;
        }
        
        // Save settings to application properties
        public void SaveToApplicationProperties()
        {
            var app = System.Windows.Application.Current;
            
            // Save GigaChat settings
            app.Properties["GigaChatClientId"] = GigaChatClientId;
            app.Properties["GigaChatClientSecret"] = GigaChatClientSecret;
            app.Properties["GigaChatScope"] = GigaChatScope;
            
            // Save hotkeys
            app.Properties["TakeScreenshotHotkey"] = TakeScreenshotHotkey;
            app.Properties["ShowSolutionHotkey"] = ShowSolutionHotkey;
            app.Properties["ShowExplanationHotkey"] = ShowExplanationHotkey;
            app.Properties["AlternativeSolutionHotkey"] = AlternativeSolutionHotkey;
            
            // Save UI settings
            app.Properties["OverlayTextSize"] = OverlayTextSize;
            app.Properties["OverlayTextColor"] = OverlayTextColor;
            app.Properties["OverlayBackgroundColor"] = OverlayBackgroundColor;
            
            // Save prompt templates
            app.Properties["SolutionPromptTemplate"] = SolutionPromptTemplate;
            app.Properties["ExplanationPromptTemplate"] = ExplanationPromptTemplate;
            
            // Save other settings
            app.Properties["HideWhenScreenRecording"] = HideWhenScreenRecording;
        }
    }
}