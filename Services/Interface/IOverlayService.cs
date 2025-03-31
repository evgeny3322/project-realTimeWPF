using AIInterviewAssistant.WPF.Models;
using System.Drawing;

namespace AIInterviewAssistant.WPF.Services.Interfaces
{
    public interface IOverlayService
    {
        /// <summary>
        /// Shows the solution overlay near the cursor
        /// </summary>
        /// <param name="response">The AI response to display</param>
        /// <param name="position">Optional position override, if null uses current cursor position</param>
        void ShowSolution(AiResponse response, Point? position = null);
        
        /// <summary>
        /// Shows the explanation overlay near the cursor
        /// </summary>
        /// <param name="response">The AI response with explanation to display</param>
        /// <param name="position">Optional position override, if null uses current cursor position</param>
        void ShowExplanation(AiResponse response, Point? position = null);
        
        /// <summary>
        /// Hides any visible overlays
        /// </summary>
        void HideOverlay();
        
        /// <summary>
        /// Updates the overlay's appearance settings
        /// </summary>
        /// <param name="settings">The application settings containing overlay appearance</param>
        void UpdateSettings(AppSettings settings);
        
        /// <summary>
        /// Gets whether the overlay is currently visible
        /// </summary>
        bool IsOverlayVisible { get; }
    }
}