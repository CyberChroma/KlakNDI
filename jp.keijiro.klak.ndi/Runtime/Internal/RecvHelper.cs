using IntPtr = System.IntPtr;
#if MIRROR
using Mirror;
#endif
using System.Text.RegularExpressions;

namespace Klak.Ndi {

// Small helper class for NDI recv interop
static class RecvHelper
{
    public static Interop.Source? FindSource(string sourceName)
    {
        foreach (var source in SharedInstance.Find.CurrentSources) {
            string urlAddress;
            if (Regex.Match(source.UrlAddress, @":\d{4}$").Success) {
                urlAddress = source.UrlAddress.Remove(source.UrlAddress.Length - 5);
            } else {
                urlAddress = source.UrlAddress;
            }
#if MIRROR
            if (source.NdiName.Contains(sourceName) && urlAddress == NetworkManager.singleton.networkAddress) {
                return source;
            }
#endif
            if (source.NdiName.Contains(sourceName)) {
                return source;
            }
        }
        return null;
    }

    public static unsafe Interop.Recv TryCreateRecv(string sourceName)
    {
        var source = FindSource(sourceName);
        if (source == null) return null;

        var opt = new Interop.Recv.Settings
          { Source = (Interop.Source)source,
            ColorFormat = Interop.ColorFormat.Fastest,
            Bandwidth = Interop.Bandwidth.Highest };

        return Interop.Recv.Create(opt);
    }

    public static Interop.VideoFrame? TryCaptureVideoFrame(Interop.Recv recv)
    {
        Interop.VideoFrame video;
        var type = recv.Capture(out video, IntPtr.Zero, IntPtr.Zero, 0);
        if (type != Interop.FrameType.Video) return null;
        return (Interop.VideoFrame?)video;
    }
}

} // namespace Klak.Ndi
