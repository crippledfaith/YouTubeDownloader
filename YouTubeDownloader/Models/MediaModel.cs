using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;


namespace YouTubeDownLoader.Models
{
    public class MediaModel
    {
        public static YoutubeClient YoutubeClient { get; set; }
        readonly List<char> _invalidFileNameChars = Path.GetInvalidFileNameChars().ToList();

        public MediaModel(YoutubeClient youtubeClient , Video video,StreamManifest media,IStreamInfo streamInfo)
        {
            YoutubeClient = youtubeClient;
            Video = video;
            StreamManifest = media;
            StreamInfo = streamInfo;
            _invalidFileNameChars.Add('\'');
        }
        public StreamManifest StreamManifest { get; set; }
        public Video Video { get; set; }
        public IStreamInfo StreamInfo { get; set; }

        public string RandomFileName => $"{Guid.NewGuid()}.{StreamInfo.Container.Name}";

        public string ValidFileName
        {
            get
            {
                var fommat = "";
                if (IsVideo)
                {
                    fommat = $"{((VideoOnlyStreamInfo)StreamInfo).VideoQuality.Label}({((VideoOnlyStreamInfo)StreamInfo).VideoCodec})";
                }
                else
                {
                    fommat = $"{((AudioOnlyStreamInfo)StreamInfo).Bitrate.KiloBitsPerSecond:## 'kbps'}({((AudioOnlyStreamInfo)StreamInfo).AudioCodec})";
                }
                var formattableString = $"{ Video.Title}-{fommat}.{ StreamInfo.Container.Name}";

                var validFilename = new string(formattableString
                    .Where(ch => !_invalidFileNameChars.Contains(ch)).ToArray());

                return validFilename;
            }
        }
        public bool IsVideo => StreamInfo is VideoOnlyStreamInfo;

        public string FormatTitle => IsVideo
            ? $"{((VideoOnlyStreamInfo)StreamInfo).VideoQuality.Label}({((VideoOnlyStreamInfo)StreamInfo).VideoCodec})-{((VideoOnlyStreamInfo)StreamInfo).Container.Name}"
            : $"{((AudioOnlyStreamInfo)StreamInfo).Bitrate.KiloBitsPerSecond:## 'kbps'}({((AudioOnlyStreamInfo)StreamInfo).AudioCodec})-{((AudioOnlyStreamInfo)StreamInfo).Container.Name}";

    }
}