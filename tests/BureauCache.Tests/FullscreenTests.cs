using System.Drawing;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BureauCache.Tests
{
    /// <summary>
    /// Détection du plein écran (option « Pas en plein écran »). Bug corrigé : sur le bureau, Windows répond « occupé »
    /// (état de notification 2), et le bureau était pris pour un jeu en plein écran : le panneau ne s'ouvrait plus.
    /// </summary>
    [TestClass]
    public class FullscreenTests
    {
        private static readonly Rectangle Screen = new Rectangle(0, 0, 2560, 1440);
        private static readonly Rectangle Full = new Rectangle(0, 0, 2560, 1440);
        private static readonly Rectangle Maximized = Rectangle.FromLTRB(-9, -9, 2569, 1389);   // fenêtre agrandie, barre des tâches visible
        private static readonly Rectangle Windowed = new Rectangle(200, 100, 1200, 800);

        private const int Busy = 2, Direct3D = 3, Presentation = 4, Normal = 5;

        [TestMethod]
        public void Bureau_au_premier_plan_n_est_pas_du_plein_ecran_meme_si_Windows_repond_occupe()
        {
            // Mesuré sur Windows 11 (build 26300) : Progman au premier plan, état de notification 2
            Assert.IsFalse(Native.IsFullscreen(true, "Progman", true, Busy, Full, Screen));
            Assert.IsFalse(Native.IsFullscreen(true, "Progman", false, Busy, Full, Screen));
            Assert.IsFalse(Native.IsFullscreen(true, "WorkerW", false, Busy, Full, Screen));
        }

        [TestMethod]
        public void Fenetre_du_shell_reconnue_par_son_handle()
        {
            Assert.IsFalse(Native.IsFullscreen(true, "UneAutreClasse", true, Busy, Full, Screen));
        }

        [TestMethod]
        [DataRow("Shell_TrayWnd")]
        [DataRow("Shell_SecondaryTrayWnd")]
        public void Barre_des_taches_n_est_pas_du_plein_ecran(string cls)
        {
            Assert.IsFalse(Native.IsFullscreen(true, cls, false, Busy, Full, Screen));
        }

        [TestMethod]
        [DataRow(Busy)]
        [DataRow(Direct3D)]
        [DataRow(Presentation)]
        public void Jeu_ou_presentation_reste_du_plein_ecran(int state)
        {
            Assert.IsTrue(Native.IsFullscreen(true, "UnityWndClass", false, state, Windowed, Screen));
        }

        [TestMethod]
        public void Fenetre_qui_couvre_tout_l_ecran_est_du_plein_ecran()
        {
            Assert.IsTrue(Native.IsFullscreen(true, "Chrome_WidgetWin_1", false, Normal, Full, Screen));
        }

        [TestMethod]
        public void Fenetre_agrandie_ou_normale_n_est_pas_du_plein_ecran()
        {
            Assert.IsFalse(Native.IsFullscreen(true, "Chrome_WidgetWin_1", false, Normal, Maximized, Screen));
            Assert.IsFalse(Native.IsFullscreen(true, "Notepad", false, Normal, Windowed, Screen));
        }

        [TestMethod]
        public void Sans_fenetre_au_premier_plan_pas_de_plein_ecran()
        {
            Assert.IsFalse(Native.IsFullscreen(false, "", false, Busy, Rectangle.Empty, Rectangle.Empty));
            Assert.IsFalse(Native.IsFullscreen(true, "X", false, Normal, Rectangle.Empty, Screen));
        }
    }
}
