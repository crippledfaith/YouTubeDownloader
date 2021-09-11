﻿using System;
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
        private readonly string[] _sizeSuffixes =
            {"bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB"};

        #endregion

        #region Constructor

        public MainWindow()
        {
            InitializeComponent();
            IsEnableDownloadButton(false, false);
            _clipboard.ClipboardChanged += ClipboardChanged;

            if (string.IsNullOrEmpty(Properties.Settings.Default.DownloadPath))
            {
                Properties.Settings.Default.DownloadPath = Path.GetTempPath();
                Properties.Settings.Default.Save();
            }

            if (string.IsNullOrEmpty(Properties.Settings.Default.FinalPath))
            {
                string path = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
                Properties.Settings.Default.FinalPath = path;
                Properties.Settings.Default.Save();
            }
            var applicationName = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name;
            var applicationDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                applicationName);
            if (!Directory.Exists(applicationDataPath))
            {
                Directory.CreateDirectory(applicationDataPath);
            }

            var currentDirectory = applicationDataPath;
            var ffmpegFile = Path.Combine(currentDirectory, "ffmpeg.exe");
            var ffprobeFile = Path.Combine(currentDirectory, "ffprobe.exe");
            if (!File.Exists(ffmpegFile))
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "YouTubeDownLoader.ffmpeg.exe";

                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    using (var fileStream = File.Create(ffmpegFile))
                    {
                        if (stream != null)
                        {
                            stream.Seek(0, SeekOrigin.Begin);
                            stream.CopyTo(fileStream);
                        }
                    }
                }
            }

            if (!File.Exists(ffprobeFile))
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = "YouTubeDownLoader.ffprobe.exe";

                using (Stream stream = assembly.GetManifestResourceStream(resourceName))
                {
                    using (var fileStream = File.Create(ffprobeFile))
                    {
                        if (stream != null)
                        {
                            stream.Seek(0, SeekOrigin.Begin);
                            stream.CopyTo(fileStream);
                        }
                    }
                }
            }

            var ffOptions = new FFOptions { BinaryFolder = currentDirectory };
            GlobalFFOptions.Configure(ffOptions);
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
                    LengthLabel.Content = info.Length.HasValue ? info.Length.Value.ToString("g"): "0:0:0";
                    VideoTypeCombobox.ItemsSource = media.Where(q => q.Channels == MediaChannels.Video && q.Format.Extension == "mp4")
                        .Distinct(new GrabbedMediaEqualityComparer())
                        .OrderBy(q => q, new GrabbedMediaComparer())
                        .Select(q => new GrabbedMediaVideoModel(q, result)).ToList();
                    AudioTypeCombobox.ItemsSource = media.Where(q => q.Channels == MediaChannels.Audio)
                        .Distinct(new GrabbedMediaEqualityComparer())
                        .OrderBy(q => q, new GrabbedMediaComparer())
                        .Select(q => new GrabbedMediaVideoModel(q, result)).ToList();
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
                var videoModel = (GrabbedMediaVideoModel)VideoTypeCombobox.SelectedItem;
                var audioModel = (GrabbedMediaVideoModel)AudioTypeCombobox.SelectedItem;

                var isVideoCheckBoxChecked = VideoCheckBox.IsChecked.HasValue && VideoCheckBox.IsChecked.Value;
                var isAudioCheckBoxChecked = AudioCheckBox.IsChecked.HasValue && AudioCheckBox.IsChecked.Value;


                if (isVideoCheckBoxChecked)
                {
                    finalFilePath = Path.Combine(finalPath, videoModel.ValidFileName);
                    if (File.Exists(finalFilePath))
                    {
                        var dialogResult = ShowMessage($"You have downloaded this media and the file exist {finalFilePath}?\nAre you sure you want to download this file?", MessageBoxButton.YesNo);
                        if (dialogResult == false)
                        {
                            _cancellationTokenSource.Cancel();
                            throw new Exception();
                        }
                    }

                    videoLocalFilePath = Path.Combine(tempFolder, videoModel.RandomFileName);
                    if (File.Exists(videoLocalFilePath))
                        File.Delete(videoLocalFilePath);
                    _progessTypeMessage = "Downloading Video: ";
                    await StartDownload(videoModel.GrabbedMedia.ResourceUri, videoLocalFilePath, _cancellationTokenSource.Token);

                }

                if (isAudioCheckBoxChecked)
                {
                    if (string.IsNullOrEmpty(finalFilePath))
                    {
                        finalFilePath = Path.Combine(finalPath, audioModel.ValidFileName);
                        if (File.Exists(finalFilePath))
                        {
                            var dialogResult = ShowMessage($"You have downloaded this media and the file exist {finalFilePath}?\n Are you sure you want to download this file?");
                            if (dialogResult == false)
                            {
                                _cancellationTokenSource.Cancel();
                                throw new Exception();
                            }
                        }
                    }

                    audioLocalFilePath = Path.Combine(tempFolder, audioModel.RandomFileName);
                    if (File.Exists(audioLocalFilePath))
                        File.Delete(audioLocalFilePath);
                    _progessTypeMessage = "Downloading Audio: ";

                    await StartDownload(audioModel.GrabbedMedia.ResourceUri, audioLocalFilePath, _cancellationTokenSource.Token);
          
                }


                if (File.Exists(finalFilePath))
                    File.Delete(finalFilePath);
                if (isVideoCheckBoxChecked && isAudioCheckBoxChecked)
                {
                    var mediaAnalysis = await FFProbe.AnalyseAsync(videoLocalFilePath);

                    var duration = mediaAnalysis.Duration;

                    _progessTypeMessage = "Merging: ";
                    _ = await FFMpegArguments
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
                ShowMessage($"Download Completed.\nFile:{ finalFilePath}", MessageBoxButton.OK);

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
                ProgressTextBlock.Text = $"{_progessTypeMessage} {percentage:F1}% - ({SizeSuffix(bytesIn)}/{SizeSuffix(totalBytes)})";
                ProgressBar.Value = int.Parse(Math.Truncate(percentage).ToString());
            });
        }

        private async Task StartDownload(Uri url, string localPath, CancellationToken cancellationToken)
        {
            await using var fileStream = File.Create(localPath);
            await DownloadFileAsync(url, fileStream, cancellationToken, ProgressCallback);
        }
   
        private async Task DownloadFileAsync(Uri uri, Stream toStream, CancellationToken cancellationToken = default, Action<long, long> progressCallback = null)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));
            if (toStream == null)
                throw new ArgumentNullException(nameof(toStream));

            using HttpClient client = new HttpClient();
            using HttpResponseMessage response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

            if (progressCallback != null)
            {
                long length = response.Content.Headers.ContentLength ?? -1;
                await using Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                byte[] buffer = new byte[4096];
                int read;
                int totalRead = 0;
                while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await toStream.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                    totalRead += read;
                    progressCallback(totalRead, length);
                }
                Debug.Assert(totalRead == length || length == -1);
            }
            else
            {
                await response.Content.CopyToAsync(toStream).ConfigureAwait(false);
            }

        }

        private async Task<long> GetFileSizeAsync(Uri uri)
        {
            using HttpClient client = new HttpClient();
            using HttpResponseMessage response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            return response.Content.Headers.ContentLength ?? -1;
        }


        private string SizeSuffix(double value, int decimalPlaces = 1)
        {
            if (decimalPlaces < 0)
            {
                throw new ArgumentOutOfRangeException("decimalPlaces");
            }

            if (value < 0)
            {
                return "-" + SizeSuffix(-value, decimalPlaces);
            }

            if (value == 0)
            {
                return string.Format("{0:n" + decimalPlaces + "} bytes", 0);
            }

            // mag is 0 for bytes, 1 for KB, 2, for MB, etc.
            int mag = (int)Math.Log(value, 1024);

            // 1L << (mag * 10) == 2 ^ (10 * mag) 
            // [i.e. the number of bytes in the unit corresponding to mag]
            decimal adjustedSize = (decimal)value / (1L << (mag * 10));

            // make adjustment when the value is large enough that
            // it would round up to 1000 or more
            if (Math.Round(adjustedSize, decimalPlaces) >= 1000)
            {
                mag += 1;
                adjustedSize /= 1024;
            }

            return string.Format("{0:n" + decimalPlaces + "} {1}",
                adjustedSize,
                _sizeSuffixes[mag]);
        }

        private void ResetProgress()
        {
            ProgressTextBlock.Text = $"";
            ProgressBar.Value = 0;
        }

        private async Task UpdateDownloadSize()
        {
            var size = 0d;
            var videoModel = (GrabbedMediaVideoModel)VideoTypeCombobox.SelectedItem;
            var audioModel = (GrabbedMediaVideoModel)AudioTypeCombobox.SelectedItem;
            if (VideoCheckBox.IsChecked.HasValue && VideoCheckBox.IsChecked.Value)
            {
                if (videoModel != null)
                {
                    size += await GetFileSizeAsync(videoModel.GrabbedMedia.ResourceUri);
                }
            }

            if (AudioCheckBox.IsChecked.HasValue && AudioCheckBox.IsChecked.Value)
            {
                if (audioModel != null)
                {
                    size += await GetFileSizeAsync(audioModel.GrabbedMedia.ResourceUri);
                }
            }

            if (Math.Abs(size) < .1)
            {
                IsEnableDownloadButton(false,false);
            }
            else
            {
                IsEnableDownloadButton(true);

            }
            DownloadButton.Content = $"{SizeSuffix(size)}";
        }


        #endregion

        #region UI Event Methords
        private async void VideoTypeComboboxOnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await UpdateDownloadSize();
        }

        private async void AudioTypeComboboxOnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await UpdateDownloadSize();
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
            VideoTypeCombobox.IsEnabled = VideoCheckBox.IsChecked.HasValue && VideoCheckBox.IsChecked.Value;
            if (!VideoTypeCombobox.IsEnabled)
            {
                AudioTypeCombobox.IsEnabled = true;
                AudioCheckBox.IsChecked = true;
            }
            await UpdateDownloadSize();
        }

        private async void AudioCheckBoxOnClick(object sender, RoutedEventArgs e)
        {
            AudioTypeCombobox.IsEnabled = AudioCheckBox.IsChecked.HasValue && AudioCheckBox.IsChecked.Value;
            if (!AudioTypeCombobox.IsEnabled)
            {
                VideoTypeCombobox.IsEnabled = true;
                VideoCheckBox.IsChecked = true;
            }
            await UpdateDownloadSize();
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
