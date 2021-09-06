using System;
using System.ComponentModel;
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

namespace YouTubeDownLoader
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow
    {
        public MainWindow()
        {
            InitializeComponent();
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

        private async void AddButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(LinkTextBox.Text))
            {
                MessageBox.Show("Invalid Youtube Link.", "Youtube DownLoader", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
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
                    VideoTypeCombobox.ItemsSource = videos.Where(q => q.Channels == MediaChannels.Video && q.Format.Extension=="mp4")
                        .Select(q => new GrabbedMediaVideoModel(q, result)).ToList();
                    AudioTypeCombobox.ItemsSource = videos.Where(q => q.Channels == MediaChannels.Audio)
                        .Select(q => new GrabbedMediaVideoModel(q, result)).ToList();
                    VideoTypeCombobox.SelectedIndex = 0;
                    AudioTypeCombobox.SelectedIndex = 0;
                }
            
            }
            catch (Exception exception)
            {
                MessageBox.Show("Invalid Youtube Link.", "Youtube DownLoader", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            Grid.IsEnabled = true;
        }



        private async void DownloadButtonOnClick(object sender, RoutedEventArgs e)
        {
            Grid.IsEnabled = false;
            var videoLocalFilePath = "";
            var audioLocalFilePath = "";
            try
            {

                var tempFolder = Properties.Settings.Default.DownloadPath;
                var finalPath = Properties.Settings.Default.FinalPath;

                var videoModel = (GrabbedMediaVideoModel) VideoTypeCombobox.SelectedItem;
                videoLocalFilePath = Path.Combine(tempFolder, videoModel.RandomFileName);
                if (File.Exists(videoLocalFilePath))
                    File.Delete(videoLocalFilePath);

                await startDownload(videoModel.GrabbedMedia.ResourceUri.AbsoluteUri, videoLocalFilePath);

                var audioModel = (GrabbedMediaVideoModel) AudioTypeCombobox.SelectedItem;
                audioLocalFilePath = Path.Combine(tempFolder, audioModel.RandomFileName);
                if (File.Exists(audioLocalFilePath))
                    File.Delete(audioLocalFilePath);
                await startDownload(audioModel.GrabbedMedia.ResourceUri.AbsoluteUri, audioLocalFilePath);


                var finalFilePath = Path.Combine(finalPath, videoModel.ValidFileName);
                if (File.Exists(finalFilePath))
                    File.Delete(finalFilePath);
                var audioMediaInfo = await FFProbe.AnalyseAsync(audioLocalFilePath);
                var videoMediaInfo = await FFProbe.AnalyseAsync(videoLocalFilePath);

                await Task.Run(() => { FFMpeg.ReplaceAudio(videoLocalFilePath, audioLocalFilePath, finalFilePath); });

                File.Delete(audioLocalFilePath);
                File.Delete(videoLocalFilePath);


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
            Grid.IsEnabled = true;
        }

        private async Task startDownload(string url, string localPath)
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
                //label2.Text = "Downloaded " + e.BytesReceived + " of " + e.TotalBytesToReceive;
                ProgressBar.Value = int.Parse(Math.Truncate(percentage).ToString());
            });
        }

        void client_DownloadFileCompleted(object sender, AsyncCompletedEventArgs e)
        {

        }

        private void SettingButtonOnClick(object sender, RoutedEventArgs e)
        {
            var settingWindow = new SettingWindow();
            settingWindow.Owner = this;
            settingWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            settingWindow.ShowDialog();
        }
    }
}
