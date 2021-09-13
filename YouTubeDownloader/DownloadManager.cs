using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace YouTubeDownLoader
{
    public class DownloadManager
    {
        public async Task StartDownload(GrabbedMediaModel info, string localPath, CancellationToken cancellationToken, Action<long, long> progressCallback)
        {
            await using var fileStream = File.Create(localPath);
            await DownloadFileAsync(info, fileStream, cancellationToken, progressCallback);
        }

        public async Task<double> GetDownloadSize(bool isVideo, bool isAudio, GrabbedMediaModel videoModel, GrabbedMediaModel audioModel)
        {
            var size = 0d;

            if (isVideo)
            {
                if (videoModel != null)
                {
                    size += await GetFileSizeAsync(videoModel.GrabbedMedia.ResourceUri);
                }
            }

            if (isAudio)
            {
                if (audioModel != null)
                {
                    size += await GetFileSizeAsync(audioModel.GrabbedMedia.ResourceUri);
                }
            }



            return size;
        }

        private async Task DownloadFileAsync(GrabbedMediaModel info, Stream toStream, CancellationToken cancellationToken = default, Action<long, long> progressCallback = null)
        {
            var uri = info.GrabbedMedia.ResourceUri;
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));
            if (toStream == null)
                throw new ArgumentNullException(nameof(toStream));

            using HttpClient client = new HttpClient();
            using HttpResponseMessage response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            long length = response.Content.Headers.ContentLength ?? -1;


            await GetDataAsync(info, toStream, cancellationToken, progressCallback, response, length);
        }

        private static async Task GetDataAsync(GrabbedMediaModel info, Stream toStream, CancellationToken cancellationToken,
            Action<long, long> progressCallback, HttpResponseMessage response, long length)
        {
            await using Stream originalStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
            byte[] buffer = new byte[9999999];
            int totalRead = 0;
            using var stream = await info.GrabResult.WrapStreamAsync(originalStream);
            await GetDataAsync(toStream, cancellationToken, progressCallback, length, stream, buffer, totalRead);

        }

        private static async Task<int> GetDataAsync(Stream toStream, CancellationToken cancellationToken, Action<long, long> progressCallback,
            long length, Stream stream, byte[] buffer, int totalRead)
        {
            int read;
            while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
            {
                await toStream.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                totalRead += read;
                progressCallback(totalRead, length);
            }

            return totalRead;
        }


        private async Task<long> GetFileSizeAsync(Uri uri)
        {
            using HttpClient client = new HttpClient();
            using HttpResponseMessage response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            return response.Content.Headers.ContentLength ?? -1;
        }

    }
}
