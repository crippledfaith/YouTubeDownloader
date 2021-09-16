using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using FFMpegCore;

namespace YouTubeDownLoader.Services
{
    public static class Helper
    {
        private static readonly string[] _sizeSuffixes =
            {"bytes", "KB", "MB", "GB", "TB", "PB", "EB", "ZB", "YB"};

        public static string SizeSuffix(double value, int decimalPlaces = 1)
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

        public static void UpdateFolderAndFfmpegConfig()
        {
            if (string.IsNullOrEmpty(Properties.Settings.Default.DownloadPath))
            {
                var path = Path.Combine(Path.GetTempPath(), "YoutubeDownloader");
                Properties.Settings.Default.DownloadPath = path;
                Properties.Settings.Default.Save();
                if (!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
            }

            if (string.IsNullOrEmpty(Properties.Settings.Default.FinalPath))
            {
                string path = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
                path = Path.Combine(path, "Youtube Downloads");
                Properties.Settings.Default.FinalPath = path;
                Properties.Settings.Default.Save();
                if(!Directory.Exists(path))
                {
                    Directory.CreateDirectory(path);
                }
            }

            var applicationName = Assembly.GetEntryAssembly()?.GetName().Name;
            var applicationDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                applicationName);
            if (!Directory.Exists(applicationDataPath))
            {
                Directory.CreateDirectory(applicationDataPath);
            }

            var ffmpegFile = Path.Combine(applicationDataPath, "ffmpeg.exe");
            var ffprobeFile = Path.Combine(applicationDataPath, "ffprobe.exe");
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

            var ffOptions = new FFOptions { BinaryFolder = applicationDataPath };
            GlobalFFOptions.Configure(ffOptions);
        }
    }
}
