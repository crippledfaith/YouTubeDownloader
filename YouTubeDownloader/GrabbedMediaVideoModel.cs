using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DotNetTools.SharpGrabber;
using DotNetTools.SharpGrabber.Grabbed;

namespace YouTubeDownLoader
{
    public class GrabbedMediaVideoModel
    {
        readonly List<char> _invalidFileNameChars = Path.GetInvalidFileNameChars().ToList();

        public GrabbedMediaVideoModel(GrabbedMedia media, GrabResult result)
        {
            GrabbedMedia = media;
            GrabResult = result;
            _invalidFileNameChars.Add('\'');
        }
        public GrabbedMedia GrabbedMedia { get; set; }
        public GrabResult GrabResult { get; set; }

        public string RandomFileName => $"{Guid.NewGuid()}.{ GrabbedMedia.Format.Extension}";

        public string ValidFileName
        {
            get
            {
                var formattableString = $"{ GrabResult.Title}-{ FormatTitle }.{ GrabbedMedia.Format.Extension}";

                var validFilename = new string(formattableString
                    .Where(ch => !_invalidFileNameChars.Contains(ch)).ToArray()).Replace(" ", "");

                return validFilename;
            }
        }

        public string FormatTitle => GrabbedMedia.Channels == MediaChannels.Video
            ? $"{GrabbedMedia.Resolution} {GrabbedMedia.Container}"
            : $"{GrabbedMedia.BitRateString} {GrabbedMedia.Container}";

    }
}