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
            Properties["GigaChatToken"] = "YzUyODUxODYtOThjNC00ZGE3LWE1ZDgtZmM1OGY0YzJlMWZjOjkwNDdiMmZiLWVhZTgtNDRlMC1iYzJjLTI0NjM5OTU4MWVhNA==";
            Properties["InitialPromptTemplate"] = "Ты профессиональный {0}. Ты проходишь собеседование. Сейчас я буду задавать вопросы, а тебе нужно на них давать ответ.";
        }
    }
}