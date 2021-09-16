using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using YouTubeDownLoader.Models;

namespace YouTubeDownLoader.Services
{
    public class DownloadManager
    {
        public async Task StartDownload(MediaModel info, string localPath, CancellationToken cancellationToken, Action<double, long> progressCallback)
        {
            await DownloadFileAsync(info, localPath, cancellationToken, progressCallback);
        }

        public double GetDownloadSize(bool isVideoAudio, bool isVideo, bool isAudio, MediaModel videoModel, MediaModel audioModel, MediaModel videoAudioModel)
        {
            var size = 0d;
            if (isVideoAudio)
            {
                size = videoAudioModel != null ? videoAudioModel.StreamInfo.Size.Bytes : 0;
            }
            else
            {
                if (isVideo)
                {
                    if (videoModel != null)
                    {
                        size += videoModel != null ? videoModel.StreamInfo.Size.Bytes : 0;
                    }
                }

                if (isAudio)
                {
                    if (audioModel != null)
                    {
                        size += audioModel != null ? audioModel.StreamInfo.Size.Bytes : 0;
                    }
                }
            }

            return size;
        }

        private async Task DownloadFileAsync(MediaModel info, string localPath, CancellationToken cancellationToken = default, Action<double, long> progressCallback = null)
        {
            if (progressCallback != null)
            {
                var progress = new Progress<double>(p => progressCallback(p, info.StreamInfo.Size.Bytes));
                await MediaModel.YoutubeClient.Videos.Streams.DownloadAsync(info.StreamInfo, localPath, progress, cancellationToken);
            }
            else
            {
                await MediaModel.YoutubeClient.Videos.Streams.DownloadAsync(info.StreamInfo, localPath, null, cancellationToken);
            }
        }

    }
}
