using System.Collections.Generic;
using System.Text.RegularExpressions;
using DotNetTools.SharpGrabber.Grabbed;

namespace YouTubeDownLoader
{
    public class GrabbedMediaComparer : IComparer<GrabbedMedia>
    {

        public int Compare(GrabbedMedia x, GrabbedMedia y)
        {
            if (x.Channels == MediaChannels.Video && y.Channels == MediaChannels.Video)
            {
                return GetNumber(x.FormatTitle) < GetNumber(y.FormatTitle) ? 1 : -1;
            }
            else if (x.Channels == MediaChannels.Audio && y.Channels == MediaChannels.Audio)
            {
                return GetNumber(x.BitRateString) < GetNumber(y.BitRateString) ? 1 : -1;
            }

            return 0;
        }


        public int GetNumber(string text)
        {
            return int.Parse(string.Join("", new Regex("[0-9]").Matches(text)));
        }
    }
}