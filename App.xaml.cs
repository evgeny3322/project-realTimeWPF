using System.Windows;

namespace AIInterviewAssistant.WPF
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Установка начальных настроек
            Properties["MaximumRecordLengthInSeconds"] = 20;
            
            // Настройки GigaChat API
            Properties["GigaChatClientId"] = "c5285186-98c4-4da7-a5d8-fc58f4c2e1fc";
            Properties["GigaChatClientSecret"] = "3ed1d039-def6-40e7-872d-4ee629d933fe";
            Properties["GigaChatScope"] = "GIGACHAT_API_PERS";
            
            // Шаблон начального промпта
            Properties["InitialPromptTemplate"] = "Ты профессиональный {0}. Ты проходишь собеседование. Сейчас я буду задавать вопросы, а тебе нужно на них давать ответ.";
            
            // Настройки горячих клавиш по умолчанию
            Properties["CaptureScreenHotkey"] = SharpHook.Native.KeyCode.VcPrintScreen;
            Properties["ShowSolutionHotkey"] = SharpHook.Native.KeyCode.VcF9;
        }
    }
}