// BureauCache — Native : session et alimentation pour le menu Démarrer (D-013, T-034)
using System;
using System.Runtime.InteropServices;
using System.Text;

static partial class Native
{
    [StructLayout(LayoutKind.Sequential)] struct LUID { public uint Low; public int High; }
    [StructLayout(LayoutKind.Sequential)] struct TOKEN_PRIVILEGES { public uint Count; public LUID Luid; public uint Attributes; }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode)] static extern uint InitiateShutdown(string machine, string message, uint grace, uint flags, uint reason);
    [DllImport("advapi32.dll", SetLastError = true)] static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool LookupPrivilegeValue(string system, string name, out LUID luid);
    [DllImport("advapi32.dll", SetLastError = true)] static extern bool AdjustTokenPrivileges(IntPtr token, bool disableAll, ref TOKEN_PRIVILEGES state, uint length, IntPtr previous, IntPtr returned);
    [DllImport("kernel32.dll")] static extern IntPtr GetCurrentProcess();
    [DllImport("secur32.dll", CharSet = CharSet.Unicode)] static extern bool GetUserNameEx(int format, StringBuilder name, ref uint size);
    [DllImport("netapi32.dll", CharSet = CharSet.Unicode)] static extern int NetUserGetInfo(string server, string user, int level, out IntPtr buffer);
    [DllImport("netapi32.dll")] static extern int NetApiBufferFree(IntPtr buffer);

    [DllImport("powrprof.dll")] [return: MarshalAs(UnmanagedType.U1)] static extern bool GetPwrCapabilities(IntPtr capabilities);   // BOOLEAN : 1 octet

    // Veille moderne (S0, « AoAc ») : octet 20 de SYSTEM_POWER_CAPABILITIES (CDX-11). Faux si inconnu.
    public static bool ModernStandby()
    {
        IntPtr buf = Marshal.AllocHGlobal(256);
        try
        {
            for (int i = 0; i < 256; i++) Marshal.WriteByte(buf, i, 0);
            return GetPwrCapabilities(buf) && Marshal.ReadByte(buf, 20) != 0;
        }
        catch { return false; }
        finally { Marshal.FreeHGlobal(buf); }
    }

    public const uint SHUTDOWN_RESTART = 0x4, SHUTDOWN_POWEROFF = 0x8, SHUTDOWN_INSTALL_UPDATES = 0x40, SHUTDOWN_HYBRID = 0x200;

    // Arrêt ou redémarrage demandé comme le menu Démarrer : les applications ouvertes peuvent proposer d'enregistrer.
    // Le droit d'arrêter le PC appartient déjà à l'utilisateur ; il faut seulement l'activer pour ce processus.
    public static uint Shutdown(uint flags)
    {
        IntPtr token;
        if (OpenProcessToken(GetCurrentProcess(), 0x20 /*TOKEN_ADJUST_PRIVILEGES*/ | 0x8 /*TOKEN_QUERY*/, out token))
        {
            try
            {
                TOKEN_PRIVILEGES tp = new TOKEN_PRIVILEGES { Count = 1, Attributes = 0x2 /*SE_PRIVILEGE_ENABLED*/ };
                if (LookupPrivilegeValue(null, "SeShutdownPrivilege", out tp.Luid))
                    AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
            }
            finally { CloseHandle(token); }
        }
        // Raison : autre, planifié (même valeur que l'arrêt depuis le menu Démarrer)
        return InitiateShutdown(null, null, 0, flags, 0x80000000 /*SHTDN_REASON_FLAG_PLANNED*/);
    }

    // Nom affiché de l'utilisateur (« Prénom Nom »), sinon son nom de compte
    public static string UserDisplayName()
    {
        try
        {
            StringBuilder sb = new StringBuilder(256);
            uint size = (uint)sb.Capacity;
            if (GetUserNameEx(3 /*NameDisplay*/, sb, ref size) && sb.Length > 0) return sb.ToString();
        }
        catch { }
        try
        {
            IntPtr buf;
            if (NetUserGetInfo(null, Environment.UserName, 10, out buf) == 0 && buf != IntPtr.Zero)
            {
                try
                {
                    // USER_INFO_10 : nom, commentaire, commentaire de l'utilisateur, nom complet
                    string full = Marshal.PtrToStringUni(Marshal.ReadIntPtr(buf, 3 * IntPtr.Size));
                    if (!string.IsNullOrEmpty(full)) return full;
                }
                finally { NetApiBufferFree(buf); }
            }
        }
        catch { }
        return Environment.UserName;
    }
}
