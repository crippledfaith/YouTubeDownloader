using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace YouTubeDownLoader
{
    /// <summary>
    /// Interaction logic for MessageBoxWindow.xaml
    /// </summary>
    public partial class MessageBoxWindow 
    {
        public MessageBoxWindow(MessageBoxButton messageBoxButton = MessageBoxButton.OKCancel)
        {
            InitializeComponent();
            if (messageBoxButton == MessageBoxButton.OK)
            {
                this.CancelButton.Visibility = Visibility.Collapsed;
            }

            if (messageBoxButton == MessageBoxButton.YesNo)
            {
                this.OkButton.Content = "Yes";
                this.CancelButton.Content = "No";
            }
        }

        private void OkButtonOnClick(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }

        private void CancelButtonOnClick(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
