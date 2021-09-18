using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using FFMpegCore;
using FFMpegCore.Enums;
using WK.Libraries.SharpClipboardNS;
using YouTubeDownLoader.Models;
using YouTubeDownLoader.Services;
using YoutubeExplode;
using Container = YoutubeExplode.Videos.Streams.Container;

namespace YouTubeDownLoader.Windows
{
    public partial class MainWindow
    {
        #region Private Class Variables

        private readonly SharpClipboard _clipboard = new SharpClipboard();
        private bool _monitorClipboard = true;
        private string _progessTypeMessage = "";
        private CancellationTokenSource _cancellationTokenSource;
        private string _currentLink = "";
        public DownloadManager DownloadManager = new DownloadManager();
        private readonly System.Windows.Forms.Timer keypressTimer;
        private readonly System.Windows.Forms.Timer showSettingTimers;
        private SearchWindow _searchWindow;
        #endregion

        #region Constructor

        public MainWindow()
        {
            //Properties.Settings.Default.Reset();
            InitializeComponent();
            keypressTimer = new System.Windows.Forms.Timer();
            keypressTimer.Tick += KeypressTimerTick;
            keypressTimer.Interval = 2000;
            keypressTimer.Stop();
            showSettingTimers = new System.Windows.Forms.Timer();
            showSettingTimers.Tick += ShowSettingTimersTick;
            showSettingTimers.Interval = 1000;
            IsEnableDownloadButton(false, false);
            _clipboard.ClipboardChanged += ClipboardChanged;
            ShowMoreToggleButton.IsChecked = Properties.Settings.Default.IsVideoAudio;
            VideoCheckBox.IsChecked = Properties.Settings.Default.IsVideo;
            AudioCheckBox.IsChecked = Properties.Settings.Default.IsAudio;
            ShowMoreSettings();
            AudioTypeToggle();
            VideoTypeToggle();
            if(Properties.Settings.Default.FistLoaded)
            {
                this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
            else
            {
                this.Top = Properties.Settings.Default.WindowTop;
                this.Left = Properties.Settings.Default.WindowLeft;
            }
            Helper.UpdateFolderAndFfmpegConfig();
        }

        #endregion

        #region Main Methords

        private async Task ExtractFileData(string videoLink, bool ignoreMessage = true)
        {
            if (ignoreMessage && string.IsNullOrEmpty(videoLink))
            {
                ShowMessage("Please provide a link.", MessageBoxButton.OK);
                return;
            }
            ProgressBar.IsIndeterminate = true;
            EnableControls(false);
            IsEnableDownloadButton(false, false);
            try
            {
                var youtubeClient = new YoutubeClient();
                var video = await youtubeClient.Videos.GetAsync(videoLink);
                if (video != null)
                {
                    var originalUri = video.Thumbnails[0].Url;
                    VideoImage.Source = new BitmapImage(new Uri(originalUri));
                    TitleLabel.Content = video.Title;
                    AuthorLabel.Content = video.Author.Title;
                    ViewLabel.Content = video.Engagement.ViewCount.ToString("N0");
                    LengthLabel.Content = video.Duration.HasValue ? video.Duration.Value.ToString("g") : "0:0:0";
                    RatingLabel.Content = $"{video.Engagement.LikeCount:N0} likes / {video.Engagement.DislikeCount:N0} dislikes";
                    var streamManifest = await youtubeClient.Videos.Streams.GetManifestAsync(videoLink);
                    VideoTypeCombobox.ItemsSource = streamManifest.GetVideoOnlyStreams().OrderByDescending(q => q.VideoQuality).Select(q => new MediaModel(youtubeClient, video, streamManifest, q)).ToList().FindAll(q => q.StreamInfo.Container != Container.WebM);
                    AudioTypeCombobox.ItemsSource = streamManifest.GetAudioOnlyStreams().OrderByDescending(q => q.Bitrate).Select(q => new MediaModel(youtubeClient, video, streamManifest, q)).ToList();
                    VideoAudioTypeCombobox.ItemsSource = streamManifest.GetMuxedStreams().OrderByDescending(q => q.VideoQuality).ThenBy(q => q.Bitrate).Select(q => new MediaModel(youtubeClient, video, streamManifest, q)).ToList();
                    VideoTypeCombobox.SelectedIndex = 0;
                    AudioTypeCombobox.SelectedIndex = 0;
                    VideoAudioTypeCombobox.SelectedIndex = 0;
                    DownloadButton.IsEnabled = true;
                    if (AutoStartCheckBox.IsChecked.HasValue && AutoStartCheckBox.IsChecked.Value)
                    {
                        await StartDownloadingProcess();
                    }
                }
            }
            catch (Exception ex)
            {
                if (ignoreMessage)
                {
                    ShowMessage(ex.Message, MessageBoxButton.OK);
                }
            }
            IsEnableDownloadButton(true);
            EnableControls(true);
            ProgressBar.IsIndeterminate = false;
        }

        private async Task StartDownloadingProcess()
        {
            EnableControls(false);
            IsEnableDownloadButton(false);
            _cancellationTokenSource = new CancellationTokenSource();

            var videoLocalFilePath = "";
            var audioLocalFilePath = "";
            var finalFilePath = "";
            try
            {
                var tempFolder = Properties.Settings.Default.DownloadPath;
                var finalPath = Properties.Settings.Default.FinalPath;
                var videoModel = (MediaModel)VideoTypeCombobox.SelectedItem;
                var audioModel = (MediaModel)AudioTypeCombobox.SelectedItem;


                var isVideo = VideoCheckBox.IsChecked.HasValue && VideoCheckBox.IsChecked.Value;
                var isAudio = AudioCheckBox.IsChecked.HasValue && AudioCheckBox.IsChecked.Value;
                var isVideoAudio = !(ShowMoreToggleButton.IsChecked.HasValue && ShowMoreToggleButton.IsChecked.Value);

                if (isVideoAudio)
                {
                    videoModel = (MediaModel)VideoAudioTypeCombobox.SelectedItem;
                    isAudio = false;
                }

                if (isVideo)
                {
                    if (!isAudio)
                    {
                        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(videoModel.ValidFileName);
                        var fileNameExtension = Path.GetExtension(videoModel.ValidFileName);
                        var newFileName = $"{fileNameWithoutExtension}-(video only){fileNameExtension}";
                        if (isVideoAudio)
                        {
                            newFileName = $"{fileNameWithoutExtension}{fileNameExtension}";
                        }
                        finalFilePath = Path.Combine(finalPath, newFileName);
                        FileLocationConfirm(finalFilePath);
                    }
                    else
                    {
                        finalFilePath = Path.Combine(finalPath, videoModel.ValidFileName);
                        FileLocationConfirm(finalFilePath);
                    }
                    videoLocalFilePath = Path.Combine(tempFolder, videoModel.RandomFileName);
                    if (File.Exists(videoLocalFilePath))
                        File.Delete(videoLocalFilePath);
                    _progessTypeMessage = "Downloading Video: ";
                    await DownloadManager.StartDownload(videoModel, videoLocalFilePath, _cancellationTokenSource.Token, ProgressCallback);
                }

                if (isAudio)
                {
                    if (!isVideo)
                    {
                        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(audioModel.ValidFileName);
                        var fileNameExtension = Path.GetExtension(audioModel.ValidFileName);
                        var newFileName = $"{fileNameWithoutExtension}-(audio only){fileNameExtension}";
                        finalFilePath = Path.Combine(finalPath, newFileName);
                        FileLocationConfirm(finalFilePath);
                    }

                    audioLocalFilePath = Path.Combine(tempFolder, audioModel.RandomFileName);
                    if (File.Exists(audioLocalFilePath))
                        File.Delete(audioLocalFilePath);
                    _progessTypeMessage = "Downloading Audio: ";
                    await DownloadManager.StartDownload(audioModel, audioLocalFilePath, _cancellationTokenSource.Token, ProgressCallback);
                }


                if (File.Exists(finalFilePath))
                    File.Delete(finalFilePath);
                if (isVideo && isAudio)
                {
                    var mediaAnalysis = await FFProbe.AnalyseAsync(videoLocalFilePath);
                    var duration = mediaAnalysis.Duration;
                    _progessTypeMessage = "Merging: ";
                    await FFMpegArguments
                        .FromFileInput(videoLocalFilePath)
                        .AddFileInput(audioLocalFilePath)
                        .OutputToFile(finalFilePath, true, options => options
                            .CopyChannel()
                            .WithAudioCodec(AudioCodec.Aac)
                            .WithAudioBitrate(AudioQuality.Good))
                        .NotifyOnProgress(q => { OnPercentageProgress(q, duration); })
                        .CancellableThrough(_cancellationTokenSource.Token)
                        .ProcessAsynchronously();

                }
                else
                {
                    File.Copy(string.IsNullOrEmpty(videoLocalFilePath) ?
                            audioLocalFilePath :
                            videoLocalFilePath,
                        finalFilePath);
                }
                var sucessMessage = $"Download Completed.\nFile: { finalFilePath}";
                ShowNotificaion(sucessMessage, finalFilePath);
    

            }
            catch (Exception exception)
            {
                if (!_cancellationTokenSource.IsCancellationRequested)
                {
                    ShowMessage(exception.Message, MessageBoxButton.OK);
                }
            }
            finally
            {
                if (File.Exists(audioLocalFilePath))
                    File.Delete(audioLocalFilePath);
                if (File.Exists(videoLocalFilePath))
                    File.Delete(videoLocalFilePath);
            }

            ResetProgress();
            EnableControls(true);
            IsEnableDownloadButton(true);
        }

        private void ShowNotificaion(string sucessMessage, string finalFilePath)
        {
            var notificationWindow = new NotificationWindow(sucessMessage, finalFilePath);
            notificationWindow.Show();
        }

        private void FileLocationConfirm(string finalFilePath)
        {
            if (File.Exists(finalFilePath))
            {
                var dialogResult =
                    ShowMessage(
                        $"You have downloaded this media and the file already exist.\n{finalFilePath}?\n\nAre you sure you want to download this file?", MessageBoxButton.YesNo);
                if (dialogResult == false)
                {
                    _cancellationTokenSource.Cancel();
                    throw new Exception();
                }
            }
        }

        private bool? ShowMessage(string message, MessageBoxButton messageBoxButton = MessageBoxButton.OKCancel)
        {
            var messageBoxWindow = new MessageBoxWindow(messageBoxButton);
            messageBoxWindow.Owner = this;
            messageBoxWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            messageBoxWindow.MessageLabel.Text = message;
            messageBoxWindow.ShowDialog();
            return messageBoxWindow.DialogResult;
        }

        #endregion

        #region Helper Methords

        private async void ClipboardChanged(object sender, SharpClipboard.ClipboardChangedEventArgs e)
        {
            if (!_monitorClipboard)
                return;
            if (e.ContentType == SharpClipboard.ContentTypes.Text)
            {
                var clipboardText = _clipboard.ClipboardText;
                await ValidUrl(clipboardText, ignoreMessage: false);
            }
        }

        private async Task ValidUrl(string linkUrL, bool ignoreMessage = false)
        {
            keypressTimer.Stop();
            if ((linkUrL != _currentLink) && (linkUrL.Contains("https://www.youtube.com/watch") || linkUrL.Contains("https://youtu.be")))
            {
                _currentLink = linkUrL;
                LinkTextBox.Text = linkUrL;

                await ExtractFileData(linkUrL, ignoreMessage);
            }
        }

        private void EnableControls(bool enable)
        {
            AddVideoPanel.IsEnabled = enable;
            SettingButton.IsEnabled = enable;
            SettingsGrid.IsEnabled = enable;
            SimpleSettingGrid.IsEnabled = enable;
            ShowMoreToggleButton.IsEnabled = enable;


            _monitorClipboard = enable;
        }

        private void IsEnableDownloadButton(bool enable, bool? cancelEnable = null)
        {
            DownloadButton.IsEnabled = enable;
            if (cancelEnable.HasValue)
            {
                CancelButton.IsEnabled = cancelEnable.Value;
            }
            else
            {
                CancelButton.IsEnabled = !enable;
            }
        }

        private void OnPercentageProgress(TimeSpan timeSpan, TimeSpan duration)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                var progress = timeSpan.TotalSeconds / duration.TotalSeconds * 100;
                ProgressTextBlock.Text = $"{_progessTypeMessage} {progress:F1}% - ({timeSpan:g}/{duration:g})";
                ProgressBar.Value = progress;
            });
        }

        private void ProgressCallback(double progress, long totalBytesToReceived)
        {
            Application.Current.Dispatcher.Invoke(() => { UpdateProgress(progress, totalBytesToReceived); });
        }

        private void UpdateProgress(double progress, long totalBytesToReceived)
        {
            double bytesIn = totalBytesToReceived * progress;
            double totalBytes = totalBytesToReceived;
            var progressMessage = $"{_progessTypeMessage} {progress * 100:F1}% - ({Helper.SizeSuffix(bytesIn)}/{Helper.SizeSuffix(totalBytes)})";
            ProgressTextBlock.Text = progressMessage;
            ProgressBar.Value = progress * 100;
        }

        private void ResetProgress()
        {
            ProgressTextBlock.Text = $"";
            ProgressBar.Value = 0;
        }

        private void ShowSettings()
        {
            var settingWindow = new SettingWindow
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            settingWindow.ShowDialog();
        }

        private void ShowMoreSettings()
        {
            if (ShowMoreToggleButton.IsChecked == true)
            {
                SimpleSettingGrid.Visibility = Visibility.Collapsed;
                SettingsGrid.Visibility = Visibility.Visible;
                Properties.Settings.Default.IsVideoAudio = true;
            }
            else
            {
                SimpleSettingGrid.Visibility = Visibility.Visible;
                SettingsGrid.Visibility = Visibility.Collapsed;
                Properties.Settings.Default.IsVideoAudio = false;
            }
            Properties.Settings.Default.Save();

            UpdateSettingGrid();
        }

        private void VideoTypeToggle()
        {
            var isVideoCheckBoxChecked = VideoCheckBox.IsChecked.HasValue && VideoCheckBox.IsChecked.Value;
            VideoTypeCombobox.IsEnabled = isVideoCheckBoxChecked;

            if (!isVideoCheckBoxChecked)
            {
                AudioTypeCombobox.IsEnabled = true;
                AudioCheckBox.IsChecked = true;
                Properties.Settings.Default.IsAudio = true;
            }

            Properties.Settings.Default.IsVideo = isVideoCheckBoxChecked;
            Properties.Settings.Default.Save();

            UpdateSettingGrid();
        }


        private void AudioTypeToggle()
        {
            var isAudioCheckBoxChecked = AudioCheckBox.IsChecked.HasValue && AudioCheckBox.IsChecked.Value;

            AudioTypeCombobox.IsEnabled = isAudioCheckBoxChecked;
            if (!isAudioCheckBoxChecked)
            {
                VideoTypeCombobox.IsEnabled = true;
                VideoCheckBox.IsChecked = true;
                Properties.Settings.Default.IsVideo = true;
            }
            Properties.Settings.Default.IsAudio = isAudioCheckBoxChecked;
            Properties.Settings.Default.Save();

            UpdateSettingGrid();
        }

        #endregion

        #region UI Event Methords
        private void VideoTypeComboboxOnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateSettingGrid();
        }

        private void AudioTypeComboboxOnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateSettingGrid();
        }

        private void UpdateSettingGrid()
        {
            var isVideoAudioChecked = !(ShowMoreToggleButton.IsChecked.HasValue && ShowMoreToggleButton.IsChecked.Value);

            var isVideoChecked = VideoCheckBox.IsChecked.HasValue && VideoCheckBox.IsChecked.Value;
            var isAudioChecked = AudioCheckBox.IsChecked.HasValue && AudioCheckBox.IsChecked.Value;
            var videoModel = (MediaModel)VideoTypeCombobox.SelectedItem;
            var audioModel = (MediaModel)AudioTypeCombobox.SelectedItem;
            var vedioAudioModel = (MediaModel)VideoAudioTypeCombobox.SelectedItem;

            var size = DownloadManager.GetDownloadSize(isVideoAudioChecked, isVideoChecked, isAudioChecked, videoModel, audioModel, vedioAudioModel);
            if (Math.Abs(size) < .1)
            {
                IsEnableDownloadButton(false, false);
            }
            else
            {
                IsEnableDownloadButton(true);
            }

            DownloadButton.Content = $"{Helper.SizeSuffix(size)}";
        }

        private void SettingButtonOnClick(object sender, RoutedEventArgs e)
        {
            ShowSettings();
        }

        private async void AddButtonClick(object sender, RoutedEventArgs e)
        {
            await ExtractFileData(LinkTextBox.Text);
        }

        private async void LinkTextBoxOnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                await ValidUrl(LinkTextBox.Text);
        }

        private void LinkTextBoxOnTextChanged(object sender, TextChangedEventArgs e)
        {
            keypressTimer.Stop();
            keypressTimer.Start();
        }

        private void VideoCheckBoxOnClick(object sender, RoutedEventArgs e)
        {
            VideoTypeToggle();
        }

        private void AudioCheckBoxOnClick(object sender, RoutedEventArgs e)
        {
            AudioTypeToggle();
        }

        private void ShowMoreToggleButtonClick(object sender, RoutedEventArgs e)
        {
            ShowMoreSettings();
        }
        private async void DownloadButtonOnClick(object sender, RoutedEventArgs e)
        {
            await StartDownloadingProcess();
        }

        private void CancelButtonOnClick(object sender, RoutedEventArgs e)
        {
            var responce = ShowMessage("Are you sure you want to cancel?", MessageBoxButton.YesNo);
            if (responce == true)
                _cancellationTokenSource.Cancel();
        }

        private void SearchButtonOnClick(object sender, RoutedEventArgs e)
        {
            if (_searchWindow == null)
            {

                _searchWindow = new SearchWindow();
                _searchWindow.Closing += SearchWindowClosing;
            }

            _searchWindow.Show();
            _searchWindow.Activate();
        }

        private void SearchWindowClosing(object sender, CancelEventArgs e)
        {
            _searchWindow = null;
        }

        private void MainWindowOnClosing(object sender, CancelEventArgs e)
        {
            Properties.Settings.Default.WindowTop = this.Top;
            Properties.Settings.Default.WindowLeft = this.Left;
            Properties.Settings.Default.Save();
            _searchWindow?.Close();
        }

        private void MetroWindowLoaded(object sender, RoutedEventArgs e)
        {
            showSettingTimers.Start();

        }


        private void ShowSettingTimersTick(object sender, EventArgs e)
        {
            showSettingTimers.Stop();
            if (Properties.Settings.Default.FistLoaded)
            {
                ShowSettings();
                Properties.Settings.Default.FistLoaded = false;
                Properties.Settings.Default.Save();
            }
        }

        private async void KeypressTimerTick(object sender, EventArgs e)
        {
            await ValidUrl(LinkTextBox.Text);
        }
        #endregion

    }

}
