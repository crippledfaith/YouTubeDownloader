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
        private SearchWindow _searchWindow;
        #endregion

        #region Constructor

        public MainWindow()
        {
            InitializeComponent();
            keypressTimer = new System.Windows.Forms.Timer();
            keypressTimer.Tick += KeypressTimerTick;
            keypressTimer.Interval = 4000;
            keypressTimer.Stop();
            IsEnableDownloadButton(false, false);
            _clipboard.ClipboardChanged += ClipboardChanged;
            Helper.UpdateFolderAndFfmpegConfig();

        }

        private async void KeypressTimerTick(object sender, EventArgs e)
        {

            await ValidUrl(LinkTextBox.Text);
        }



        #endregion

        #region Main Methords

        private async Task ExtractFileData(string videoLink, bool ignoreMessage = true)
        {
            if (ignoreMessage && string.IsNullOrEmpty(videoLink))
            {
                ShowMessage("Invalid Youtube Link.", MessageBoxButton.OK);
                return;
            }

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
                    ViewLabel.Content = video.Engagement.ViewCount.ToString("##,###");
                    LengthLabel.Content = video.Duration.HasValue ? video.Duration.Value.ToString("g") : "0:0:0";
                    RatingLabel.Content = $"({video.Engagement.LikeCount:##,###}) likes ({video.Engagement.DislikeCount:##,###}) Dislikes";
                    var streamManifest = await youtubeClient.Videos.Streams.GetManifestAsync(videoLink);
                    VideoTypeCombobox.ItemsSource = streamManifest.GetVideoOnlyStreams().OrderByDescending(q => q.VideoQuality).Select(q => new MediaModel(youtubeClient, video, streamManifest, q)).ToList().FindAll(q => q.StreamInfo.Container != Container.WebM);
                    AudioTypeCombobox.ItemsSource = streamManifest.GetAudioOnlyStreams().OrderByDescending(q => q.Bitrate).Select(q => new MediaModel(youtubeClient, video, streamManifest, q)).ToList();
                    VideoTypeCombobox.SelectedIndex = 0;
                    AudioTypeCombobox.SelectedIndex = 0;
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

                var isVideoCheckBoxChecked = VideoCheckBox.IsChecked.HasValue && VideoCheckBox.IsChecked.Value;
                var isAudioCheckBoxChecked = AudioCheckBox.IsChecked.HasValue && AudioCheckBox.IsChecked.Value;


                if (isVideoCheckBoxChecked)
                {
                    if (!isAudioCheckBoxChecked)
                    {
                        var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(videoModel.ValidFileName);
                        var fileNameExtension = Path.GetExtension(videoModel.ValidFileName);
                        var newFileName = $"{fileNameWithoutExtension}-(video only){fileNameExtension}";
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

                if (isAudioCheckBoxChecked)
                {
                    if (!isVideoCheckBoxChecked)
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
                if (isVideoCheckBoxChecked && isAudioCheckBoxChecked)
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
                var result = ShowMessage($"Download Completed.\nFile:{ finalFilePath}\nWould like to Open the containing folder?", MessageBoxButton.YesNo);
                if (result.HasValue && result.Value)
                {
                    string argument = "/select, \"" + finalFilePath + "\"";
                    Process.Start("explorer.exe", argument);
                }

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

        private void FileLocationConfirm(string finalFilePath)
        {
            if (File.Exists(finalFilePath))
            {
                var dialogResult =
                    ShowMessage(
                        $"You have downloaded this media and the file already exist.\n{finalFilePath}?\n Are you sure you want to download this file?", MessageBoxButton.YesNo);
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
            var isVideoChecked = VideoCheckBox.IsChecked.HasValue && VideoCheckBox.IsChecked.Value;
            var isAudioChecked = AudioCheckBox.IsChecked.HasValue && AudioCheckBox.IsChecked.Value;
            var videoModel = (MediaModel)VideoTypeCombobox.SelectedItem;
            var audioModel = (MediaModel)AudioTypeCombobox.SelectedItem;

            var size = DownloadManager.GetDownloadSize(isVideoChecked, isAudioChecked, videoModel,
                audioModel);
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
            var settingWindow = new SettingWindow
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };
            settingWindow.ShowDialog();
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
            var isVideoCheckBoxChecked = VideoCheckBox.IsChecked.HasValue && VideoCheckBox.IsChecked.Value;
            VideoTypeCombobox.IsEnabled = isVideoCheckBoxChecked;

            if (!isVideoCheckBoxChecked)
            {
                AudioTypeCombobox.IsEnabled = true;
                AudioCheckBox.IsChecked = true;
            }

            UpdateSettingGrid();

        }

        private void AudioCheckBoxOnClick(object sender, RoutedEventArgs e)
        {
            var isAudioCheckBoxChecked = AudioCheckBox.IsChecked.HasValue && AudioCheckBox.IsChecked.Value;

            AudioTypeCombobox.IsEnabled = isAudioCheckBoxChecked;
            if (!isAudioCheckBoxChecked)
            {
                VideoTypeCombobox.IsEnabled = true;
                VideoCheckBox.IsChecked = true;
            }
            UpdateSettingGrid();
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
            _searchWindow?.Close();
        }
        #endregion



    }

}
