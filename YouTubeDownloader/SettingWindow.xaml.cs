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
    /// Interaction logic for SettingWindow.xaml
    /// </summary>
    public partial class SettingWindow 
    {
        public SettingWindow()
        {
            InitializeComponent();
            TempPathTextBox.Text = Properties.Settings.Default.DownloadPath;
            FinalPathTextBox.Text = Properties.Settings.Default.FinalPath;
        }

        private void OkButtonOnClick(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.DownloadPath = TempPathTextBox.Text;
            Properties.Settings.Default.FinalPath = FinalPathTextBox.Text;
            Properties.Settings.Default.Save();
            this.Close();
        }

        private void OkCancelOnClick(object sender, RoutedEventArgs e)
        {
            this.Close();

        }

        private void TempPathButtonOnClick(object sender, RoutedEventArgs e)
        {
            var folderBrowserDialog = new System.Windows.Forms.FolderBrowserDialog();
            var dialogResult = folderBrowserDialog.ShowDialog();
            if (dialogResult == System.Windows.Forms.DialogResult.OK)
            {
                TempPathTextBox.Text = folderBrowserDialog.SelectedPath;
            }
        }

        private void FinalPathButtonOnClick(object sender, RoutedEventArgs e)
        {
            var folderBrowserDialog = new System.Windows.Forms.FolderBrowserDialog();
            var dialogResult = folderBrowserDialog.ShowDialog();
            if (dialogResult == System.Windows.Forms.DialogResult.OK)
            {
                FinalPathTextBox.Text = folderBrowserDialog.SelectedPath;
            }

        }
    }
}
