using AIInterviewAssistant.WPF.Helpers;
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace AIInterviewAssistant.WPF.UI
{
    public partial class NotificationWindow : Window
    {
        public NotificationWindow()
        {
            InitializeComponent();
            
            // Устанавливаем окно как прозрачное для кликов
            this.SourceInitialized += (s, e) =>
            {
                WindowsApiHelper.MakeWindowClickThrough(this);
                WindowsApiHelper.MakeWindowTopmost(this);
            };
            
            // Размещаем уведомление в нижнем правом углу экрана
            this.Loaded += (s, e) =>
            {
                PositionWindow();
            };
        }
        
        public void ShowNotification(string message)
        {
            NotificationText.Text = message;
            
            if (!this.IsVisible)
            {
                // Позиционируем и показываем окно
                PositionWindow();
                this.Show();
                
                // Создаем анимацию появления
                DoubleAnimation fadeInAnimation = new DoubleAnimation
                {
                    From = 0.0,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(500)
                };
                
                this.BeginAnimation(UIElement.OpacityProperty, fadeInAnimation);
            }
        }
        
        private void PositionWindow()
        {
            // Размещаем уведомление в нижнем правом углу экрана
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            double screenHeight = SystemParameters.PrimaryScreenHeight;
            
            // Измеряем размер окна
            this.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            this.Arrange(new Rect(0, 0, this.DesiredSize.Width, this.DesiredSize.Height));
            
            // Вычисляем позицию (с отступом 20 пикселей от краев)
            this.Left = screenWidth - this.ActualWidth - 20;
            this.Top = screenHeight - this.ActualHeight - 20;
        }
        
        // Используем ключевое слово new для явного указания, что мы переопределяем метод
        public new void Hide()
        {
            // Создаем анимацию исчезновения
            DoubleAnimation fadeOutAnimation = new DoubleAnimation
            {
                From = 1.0,
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(300)
            };
            
            fadeOutAnimation.Completed += (s, e) => base.Hide();
            
            this.BeginAnimation(UIElement.OpacityProperty, fadeOutAnimation);
        }
    }
}