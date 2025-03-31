using System.Windows;

namespace AIInterviewAssistant.WPF
{
    public partial class App : Application
    {
        // Удаляем метод Main, так как он уже генерируется автоматически
        
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Установка начальных настроек
            Properties["MaximumRecordLengthInSeconds"] = 20;
            
            // Настройки GigaChat API
            Properties["GigaChatClientId"] = "c5285186-98c4-4da7-a5d8-fc58f4c2e1fc";
            Properties["GigaChatClientSecret"] = "716009ee-6d04-400b-b95a-308d2f621526";
            Properties["GigaChatScope"] = "GIGACHAT_API_PERS";
            
            // Шаблон начального промпта
            Properties["InitialPromptTemplate"] = "Ты профессиональный {0}. Ты проходишь собеседование. Сейчас я буду задавать вопросы, а тебе нужно на них давать ответ.";
            
            // Настройки горячих клавиш по умолчанию
            Properties["CaptureScreenHotkey"] = SharpHook.Native.KeyCode.VcPrintScreen;
            Properties["ShowSolutionHotkey"] = SharpHook.Native.KeyCode.VcF9;
        }
    }
}
{
    public partial class App : Application
    {
        // Static entry point
        [STAThread]
        public static void Main()
        {
            App app = new App();
            app.InitializeComponent();
            app.Run();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Установка начальных настроек
            Properties["MaximumRecordLengthInSeconds"] = 20;
            
            // Настройки GigaChat API
            Properties["GigaChatClientId"] = "c5285186-98c4-4da7-a5d8-fc58f4c2e1fc";
            Properties["GigaChatClientSecret"] = "716009ee-6d04-400b-b95a-308d2f621526";
            Properties["GigaChatScope"] = "GIGACHAT_API_PERS";
            
            // Шаблон начального промпта
            Properties["InitialPromptTemplate"] = "Ты профессиональный {0}. Ты проходишь собеседование. Сейчас я буду задавать вопросы, а тебе нужно на них давать ответ.";
        }
    }
}