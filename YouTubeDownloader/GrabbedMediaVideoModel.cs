using System.IO;
using System.Linq;
using DotNetTools.SharpGrabber;
using DotNetTools.SharpGrabber.Grabbed;

namespace YouTubeDownLoader
{
    public class GrabbedMediaVideoModel
    {
        readonly char[] _invalidFileNameChars = Path.GetInvalidFileNameChars();

        public GrabbedMediaVideoModel(GrabbedMedia media, GrabResult result)
        {
            GrabbedMedia = media;
            GrabResult = result;
        }
        public GrabbedMedia GrabbedMedia { get; set; }
        public GrabResult GrabResult { get; set; }

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