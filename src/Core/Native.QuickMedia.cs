// BureauCache — session multimédia de Windows, chargée à l'exécution sans SDK.
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;

static partial class Native
{
    internal sealed class QuickMedia : IDisposable
    {
        public string Title, Artist, ArtworkKey, PlayCommand;
        public Bitmap Artwork;
        public bool Playing, Previous, Next;
        internal object Session;
        internal Type SessionType;

        public void Dispose() { if (Artwork != null) Artwork.Dispose(); }
    }

    const string MediaRuntime = ", Windows.Media, ContentType=WindowsRuntime";
    const string FoundationRuntime = ", Windows.Foundation, ContentType=WindowsRuntime";
    static class WinRtTypes
    {
        internal static readonly Type AsyncInfo = Type.GetType("Windows.Foundation.IAsyncInfo" + FoundationRuntime, false);
    }

    static class MediaTypes
    {
        internal static readonly Type Manager = Type.GetType("Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager" + MediaRuntime, false);
    }

    internal static QuickMedia ReadQuickMedia(string knownArtwork, out bool accessible)
    {
        accessible = false;
        if (Sandbox.Active) return null;
        try
        {
            Type managerType = MediaTypes.Manager;
            if (managerType == null || WinRtTypes.AsyncInfo == null) return null;
            object manager = WinRtResult(managerType.GetMethod("RequestAsync"), null);
            if (manager == null) return null;
            MethodInfo getSession = managerType.GetMethod("GetCurrentSession");
            object session = getSession.Invoke(manager, null);
            accessible = true;
            if (session == null) return null;
            Type type = getSession.ReturnType;
            QuickMedia media = new QuickMedia { Session = session, SessionType = type };
            MethodInfo getInfo = type.GetMethod("GetPlaybackInfo");
            object info = getInfo.Invoke(session, null);
            if (info != null)
            {
                PropertyInfo getControls = getInfo.ReturnType.GetProperty("Controls");
                object controls = getControls.GetValue(info, null);
                media.Playing = string.Equals(getInfo.ReturnType.GetProperty("PlaybackStatus").GetValue(info, null).ToString(), "Playing", StringComparison.Ordinal);
                if (controls != null)
                {
                    Type ct = getControls.PropertyType;
                    media.Previous = (bool)ct.GetProperty("IsPreviousEnabled").GetValue(controls, null);
                    media.Next = (bool)ct.GetProperty("IsNextEnabled").GetValue(controls, null);
                    if ((bool)ct.GetProperty("IsPlayPauseToggleEnabled").GetValue(controls, null))
                        media.PlayCommand = "TryTogglePlayPauseAsync";
                    else if (media.Playing && (bool)ct.GetProperty("IsPauseEnabled").GetValue(controls, null))
                        media.PlayCommand = "TryPauseAsync";
                    else if (!media.Playing && (bool)ct.GetProperty("IsPlayEnabled").GetValue(controls, null))
                        media.PlayCommand = "TryPlayAsync";
                }
            }
            ReadMediaProperties(media, knownArtwork);
            return media;
        }
        catch (Exception) { accessible = false; return null; }
    }

    static void ReadMediaProperties(QuickMedia media, string knownArtwork)
    {
        try
        {
            MethodInfo getProps = media.SessionType.GetMethod("TryGetMediaPropertiesAsync");
            object props = WinRtResult(getProps, media.Session);
            if (props == null) return;
            Type pt = getProps.ReturnType.GetGenericArguments()[0];
            media.Title = (string)pt.GetProperty("Title").GetValue(props, null);
            media.Artist = (string)pt.GetProperty("Artist").GetValue(props, null);
            string album = (string)pt.GetProperty("AlbumTitle").GetValue(props, null);
            string app = (string)media.SessionType.GetProperty("SourceAppUserModelId").GetValue(media.Session, null);
            media.ArtworkKey = string.Join("\0", app, media.Title, media.Artist, album);
            if (media.ArtworkKey == knownArtwork) return;
            PropertyInfo thumbnail = pt.GetProperty("Thumbnail");
            object reference = thumbnail.GetValue(props, null);
            if (reference != null) media.Artwork = ReadMediaArtwork(thumbnail.PropertyType, reference);
        }
        catch (Exception) { }   // Une pochette absente ou illisible ne bloque pas les commandes.
    }

