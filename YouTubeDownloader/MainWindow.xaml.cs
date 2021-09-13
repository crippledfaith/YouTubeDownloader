using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using DotNetTools.SharpGrabber;
using DotNetTools.SharpGrabber.Grabbed;
using FFMpegCore;
using FFMpegCore.Enums;
using WK.Libraries.SharpClipboardNS;

namespace YouTubeDownLoader
{
    public partial class MainWindow
    {
        #region Private Class Variables

        private readonly SharpClipboard _clipboard = new SharpClipboard();
        private bool _monitorClipboard = true;
        private string _progessTypeMessage = "";
        private CancellationTokenSource _cancellationTokenSource;

        public DownloadManager DownloadManager = new DownloadManager();
        #endregion

        #region Constructor

        public MainWindow()
        {
            InitializeComponent();
            IsEnableDownloadButton(false, false);
            _clipboard.ClipboardChanged += ClipboardChanged;
            Helper.UpdateFolderAndFfmpegConfig();
        }



        #endregion

        #region Main Methords

        private async Task ExtractFileData()
        {
            if (string.IsNullOrEmpty(LinkTextBox.Text))
            {
                ShowMessage("Invalid Youtube Link.", MessageBoxButton.OK);
                return;
            }

            EnableControls(false);
            IsEnableDownloadButton(false, false);
            try
            {
                var grabber = GrabberBuilder.New()
                    .UseDefaultServices()
                    .AddYouTube()
                    .Build();
                var result = await grabber.GrabAsync(new Uri(LinkTextBox.Text));
                if (result != null)
                {
                    var info = result.Resource<GrabbedInfo>();
                    var images = result.Resources<GrabbedImage>();
                    var media = result.Resources<GrabbedMedia>();
                    var originalUri = images.FirstOrDefault()?.ResourceUri;
                    VideoImage.Source = new BitmapImage(originalUri);
                    TitleLabel.Content = result.Title;
                    AuthorLabel.Content = info.Author;
                    ViewLabel.Content = info.ViewCount.Value.ToString("##,###");
                    LengthLabel.Content = info.Length.HasValue ? info.Length.Value.ToString("g") : "0:0:0";
                    VideoTypeCombobox.ItemsSource = media.Where(q => q.Channels == MediaChannels.Video && q.Format.Extension == "mp4")//
                        .Distinct(new GrabbedMediaEqualityComparer())
                        .OrderBy(q => q, new GrabbedMediaComparer())
                        .Select(q => new GrabbedMediaModel(q, result)).ToList();
                    AudioTypeCombobox.ItemsSource = media.Where(q => q.Channels == MediaChannels.Audio)
                        .Distinct(new GrabbedMediaEqualityComparer())
                        .OrderBy(q => q, new GrabbedMediaComparer())
                        .Select(q => new GrabbedMediaModel(q, result)).ToList();
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
                ShowMessage("Invalid Youtube Link.", MessageBoxButton.OK);
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
                var videoModel = (GrabbedMediaModel)VideoTypeCombobox.SelectedItem;
                var audioModel = (GrabbedMediaModel)AudioTypeCombobox.SelectedItem;

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
                        $"You have downloaded this media and the file exist {finalFilePath}?\n Are you sure you want to download this file?",MessageBoxButton.YesNo);
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

        private void ClipboardChanged(object sender, SharpClipboard.ClipboardChangedEventArgs e)
        {
            if (!_monitorClipboard)
                return;
            if (e.ContentType == SharpClipboard.ContentTypes.Text)
            {
                var clipboardText = _clipboard.ClipboardText.ToLower();
                if (clipboardText.Contains("https://www.youtube.com/") || clipboardText.Contains("https://youtu.be"))
                {
                    LinkTextBox.Text = _clipboard.ClipboardText;
                }
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

        private void ProgressCallback(long bytesReceived, long totalBytesToReceive)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                double bytesIn = bytesReceived;
                double totalBytes = totalBytesToReceive;
                double percentage = bytesIn / totalBytes * 100;
                ProgressTextBlock.Text = $"{_progessTypeMessage} {percentage:F1}% - ({Helper.SizeSuffix(bytesIn)}/{Helper.SizeSuffix(totalBytes)})";
                ProgressBar.Value = int.Parse(Math.Truncate(percentage).ToString());
            });
        }

        private void ResetProgress()
        {
            ProgressTextBlock.Text = $"";
            ProgressBar.Value = 0;
        }

        #endregion

        #region UI Event Methords
        private async void VideoTypeComboboxOnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await UpdateSettingGrid();
        }

        private async void AudioTypeComboboxOnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await UpdateSettingGrid();
        }

        private async Task UpdateSettingGrid()
        {
            var isVideoChecked = VideoCheckBox.IsChecked.HasValue && VideoCheckBox.IsChecked.Value;
            var isAudioChecked = AudioCheckBox.IsChecked.HasValue && AudioCheckBox.IsChecked.Value;
            var grabbedVideoMediaModel = (GrabbedMediaModel)VideoTypeCombobox.SelectedItem;
            var grabbedAudioMediaModel = (GrabbedMediaModel)AudioTypeCombobox.SelectedItem;

            var size = await DownloadManager.GetDownloadSize(isVideoChecked, isAudioChecked, grabbedVideoMediaModel,
                grabbedAudioMediaModel);
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
            await ExtractFileData();
        }

        private async void LinkTextBoxOnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                await ExtractFileData();
        }

        private async void LinkTextBoxOnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (AutoStartCheckBox.IsChecked.HasValue && AutoStartCheckBox.IsChecked.Value)
            {
                await ExtractFileData();
            }
        }

        private async void VideoCheckBoxOnClick(object sender, RoutedEventArgs e)
        {
            var isVideoCheckBoxChecked = VideoCheckBox.IsChecked.HasValue && VideoCheckBox.IsChecked.Value;
            VideoTypeCombobox.IsEnabled = isVideoCheckBoxChecked;

            if (!isVideoCheckBoxChecked)
            {
                AudioTypeCombobox.IsEnabled = true;
                AudioCheckBox.IsChecked = true;
            }

            await UpdateSettingGrid();

        }

        private async void AudioCheckBoxOnClick(object sender, RoutedEventArgs e)
        {
            var isAudioCheckBoxChecked = AudioCheckBox.IsChecked.HasValue && AudioCheckBox.IsChecked.Value;

            AudioTypeCombobox.IsEnabled = isAudioCheckBoxChecked;
            if (!isAudioCheckBoxChecked)
            {
                VideoTypeCombobox.IsEnabled = true;
                VideoCheckBox.IsChecked = true;
            }
            await UpdateSettingGrid();
        }

        private async void DownloadButtonOnClick(object sender, RoutedEventArgs e)
        {
            await StartDownloadingProcess();
        }

        private void CancelButtonOnClick(object sender, RoutedEventArgs e)
        {
            _cancellationTokenSource.Cancel();
        }

        #endregion


    }

}
