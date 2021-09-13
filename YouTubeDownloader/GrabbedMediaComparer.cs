using System;
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
                var resolutionSame = x.Resolution == y.Resolution;
                if (resolutionSame)
                {
                    var containerBigger = String.Compare(x.Container, y.Container, comparisonType: StringComparison.OrdinalIgnoreCase);
                    return containerBigger;
                }
                else
                {
                    var resolutionBigger = GetNumberByNumbers(x.Resolution) < GetNumberByNumbers(y.Resolution);
                    return resolutionBigger ? 1 : -1;
                }
            }

            if (x.Channels == MediaChannels.Audio && y.Channels == MediaChannels.Audio)
            {
                return GetNumberByNumbers(x.BitRateString) < GetNumberByNumbers(y.BitRateString) ? 1 : -1;
            }

            return 0;
        }

        public int GetNumberByNumbers(string text)
        {
            return int.Parse(string.Join("", new Regex("[0-9]").Matches(text)));
        }
    }
}