    static Bitmap ReadMediaArtwork(Type referenceType, object reference)
    {
        Type extensions = Type.GetType("System.IO.WindowsRuntimeStreamExtensions, System.Runtime.WindowsRuntime, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", false);
        Type input = Type.GetType("Windows.Storage.Streams.IRandomAccessStream, Windows.Storage, ContentType=WindowsRuntime", false);
        if (extensions == null || input == null) return null;
        object content = WinRtResult(referenceType.GetMethod("OpenReadAsync"), reference);
        if (content == null) return null;
        Stream stream = null;
        try
        {
            MethodInfo asStream = extensions.GetMethod("AsStream", new[] { input });
            stream = (Stream)asStream.Invoke(null, new[] { content });
            if (!stream.CanSeek || stream.Length <= 0 || stream.Length > 2 * 1024 * 1024) return null;
            using (Image image = Image.FromStream(stream))
            {
                if (image.Width < 1 || image.Height < 1 || (long)image.Width * image.Height > 16000000) return null;
                double scale = 96.0 / Math.Max(image.Width, image.Height);
                return new Bitmap(image, Math.Max(1, (int)Math.Round(image.Width * scale)), Math.Max(1, (int)Math.Round(image.Height * scale)));
            }
        }
        finally
        {
            if (stream != null) stream.Dispose();
            else ((IDisposable)content).Dispose();
        }
    }

    static object WinRtResult(MethodInfo start, object target, object[] arguments = null,
        int timeout = 1200, CancellationToken cancellation = default(CancellationToken))
    {
        if (cancellation.IsCancellationRequested) return null;
        object operation = start.Invoke(target, arguments);
        if (operation == null) return null;
        Type asyncInfo = WinRtTypes.AsyncInfo;
        PropertyInfo status = asyncInfo.GetProperty("Status");
        Stopwatch watch = Stopwatch.StartNew();
        bool finished = false;
        try
        {
            while (Convert.ToInt32(status.GetValue(operation, null)) == 0)
            {
                if (watch.ElapsedMilliseconds >= timeout || cancellation.IsCancellationRequested)
                {
                    asyncInfo.GetMethod("Cancel").Invoke(operation, null);
                    CloseWhenStopped(operation, status, asyncInfo.GetMethod("Close"));
                    return null;
                }
                Thread.Sleep(20);
            }
            finished = true;
            return start.ReturnType.GetMethod("GetResults").Invoke(operation, null);
        }
        finally { if (finished) asyncInfo.GetMethod("Close").Invoke(operation, null); }
    }

    // Close n'est permis qu'une fois l'opération terminée ou annulée : on attend son arrêt hors du fil appelant (CDX-45)
    static void CloseWhenStopped(object operation, PropertyInfo status, MethodInfo close)
    {
        System.Threading.Tasks.Task.Factory.StartNew(() =>
        {
            try
            {
                for (int i = 0; i < 250 && Convert.ToInt32(status.GetValue(operation, null)) == 0; i++) Thread.Sleep(20);
                if (Convert.ToInt32(status.GetValue(operation, null)) != 0) close.Invoke(operation, null);
            }
            catch (Exception) { }
        });
    }

    internal static bool QuickMediaCommand(QuickMedia media, string command, byte fallbackKey)
    {
        if (Sandbox.Active) return false;
        if (media == null)
        {
            keybd_event(fallbackKey, 0, 0, UIntPtr.Zero);
            keybd_event(fallbackKey, 0, 2 /* KEYEVENTF_KEYUP */, UIntPtr.Zero);
            return true;
        }
        if (command == null) return false;
        try { return object.Equals(WinRtResult(media.SessionType.GetMethod(command), media.Session), true); }
        catch (Exception) { return false; }
    }
}
