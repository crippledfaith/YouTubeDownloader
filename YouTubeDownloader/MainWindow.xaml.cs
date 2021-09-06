using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using DotNetTools.SharpGrabber;
using DotNetTools.SharpGrabber.Grabbed;
using FFMpegCore;
using WK.Libraries.SharpClipboardNS;

namespace YouTubeDownLoader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        private readonly SharpClipboard _clipboard = new SharpClipboard();
        private bool _monitorClipboard = true;

        private readonly string[] _sizeSuffixes =
            {"bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB"};

        public MainWindow()
        {
            InitializeComponent();
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

            var currentDirectory = Directory.GetCurrentDirectory();
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

            var ffOptions = new FFOptions {BinaryFolder = currentDirectory};
            GlobalFFOptions.Configure(ffOptions);
        }

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

        private async void AddButton_Click(object sender, RoutedEventArgs e)
        {
            await ExtractFileData();
        }

        private async Task ExtractFileData()
        {
            if (string.IsNullOrEmpty(LinkTextBox.Text))
            {
                MessageBox.Show("Invalid Youtube Link.", "Youtube DownLoader", MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            _monitorClipboard = false;
            Grid.IsEnabled = false;
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
                    Console.WriteLine("Time Length: {0}", info.Length);
                    var images = result.Resources<GrabbedImage>();
                    var videos = result.Resources<GrabbedMedia>();
                    var originalUri = images.FirstOrDefault().ResourceUri;
                    VideoImage.Source = new BitmapImage(originalUri);
                    TitleLabel.Content = result.Title;
                    AuthorLabel.Content = info.Author;
                    ViewLabel.Content = info.ViewCount;
                    VideoTypeCombobox.ItemsSource = videos
                        .Where(q => q.Channels == MediaChannels.Video && q.Format.Extension == "mp4")
                        .Distinct(new GrabbedMediaComparer())
                        .Select(q => new GrabbedMediaVideoModel(q, result)).ToList();
                    AudioTypeCombobox.ItemsSource = videos.Where(q => q.Channels == MediaChannels.Audio)
                        .Distinct(new GrabbedMediaComparer())
                        .Select(q => new GrabbedMediaVideoModel(q, result)).ToList();
                    VideoTypeCombobox.SelectedIndex = 0;
                    AudioTypeCombobox.SelectedIndex = 0;
                    if (AutoStartCheckBox.IsChecked.HasValue && AutoStartCheckBox.IsChecked.Value)
                    {
                        await DownloadFile();
                    }
                }
            }
            catch (Exception)
            {
                MessageBox.Show("Invalid Youtube Link.", "Youtube DownLoader", MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }

            Grid.IsEnabled = true;
            _monitorClipboard = true;
        }


        private async void DownloadButtonOnClick(object sender, RoutedEventArgs e)
        {
            await DownloadFile();
        }

        private async Task DownloadFile()
        {
            _monitorClipboard = false;
            Grid.IsEnabled = false;
            var videoLocalFilePath = "";
            var audioLocalFilePath = "";
            var finalFilePath = "";
            try
            {
                var tempFolder = Properties.Settings.Default.DownloadPath;
                var finalPath = Properties.Settings.Default.FinalPath;
                var videoModel = (GrabbedMediaVideoModel) VideoTypeCombobox.SelectedItem;
                var audioModel = (GrabbedMediaVideoModel) AudioTypeCombobox.SelectedItem;
                if (VideoCheckBox.IsChecked.HasValue && VideoCheckBox.IsChecked.Value)
                {
                    videoLocalFilePath = Path.Combine(tempFolder, videoModel.RandomFileName);
                    if (File.Exists(videoLocalFilePath))
                        File.Delete(videoLocalFilePath);

                    await StartDownload(videoModel.GrabbedMedia.ResourceUri.AbsoluteUri, videoLocalFilePath);
                    finalFilePath = Path.Combine(finalPath, videoModel.ValidFileName);
                }

                if (AudioCheckBox.IsChecked.HasValue && AudioCheckBox.IsChecked.Value)
                {
                    audioLocalFilePath = Path.Combine(tempFolder, audioModel.RandomFileName);
                    if (File.Exists(audioLocalFilePath))
                        File.Delete(audioLocalFilePath);
                    await StartDownload(audioModel.GrabbedMedia.ResourceUri.AbsoluteUri, audioLocalFilePath);
                    if (string.IsNullOrEmpty(finalFilePath))
                    {
                        finalFilePath = Path.Combine(finalPath, audioModel.ValidFileName);
                    }
                }


                if (File.Exists(finalFilePath))
                    File.Delete(finalFilePath);
                if (VideoCheckBox.IsChecked.HasValue && VideoCheckBox.IsChecked.Value &&
                    AudioCheckBox.IsChecked.HasValue && AudioCheckBox.IsChecked.Value)
                {
                    await Task.Run(
                        () => { FFMpeg.ReplaceAudio(videoLocalFilePath, audioLocalFilePath, finalFilePath); });
                }
                else
                {
                    if (string.IsNullOrEmpty(videoLocalFilePath))
                    {
                        File.Copy(audioLocalFilePath, finalFilePath);
                    }
                    else
                    {
                        File.Copy(videoLocalFilePath, finalFilePath);
                    }

                }

                MessageBox.Show($"Download Completed.\nFile:{finalFilePath}", "Youtube DownLoader", MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception exception)
            {
                MessageBox.Show(exception.Message, "Youtube DownLoader", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (File.Exists(audioLocalFilePath))
                    File.Delete(audioLocalFilePath);
                if (File.Exists(videoLocalFilePath))
                    File.Delete(videoLocalFilePath);
            }

            _monitorClipboard = true;
            Grid.IsEnabled = true;
        }

        private async Task StartDownload(string url, string localPath)
        {
            WebClient client = new WebClient();
            client.DownloadProgressChanged += client_DownloadProgressChanged;
            client.DownloadFileCompleted += client_DownloadFileCompleted;
            await client.DownloadFileTaskAsync(new Uri(url), localPath);
        }

        void client_DownloadProgressChanged(object sender, DownloadProgressChangedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                double bytesIn = double.Parse(e.BytesReceived.ToString());
                double totalBytes = double.Parse(e.TotalBytesToReceive.ToString());
                double percentage = bytesIn / totalBytes * 100;
                ProgressTextBlock.Text = $"{percentage:F1}% - ({SizeSuffix(bytesIn)}/{SizeSuffix(totalBytes)})";
                ProgressBar.Value = int.Parse(Math.Truncate(percentage).ToString());
            });
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
            int mag = (int) Math.Log(value, 1024);

            // 1L << (mag * 10) == 2 ^ (10 * mag) 
            // [i.e. the number of bytes in the unit corresponding to mag]
            decimal adjustedSize = (decimal) value / (1L << (mag * 10));

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

        void client_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                ProgressTextBlock.Text = $"";
                ProgressBar.Value = 0;
            });
        }

        private void SettingButtonOnClick(object sender, RoutedEventArgs e)
        {
            var settingWindow = new SettingWindow();
            settingWindow.Owner = this;
            settingWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            settingWindow.ShowDialog();
        }

        private async void LinkTextBoxOnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (AutoStartCheckBox.IsChecked.HasValue && AutoStartCheckBox.IsChecked.Value)
            {
                await ExtractFileData();
            }
        }

        private void VideoCheckBoxOnClick(object sender, RoutedEventArgs e)
        {
            VideoTypeCombobox.IsEnabled = VideoCheckBox.IsChecked.HasValue && VideoCheckBox.IsChecked.Value;
            if (!VideoTypeCombobox.IsEnabled)
            {
                AudioTypeCombobox.IsEnabled = true;
                AudioCheckBox.IsChecked = true;
            }
        }

        private void AudioCheckBoxOnClick(object sender, RoutedEventArgs e)
        {
            AudioTypeCombobox.IsEnabled = AudioCheckBox.IsChecked.HasValue && AudioCheckBox.IsChecked.Value;
            if (!AudioTypeCombobox.IsEnabled)
            {
                VideoTypeCombobox.IsEnabled = true;
                VideoCheckBox.IsChecked = true;
            }
        }
    }

    class GrabbedMediaComparer : EqualityComparer<GrabbedMedia>
    {
        public override bool Equals(GrabbedMedia x, GrabbedMedia y)
        {
            return x.FormatTitle == y.FormatTitle && x.BitRateString == y.BitRateString; 
        }

        public override int GetHashCode(GrabbedMedia obj)
        {
            return obj.FormatTitle.GetHashCode();
        }
    }
}
