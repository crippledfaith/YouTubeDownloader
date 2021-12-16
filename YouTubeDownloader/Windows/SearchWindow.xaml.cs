using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using YouTubeDownLoader.Models;
using YoutubeExplode;

namespace YouTubeDownLoader.Windows
{
    /// <summary>
    /// Interaction logic for SearchWindow.xaml
    /// </summary>
    public partial class SearchWindow
    {
        private CancellationTokenSource _cancelationToken;
        private ObservableCollection<SearchModel> observableCollection = new ObservableCollection<SearchModel>();
        public SearchWindow()
        {
            InitializeComponent();
            DataGrid.ItemsSource = observableCollection;
            if (Properties.Settings.Default.SearchWindowFirstLoaded)
            {
                this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                Properties.Settings.Default.SearchWindowFirstLoaded = false;
                Properties.Settings.Default.Save();
            }
            else
            {
                this.Top = Properties.Settings.Default.SearchWindowTop;
                this.Left = Properties.Settings.Default.SearchWindowLeft;
            }
        }

        private async void SearchButton_OnClick(object sender, RoutedEventArgs e)
        {
            await Search(SearchTextBox.Text);
        }

        private async Task Search(string text)
        {
            _cancelationToken?.Cancel();
            _cancelationToken = new CancellationTokenSource();
            observableCollection.Clear();
            try
            {
                var youtubeClient = new YoutubeClient();
                await foreach (var batch in youtubeClient.Search.GetVideosAsync(text, _cancelationToken.Token))
                {
                    observableCollection.Add(new SearchModel(batch));
                }

            }
            catch (Exception)
            {

            }
        }

        private void DataGridMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            try
            {
                var item = (SearchModel)DataGrid.SelectedItem;
                if (item != null)
                {
                    Clipboard.SetText(item.Url);
                }
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private async void SearchTextBoxKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                await Search(SearchTextBox.Text);
        }

        private void WindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_cancelationToken != null)
                _cancelationToken.Cancel();
            Properties.Settings.Default.SearchWindowTop = this.Top;
            Properties.Settings.Default.SearchWindowLeft = this.Left;
            Properties.Settings.Default.Save();
        }
    }
}
