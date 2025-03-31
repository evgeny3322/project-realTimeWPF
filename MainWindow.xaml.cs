using AIInterviewAssistant.WPF.ViewModels;
using System.Windows;

namespace AIInterviewAssistant.WPF
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _viewModel;

        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
        }

        private void HotkeyItem_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            // TODO: Allow hotkey rebinding
            MessageBox.Show("Hotkey rebinding not implemented yet", "TODO", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}