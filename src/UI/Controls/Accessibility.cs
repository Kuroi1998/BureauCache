// BureauCache — accessibilité des contrôles dessinés à la main
using System;
using System.Windows.Forms;

// =====================================================================
//  Lecteurs d'écran (GEM-07)
// =====================================================================
// Les contrôles de BureauCache sont dessinés à la main : sans aide, un lecteur d'écran (Narrateur, NVDA) ne voit
// qu'un rectangle. Chaque FlatControl décrit son rôle, sa valeur, son état et son action par défaut ; un
// changement visible (interrupteur basculé, autre choix, curseur déplacé) est annoncé.
abstract partial class FlatControl
{
    protected virtual AccessibleRole FlatRole { get { return AccessibleRole.Client; } }
    protected virtual string FlatValue { get { return null; } }
    protected virtual AccessibleStates FlatState { get { return AccessibleStates.None; } }
    protected virtual string FlatDefaultAction { get { return null; } }
    protected virtual void FlatDoDefault() { OnClick(EventArgs.Empty); }

    string announcedValue;
    AccessibleStates announcedState;

    protected override AccessibleObject CreateAccessibilityInstance() { return new FlatAccessible(this); }

    // Tout changement visible passe par Invalidate : c'est là qu'on compare avec ce qui a été annoncé
    protected override void OnInvalidated(InvalidateEventArgs e)
    {
        base.OnInvalidated(e);
        if (!IsHandleCreated) return;
        string v = FlatValue;
        AccessibleStates st = FlatState;
        if (v != announcedValue) { announcedValue = v; AccessibilityNotifyClients(AccessibleEvents.ValueChange, -1); }
        if (st != announcedState) { announcedState = st; AccessibilityNotifyClients(AccessibleEvents.StateChange, -1); }
    }

    internal sealed class FlatAccessible : ControlAccessibleObject
    {
        readonly FlatControl c;
        public FlatAccessible(FlatControl owner) : base(owner) { c = owner; }

        public override AccessibleRole Role { get { return c.AccessibleRole != AccessibleRole.Default ? c.AccessibleRole : c.FlatRole; } }
        public override string Name { get { return !string.IsNullOrEmpty(c.AccessibleName) ? c.AccessibleName : c.Text; } set { c.AccessibleName = value; } }
        public override string Value { get { return c.FlatValue ?? base.Value; } set { } }
        public override string DefaultAction { get { return c.FlatDefaultAction ?? base.DefaultAction; } }
        public override AccessibleStates State
        {
            get
            {
                AccessibleStates s = base.State | c.FlatState;
                if (!c.Enabled) s |= AccessibleStates.Unavailable;
                if (c.TabStop) s |= AccessibleStates.Focusable;
                if (c.Focused) s |= AccessibleStates.Focused;
                return s;
            }
        }
        public override void DoDefaultAction() { if (c.Enabled) c.FlatDoDefault(); }
    }
}
