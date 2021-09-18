using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace YouTubeDownLoader.Windows
{
    /// <summary>
    /// Interaction logic for NotificationWindow.xaml
    /// </summary>
    public partial class NotificationWindow
    {
        private Timer _timer = new Timer(15000);
        private string _finalFilePath;



        public NotificationWindow(string detail, string finalFilePath)
        {
            InitializeComponent();
            DetailTextBlock.Text = detail;
            _timer.Elapsed += TimerElapsed;
            _timer.Start();
            this._finalFilePath = finalFilePath;

        }

        private void TimerElapsed(object sender, ElapsedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(new Action(() =>
            {
                _timer.Stop();
                this.Close();
            }));
        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _timer.Stop();
            _timer.Dispose();
  
        }

        private void WindowLoaded(object sender, RoutedEventArgs e)
        {
            var desktopWorkingArea = System.Windows.SystemParameters.WorkArea;
            this.Left = desktopWorkingArea.Right - this.Width;
            this.Top = desktopWorkingArea.Bottom - this.Height;
        }

        private void OpenFolderButtonOnClick(object sender, RoutedEventArgs e)
        {
            string argument = "/select, \"" + _finalFilePath + "\"";
            Process.Start("explorer.exe", argument);
        }
        private void OpenFileButtonOnClick(object sender, RoutedEventArgs e)
        {
            //Process.Start(_finalFilePath);
            _ = new Process { StartInfo = new ProcessStartInfo(_finalFilePath) { UseShellExecute = true } }.Start();
        }
    }
}
