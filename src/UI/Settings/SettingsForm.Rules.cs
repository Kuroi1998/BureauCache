// BureauCache — SettingsForm (règles de rangement)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

// Paramètres → Organisation → « Règles de rangement » (D-009) : une ligne par règle, avec Modifier et Supprimer,
// puis « Ajouter une règle ». Les règles s'appliquent quand le rangement automatique est activé, et à
// « Ranger maintenant ».
partial class SettingsForm
{
    Card rulesCard;
    string rulesSig;

    void BuildRulesSection()
    {
        Section(T("RÈGLES DE RANGEMENT", "TIDY-UP RULES", "REGLAS DE ORGANIZACIÓN"));
        rulesCard = cur;
        RebuildRules();
    }

    void RebuildRules()
    {
        string sig = string.Join("|", Settings.ArchiveRules.Select(r => r.Serialize())) + "#" + L.CurrentLanguage;
        if (sig == rulesSig) return;
        rulesSig = sig;
        Card saved = cur;
        cur = rulesCard;
        foreach (Control c in rulesCard.Controls.Cast<Control>().ToList())
        {
            Label label = c as Label;
            if (label != null) dim.Remove(label);
            c.Dispose();
        }
        rulesCard.Separators.Clear();
        cardRows[rulesCard].Clear();
        for (int i = 0; i < Settings.ArchiveRules.Count; i++)
        {
            ArchiveRule rule = Settings.ArchiveRules[i];
            FlatButton del = Button(T("Supprimer", "Delete", "Eliminar"), false, S(100), () =>
            {
                Settings.ArchiveRules = Settings.ArchiveRules.Where(r => r != rule).ToList();
                Settings.Notify();
            });
            del.Danger = true;
            FlatButton edit = Button(T("Modifier", "Edit", "Editar"), false, S(100), () =>
            {
                ArchiveRule changed = RuleDialog.Run(this, rule);
                if (changed == null) return;
                Settings.ArchiveRules = Settings.ArchiveRules.Select(r => r == rule ? changed : r).ToList();
                Settings.Notify();
            });
            Row(rule.Name, Describe(rule), del, null, null, edit);
        }
        FlatButton add = Button(T("Ajouter une règle", "Add a rule", "Añadir una regla"), Settings.ArchiveRules.Count == 0, S(170), () =>
        {
            if (Settings.ArchiveRules.Count >= ArchiveRules.MaxRules) return;
            ArchiveRule created = RuleDialog.Run(this, null);
            if (created == null) return;
            Settings.ArchiveRules = Settings.ArchiveRules.Concat(new[] { created }).ToList();
            Settings.Notify();
        });
        Row(Settings.ArchiveRules.Count == 0 ? T("Aucune règle", "No rule", "Ninguna regla") : T("Ajouter une règle", "Add a rule", "Añadir una regla"),
            T("Ex. : les captures d'écran vont dans Images\\Captures après 7 jours. La première règle qui correspond l'emporte ; les autres fichiers suivent le rangement par ancienneté.",
              "E.g. screenshots go to Pictures\\Screenshots after 7 days. The first matching rule wins; other files follow the age-based tidy-up.",
              "P. ej., las capturas van a Imágenes\\Capturas después de 7 días. Gana la primera regla que coincide; los demás archivos siguen la organización por antigüedad."), add, null);
        Style(rulesCard);
        cur = saved;
        ArrangePages();
    }

    string Describe(ArchiveRule r)
    {
        string what = r.Filter == RuleFilter.Extensions ? string.Join(", ", r.Extensions().Select(e => "." + e).ToArray())
                                                        : T("nom contenant « ", "name containing \"", "nombre que contiene «") + r.Pattern + T(" »", "\"", "»");
        string when = r.Days <= 0 ? T("dès l'arrivée", "on arrival", "al llegar") : T("après ", "after ", "después de ") + r.Days + T(" j", " d", " d");
        return what + " → " + ArchiveRules.TargetName(r.Target) + (r.Sub.Length > 0 ? "\\" + r.Sub : "") + " · " + when;
    }
}
