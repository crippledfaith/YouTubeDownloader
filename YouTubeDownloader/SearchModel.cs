﻿using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using YouTubeDownLoader.Annotations;
using YoutubeExplode;
using YoutubeExplode.Search;
using Timer = System.Timers.Timer;

namespace YouTubeDownLoader
{
    public class SearchModel : INotifyPropertyChanged
    {
        readonly Timer _timer = new Timer(500);
        private readonly YoutubeClient _client;
        private readonly VideoSearchResult _video;
        private string _title;
        private BitmapImage _image;
        private string _url;
        private string _author;

        public string Title
        {
            get => _title;
            set
            {
                _title = value;
                OnPropertyChanged(nameof(Title));
            }
        }

        public string Url
        {
            get { return _url; }
            set
            {
                _url = value;
                OnPropertyChanged(nameof(Url));
            }
        }

        public string Author
        {
            get 
            {
                return _author;
            }
            set
            {
                _author = value;
                OnPropertyChanged(nameof(Author));
            }
        }

        public string Duration { get; private set; }

        public BitmapImage Image
        {
            get { return _image; }
            set
            {
                _image = value;
                OnPropertyChanged(nameof(Image));
            }
        }

        public SearchModel(VideoSearchResult video)
        {
            this._video = video;
            Title = _video.Title;
            Url = video.Url;
            Author = video.Author.Title;
            Duration = video.Duration.ToString();
            Image = new BitmapImage(new Uri(video.Thumbnails[0].Url));
            
        }





        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
