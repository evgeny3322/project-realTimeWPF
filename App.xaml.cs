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
            Properties["GigaChatToken"] = "";
            Properties["InitialPromptTemplate"] = "Ты профессиональный {0}. Ты проходишь собеседование. Сейчас я буду задавать вопросы, а тебе нужно на них давать ответ.";
        }
    }
}