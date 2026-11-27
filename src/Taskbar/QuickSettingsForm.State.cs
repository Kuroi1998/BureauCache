// BureauCache — lecture et commandes système hors du fil d'interface (T-040)
using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;

partial class QuickSettingsForm
{
    sealed class LevelResult
    {
        public int? Value;
        public bool Muted, InternalDisplay;
        public bool Wrote;
    }

    sealed class QuickLevel
    {
        public readonly bool Audio;
        public readonly string Name;
        public Label Readout;
        public Slider Slider;
        public StartTile Action, SettingsLink;
        public int? Requested, Confirmed;
        public bool ToggleMute;
        public bool? InternalDisplay;
        public Task<LevelResult> Work;
        public long NextRead;
        public QuickLevel(bool audio, string name) { Audio = audio; Name = name; }
    }

    readonly Stopwatch elapsed = Stopwatch.StartNew();
    // La dernière écriture d'un panneau fermé précède la lecture du suivant.
    static Task<LevelResult> lastAudio, lastBrightness;
    long nextBattery, nextMedia;
    Native.QuickMedia currentMedia;
    Task<Tuple<Native.QuickMedia, bool>> mediaRead;
    Task<bool> mediaWrite;
    bool mediaFallback;

    QuickLevel AddLevel(string glyph, string label, int y, bool audio)
    {
        QuickLevel level = new QuickLevel(audio, label);
        level.Readout = LabelAt(label, Pad, y, Inner, 22, true);
        int left = Pad, height = Ui.S(31), ySlider = level.Readout.Bottom + Ui.S(2);
        if (audio)
        {
            string mute = L.T("Couper", "Mute", "Silenciar");
            level.Action = AddTile(glyph, mute, new Rectangle(left, ySlider, Ui.S(126), height), false, () =>
            {
                level.ToggleMute = !level.ToggleMute;
                UpdateLevel(level);
            });
            level.Action.Visible = false;
            left = level.Action.Right + Gap;
        }
        int linkWidth = Ui.S(146);
        level.SettingsLink = AddLink(audio ? Glyphs.Settings : glyph,
            audio ? L.T("Son", "Sound", "Sonido") : L.T("Affichage", "Display", "Pantalla"),
            audio ? "ms-settings:sound" : "ms-settings:display",
            new Rectangle(PanelWidth - Pad - linkWidth, ySlider, linkWidth, height), null, false);
        level.Slider = new Slider(0, 100, 1)
        {
            Bounds = new Rectangle(left, ySlider, level.SettingsLink.Left - Gap - left, height),
            BackColor = Theme.Bg, AccessibleName = label, Visible = false
        };
        level.Slider.EnableSettingsKeyboard();
        level.Slider.Changed += (s, e) =>
        {
            int value = level.Slider.Value;
            level.Requested = value;
            ShowPercent(level, value);
            UpdateLevel(level);
        };
        Controls.Add(level.Slider);
        return level;
    }

    void RefreshControls()
    {
        UpdateLevel(volume);
        UpdateLevel(brightness);
        UpdateMedia();
        long now = elapsed.ElapsedMilliseconds;
        if (now < nextBattery) return;
        nextBattery = now + 4000;
        bool pluggedIn, saving;
        int? percent = Native.QuickBattery(out pluggedIn, out saving);
        battery.Text = !percent.HasValue ? string.Empty :
            L.T("Batterie", "Battery", "Batería") + " · " + percent.Value.ToString(L.Culture) + " %"
            + (pluggedIn ? L.T(" · Secteur", " · Plugged in", " · Conectada") : string.Empty);
        saver.Text = saving ? L.T("Économie active", "Saver on", "Ahorro activo") : L.T("Énergie", "Power", "Energía");
    }

    static LevelResult ReadLevel(bool audio, IntPtr monitor, int? requested, bool toggle, bool? internalDisplay)
    {
        LevelResult result = new LevelResult { Wrote = requested.HasValue || toggle };
        try
        {
            result.Value = audio ? Native.QuickVolume(requested, toggle, out result.Muted) :
                Native.QuickBrightness(monitor, requested, internalDisplay, out result.InternalDisplay);
        }
        catch (Exception) { }
        return result;
    }

    void UpdateLevel(QuickLevel level)
    {
        if (level.Work != null && !level.Work.IsCompleted) return;
        bool pending = level.Requested.HasValue || level.ToggleMute;
        if (level.Work != null)
        {
            // Conserve le résultat final jusqu'au relâchement et ignore les résultats dépassés.
            if (!pending && level.Slider.IsDragging) return;
            LevelResult result = level.Work.Result;
            level.Work = null;
            level.NextRead = elapsed.ElapsedMilliseconds + (level.Audio ? 250 : 800);
            if (!pending) DisplayLevel(level, result);
        }
        if (pending || elapsed.ElapsedMilliseconds >= level.NextRead)
        {
            bool audio = level.Audio, toggle = level.ToggleMute;
            int? requested = level.Requested;
            bool? internalDisplay = pending ? level.InternalDisplay : null;
            IntPtr display = monitor;
            level.Requested = null;
            level.ToggleMute = false;
            level.Work = QueueLevel(audio, display, requested, toggle, internalDisplay);
        }
    }

    static Task<LevelResult> QueueLevel(bool audio, IntPtr display, int? requested, bool toggle, bool? internalDisplay)
    {
        Func<LevelResult> operation = () => ReadLevel(audio, display, requested, toggle, internalDisplay);
        Task<LevelResult> last = audio ? lastAudio : lastBrightness;
        Task<LevelResult> work = last == null || last.IsCompleted ?
            Task.Factory.StartNew(operation, System.Threading.CancellationToken.None, TaskCreationOptions.None, TaskScheduler.Default) :
            last.ContinueWith(t => operation(), TaskScheduler.Default);
        if (audio) lastAudio = work;
        else lastBrightness = work;
        return work;
    }

