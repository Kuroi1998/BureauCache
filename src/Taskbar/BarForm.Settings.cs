using System.Drawing;
using System.Linq;

partial class BarForm
{
    AppearanceSettings appliedAppearance;
    bool appliedGrouping;
    string[] appliedPins = new string[0];

    internal sealed class AppearanceSettings
    {
        readonly Color background = Theme.Bg, text = Theme.Fg, accent = Theme.Accent, line = Theme.Line;
        readonly int size = Settings.TaskbarSize, style = Settings.TaskbarStyle, language = L.CurrentLanguage;
        readonly bool dark = Settings.Dark, contrast = Theme.HighContrast, center = Settings.TaskbarCenter,
            labels = Settings.TaskbarLabels, quick = Settings.TaskbarQuick, bell = Settings.TaskbarBell,
            date = Settings.TaskbarDate, seconds = Settings.TaskbarSeconds, topLine = Settings.TaskbarLine,
            autoHide = Settings.TaskbarAutoHide;

        internal bool SameTheme(AppearanceSettings other)
        {
            return background == other.background && text == other.text && accent == other.accent && line == other.line &&
                dark == other.dark && contrast == other.contrast;
        }

        internal bool SameLayout(AppearanceSettings other)
        {
            return size == other.size && style == other.style && language == other.language && center == other.center &&
                labels == other.labels && quick == other.quick && bell == other.bell && date == other.date &&
                seconds == other.seconds && topLine == other.topLine && autoHide == other.autoHide;
        }
    }

    public void ApplyChangedSettings()
    {
        var current = new AppearanceSettings();
        bool themeChanged = !current.SameTheme(appliedAppearance);
        bool layoutChanged = !current.SameLayout(appliedAppearance);
        appliedAppearance = current;
        if (themeChanged) ApplyTheme();
        if (layoutChanged) ApplyLayoutSettings();
        else if (themeChanged) RefreshGlass();
        if (appliedGrouping != Settings.TaskbarGroup || !appliedPins.SequenceEqual(Settings.TaskbarPins)) RebuildButtons();
    }
}
