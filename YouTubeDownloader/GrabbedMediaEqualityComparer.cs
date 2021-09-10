using System.Collections.Generic;
using DotNetTools.SharpGrabber.Grabbed;

namespace YouTubeDownLoader
{
    public class GrabbedMediaEqualityComparer : EqualityComparer<GrabbedMedia>
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