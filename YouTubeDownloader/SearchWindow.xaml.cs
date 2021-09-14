using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using AngleSharp.Html.Dom;
using YoutubeExplode;
using YoutubeExplode.Common;

namespace YouTubeDownLoader
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
            catch (Exception e)
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
    }
}
