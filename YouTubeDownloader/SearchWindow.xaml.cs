using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using AngleSharp.Html.Dom;
using YouTubeDownLoader.Annotations;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Search;
using Timer = System.Timers.Timer;

namespace YouTubeDownLoader
{
    /// <summary>
    /// Interaction logic for SearchWindow.xaml
    /// </summary>
    public partial class SearchWindow
    {
        private CancellationTokenSource _cancelationToken;
        public SearchWindow()
        {
            InitializeComponent();
        }

        private void SearchTextBoxOnSelectionChanged(object sender, RoutedEventArgs e)
        {

        }

        private async void SearchButton_OnClick(object sender, RoutedEventArgs e)
        {
            await Search(SearchTextBox.Text);
        }

        private async Task Search(string text)
        {
            _cancelationToken?.Cancel();
            _cancelationToken = new CancellationTokenSource();
            try
            {
                var youtubeClient = new YoutubeClient();
                var searchResults = await youtubeClient.Search.GetResultsAsync(text, _cancelationToken.Token);
                DataGrid.ItemsSource = searchResults.Select(q => new SearchModel(q, _cancelationToken.Token));
            }
            catch (Exception e)
            {

            }

        }
    }

    public class SearchModel : INotifyPropertyChanged
    {
        private CancellationToken _cancellationToken;
        private string _title;
        readonly Timer _timer = new Timer(500);
        private readonly YoutubeClient _client;

        public ISearchResult SearchResult { get; }

        public string Title
        {
            get => _title;
            set
            {
                _title = value;
                OnPropertyChanged(nameof(Title));
            }
        }

        public string Url { get; set; }

        public BitmapImage Image { get; set; }

        public SearchModel(ISearchResult searchResult, CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
            SearchResult = searchResult;
            Title = searchResult.Title;
            Url = searchResult.Url;
            _client = new YoutubeClient();
            _timer.Elapsed += TimerElapsed;
            _timer.Start();
        }

        private async void TimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            _timer.Stop();
            await UpdateData();
        }

        private async Task UpdateData()
        {
            try
            {
                var video = await _client.Videos.GetAsync(Url, cancellationToken: _cancellationToken);
                if (video != null)
                {
                    var originalUri = video.Thumbnails[0].Url;
                    Image = new BitmapImage(new Uri(originalUri));
                }
            }
            catch (Exception e)
            {

            }
          
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