    static void DisplayLevel(QuickLevel level, LevelResult result)
    {
        if (result.Value.HasValue || !result.Wrote) level.Confirmed = result.Value;
        level.Slider.Visible = level.Confirmed.HasValue;
        if (level.Action != null)
        {
            level.Action.Visible = level.Confirmed.HasValue;
            if (result.Value.HasValue)
                level.Action.Text = result.Muted ? L.T("Rétablir", "Unmute", "Activar") : L.T("Couper", "Mute", "Silenciar");
        }
        if (result.Value.HasValue)
        {
            level.InternalDisplay = result.InternalDisplay;
            ShowPercent(level, result.Value.Value);
            return;
        }
        if (level.Confirmed.HasValue)
        {
            ShowPercent(level, level.Confirmed.Value);
            level.Readout.Text += L.T(" · Modification non confirmée", " · Change not confirmed", " · Cambio sin confirmar");
            return;
        }
        string state = result.Wrote ? L.T("Modification non confirmée", "Change not confirmed", "Cambio sin confirmar") :
            L.T("Indisponible ici", "Unavailable here", "No disponible aquí");
        level.Readout.Text = level.Name + " · " + state;
    }

    static void ShowPercent(QuickLevel level, int value)
    {
        if (level.Slider.Value != value) level.Slider.Value = value;
        level.Readout.Text = level.Name + " · " + value.ToString(L.Culture) + " %";
    }

    void FlushLevel(QuickLevel level)
    {
        if (level == null || (!level.Requested.HasValue && !level.ToggleMute)) return;
        bool audio = level.Audio, toggle = level.ToggleMute;
        int? requested = level.Requested;
        bool? internalDisplay = level.InternalDisplay;
        IntPtr display = monitor;
        QueueLevel(audio, display, requested, toggle, internalDisplay);
    }

    void UpdateMedia()
    {
        if (mediaWrite != null)
        {
            if (!mediaWrite.IsCompleted) return;
            mediaWrite = null;
            nextMedia = 0;
        }
        if (mediaRead != null)
        {
            if (!mediaRead.IsCompleted) return;
            Tuple<Native.QuickMedia, bool> reading = mediaRead.Result;
            mediaRead = null;
            DisplayMedia(reading.Item1, reading.Item2);
        }
        if (elapsed.ElapsedMilliseconds < nextMedia) return;
        nextMedia = elapsed.ElapsedMilliseconds + (mediaFallback ? 6000 : 1800);
        string knownArtwork = artwork.Image != null && currentMedia != null ? currentMedia.ArtworkKey : null;
        mediaRead = Task.Factory.StartNew(() =>
        {
            bool accessible;
            Native.QuickMedia read = Native.ReadQuickMedia(knownArtwork, out accessible);
            return Tuple.Create(read, accessible);
        });
    }

    void DisplayMedia(Native.QuickMedia read, bool accessible)
    {
        mediaFallback = !accessible;
        bool sameArtwork = currentMedia != null && read != null && currentMedia.ArtworkKey == read.ArtworkKey;
        if (!sameArtwork || (read != null && read.Artwork != null))
        {
            Image old = artwork.Image;
            artwork.Image = read == null ? null : read.Artwork;
            if (old != null) old.Dispose();
        }
        currentMedia = read;
        mediaTitle.Text = read != null && !string.IsNullOrEmpty(read.Title) ? read.Title :
            L.T("Lecture multimédia", "Media playback", "Reproducción multimedia");
        mediaArtist.Text = read != null ? read.Artist ?? string.Empty : accessible ?
            L.T("Aucune session active", "No active session", "Ninguna sesión activa") :
            L.T("Commandes multimédias Windows", "Windows media controls", "Controles multimedia de Windows");
        mediaTip.SetToolTip(mediaTitle, mediaTitle.Text);
        mediaTip.SetToolTip(mediaArtist, mediaArtist.Text);
        previous.Enabled = mediaFallback || (read != null && read.Previous);
        playback.Enabled = mediaFallback || (read != null && read.PlayCommand != null);
        next.Enabled = mediaFallback || (read != null && read.Next);
        playback.SetGlyph(read != null && read.Playing ? Glyphs.Pause : Glyphs.Play);
        playback.Text = read == null ? L.T("Lecture / Pause", "Play / Pause", "Reproducir / Pausar") :
            read.Playing ? L.T("Pause", "Pause", "Pausar") : L.T("Lecture", "Play", "Reproducir");
    }

    void SendMedia(int direction)
    {
        if (mediaWrite != null) return;
        Native.QuickMedia snapshot = currentMedia;
        if (snapshot == null && !mediaFallback) return;
        byte key = direction < 0 ? (byte)0xB1 : direction > 0 ? (byte)0xB0 : (byte)0xB3;
        string command = direction < 0 ? "TrySkipPreviousAsync" : direction > 0 ? "TrySkipNextAsync" :
            snapshot == null ? null : snapshot.PlayCommand;
        if (snapshot == null)
        {
            Native.QuickMediaCommand(null, null, key);
            return;
        }
        mediaWrite = Task.Factory.StartNew(() => Native.QuickMediaCommand(snapshot, command, key));
    }

    void DisposeMedia()
    {
        if (artwork != null && artwork.Image != null)
        {
            artwork.Image.Dispose();
            artwork.Image = null;
        }
        if (mediaRead != null)
            mediaRead.ContinueWith(t => { if (t.Result.Item1 != null) t.Result.Item1.Dispose(); }, TaskScheduler.Default);
    }
}
