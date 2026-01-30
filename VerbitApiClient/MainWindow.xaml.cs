using System.Windows;

namespace VerbitApiClient
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            // Open WebSocket Order window on startup
            var webSocketOrderWindow = new WebSocketOrderWindow();
            webSocketOrderWindow.Show();
            // Close this main window
            this.Close();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Exit application when main window closes
            Application.Current.Shutdown();
        }
    }
}
