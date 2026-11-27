// BureauCache — découverte et connexion Wi-Fi avec Native Wi-Fi.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Xml;

static partial class Native
{
    const uint WifiConnected = 1, WifiHasProfile = 2, WifiAccessDenied = 5;
    const int WifiOpen = 1, WifiWpa2Personal = 7, WifiWpa3Personal = 9, WifiAes = 4;
    internal enum QuickWifiStatus { Ready, NoAdapter, AccessDenied, Unavailable }

    [StructLayout(LayoutKind.Sequential)]
    struct WifiSsid
    {
        public uint Length;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] Bytes;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct WifiInterface
    {
        public Guid Id;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Description;
        public int State;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct WifiAvailable
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string Profile;
        public WifiSsid Ssid;
        public int BssType;
        public uint BssidCount, Connectable, Reason, PhyCount;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public int[] PhyTypes;
        public uint MorePhyTypes, Signal, Secured;
        public int Auth, Cipher;
        public uint Flags, Reserved;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct WifiConnection
    {
        public int Mode;
        [MarshalAs(UnmanagedType.LPWStr)] public string Profile;
        public IntPtr Ssid, DesiredBssids;
        public int BssType;
        public uint Flags;
    }

    internal sealed class QuickWifiNetwork
    {
        public Guid Adapter;
        public string Name, Profile;
        public byte[] Ssid;
        public int BssType, Auth, Cipher;
        public uint Signal;
        public bool Connected, Connectable, Secured;
        public bool NeedsPassword { get { return Profile == null && Cipher == WifiAes && (Auth == WifiWpa2Personal || Auth == WifiWpa3Personal); } }
        public bool CanJoin { get { return Connectable && (Profile != null || NeedsPassword || (!Secured && Auth == WifiOpen)); } }
    }

    [DllImport("wlanapi.dll")] static extern uint WlanOpenHandle(uint version, IntPtr reserved, out uint negotiated, out IntPtr client);
    [DllImport("wlanapi.dll")] static extern uint WlanCloseHandle(IntPtr client, IntPtr reserved);
    [DllImport("wlanapi.dll")] static extern void WlanFreeMemory(IntPtr memory);
    [DllImport("wlanapi.dll")] static extern uint WlanEnumInterfaces(IntPtr client, IntPtr reserved, out IntPtr interfaces);
    [DllImport("wlanapi.dll")] static extern uint WlanGetAvailableNetworkList(IntPtr client, ref Guid adapter, uint flags, IntPtr reserved, out IntPtr networks);
    [DllImport("wlanapi.dll")] static extern uint WlanScan(IntPtr client, ref Guid adapter, IntPtr ssid, IntPtr data, IntPtr reserved);
    [DllImport("wlanapi.dll")] static extern uint WlanConnect(IntPtr client, ref Guid adapter, ref WifiConnection connection, IntPtr reserved);
    [DllImport("wlanapi.dll")] static extern uint WlanDisconnect(IntPtr client, ref Guid adapter, IntPtr reserved);

    internal static QuickWifiStatus ReadQuickWifi(bool scan, List<QuickWifiNetwork> networks, CancellationToken cancellation)
    {
        uint version;
        IntPtr client;
        if (Sandbox.Active || WlanOpenHandle(2, IntPtr.Zero, out version, out client) != 0) return QuickWifiStatus.Unavailable;
        try
        {
            IntPtr data;
            if (WlanEnumInterfaces(client, IntPtr.Zero, out data) != 0) return QuickWifiStatus.Unavailable;
            List<Guid> adapters = new List<Guid>();
            try
            {
                int count = Marshal.ReadInt32(data);
                int size = Marshal.SizeOf(typeof(WifiInterface));
                for (int i = 0; i < count && i < 128; i++)
                    adapters.Add(((WifiInterface)Marshal.PtrToStructure(IntPtr.Add(data, 8 + i * size), typeof(WifiInterface))).Id);
            }
            finally { WlanFreeMemory(data); }
            if (adapters.Count == 0) return QuickWifiStatus.NoAdapter;
            if (scan)
            {
                bool started = false;
                foreach (Guid id in adapters)
                {
                    Guid adapter = id;
                    if (WlanScan(client, ref adapter, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero) == 0) started = true;
                }
                if (started && cancellation.WaitHandle.WaitOne(4000)) return QuickWifiStatus.Unavailable;
            }
            bool read = false, denied = false;
            foreach (Guid adapter in adapters)
            {
                if (cancellation.IsCancellationRequested) break;
                uint error = ReadWifiAdapter(client, adapter, networks);
                read |= error == 0;
                denied |= error == WifiAccessDenied;
            }
            return read ? QuickWifiStatus.Ready : denied ? QuickWifiStatus.AccessDenied : QuickWifiStatus.Unavailable;
        }
        finally { WlanCloseHandle(client, IntPtr.Zero); }
    }

    static uint ReadWifiAdapter(IntPtr client, Guid adapter, List<QuickWifiNetwork> networks)
    {
        IntPtr data;
        uint error = WlanGetAvailableNetworkList(client, ref adapter, 0, IntPtr.Zero, out data);
        if (error != 0) return error;
        try
        {
            int count = Marshal.ReadInt32(data), size = Marshal.SizeOf(typeof(WifiAvailable));
            for (int i = 0; i < count && i < 8192; i++)
            {
                WifiAvailable entry = (WifiAvailable)Marshal.PtrToStructure(IntPtr.Add(data, 8 + i * size), typeof(WifiAvailable));
                if (entry.Ssid.Length == 0 || entry.Ssid.Length > 32) continue;
                byte[] ssid = new byte[entry.Ssid.Length];
                Array.Copy(entry.Ssid.Bytes, ssid, ssid.Length);
                string name = new string(Encoding.UTF8.GetString(ssid).Select(c => char.IsControl(c) ? '�' : c).ToArray());
                networks.Add(new QuickWifiNetwork
                {
                    Name = name, Ssid = ssid, Adapter = adapter, BssType = entry.BssType, Auth = entry.Auth, Cipher = entry.Cipher, Signal = entry.Signal,
                    Connected = (entry.Flags & WifiConnected) != 0, Connectable = entry.Connectable != 0, Secured = entry.Secured != 0,
                    Profile = (entry.Flags & WifiHasProfile) != 0 && !string.IsNullOrEmpty(entry.Profile) ? entry.Profile : null
                });
            }
            return 0;
        }
        finally { WlanFreeMemory(data); }
    }

    internal static bool ChangeQuickWifi(QuickWifiNetwork network, string password, CancellationToken cancellation)
    {
        uint version;
        IntPtr client;
        if (Sandbox.Active || cancellation.IsCancellationRequested || WlanOpenHandle(2, IntPtr.Zero, out version, out client) != 0) return false;
        Guid adapter = network.Adapter;
        IntPtr ssid = IntPtr.Zero;
        try
        {
            if (network.Connected)
            {
                bool connected;
                if (!ReadWifiConnection(client, network, out connected) || !connected) return false;
                if (WlanDisconnect(client, ref adapter, IntPtr.Zero) != 0) return false;
            }
            else
            {
                if (!network.CanJoin) return false;
                WifiConnection connection = new WifiConnection { BssType = network.BssType, Profile = network.Profile };
                if (network.NeedsPassword)
                {
                    if (!ValidQuickWifiPassword(password, network.Auth)) return false;
                    connection.Mode = 1; // wlan_connection_mode_temporary_profile
                    connection.Profile = QuickWifiProfile(network, password);
                }
                else if (connection.Profile == null)
                {
                    byte[] bytes = new byte[32];
                    network.Ssid.CopyTo(bytes, 0);
                    WifiSsid target = new WifiSsid { Length = (uint)network.Ssid.Length, Bytes = bytes };
                    ssid = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(WifiSsid)));
                    Marshal.StructureToPtr(target, ssid, false);
                    connection.Mode = 3; // wlan_connection_mode_discovery_unsecure
                    connection.Ssid = ssid;
                }
                if (WlanConnect(client, ref adapter, ref connection, IntPtr.Zero) != 0) return false;
            }
            Stopwatch wait = Stopwatch.StartNew();
            do
            {
                bool connected;
                if (!ReadWifiConnection(client, network, out connected)) return false;
                if (connected != network.Connected) return true;
            }
            while (wait.ElapsedMilliseconds < 12000 && !cancellation.WaitHandle.WaitOne(450));
            return false;
        }
        finally
        {
            if (ssid != IntPtr.Zero) Marshal.FreeHGlobal(ssid);
            WlanCloseHandle(client, IntPtr.Zero);
        }
    }

    static bool ReadWifiConnection(IntPtr client, QuickWifiNetwork network, out bool connected)
    {
        List<QuickWifiNetwork> current = new List<QuickWifiNetwork>();
        uint error = ReadWifiAdapter(client, network.Adapter, current);
        connected = current.Any(row => row.Connected && row.BssType == network.BssType && row.Ssid.SequenceEqual(network.Ssid));
        return error == 0;
    }

    internal static bool ValidQuickWifiPassword(string password)
    {
        return password != null && password.Length >= 8 && password.Length <= 63 && password.All(c => c >= 32 && c <= 126);
    }

    // WPA2 accepte aussi la clé brute : 64 chiffres hexadécimaux, écrits en « networkKey ». WPA3 (SAE) ne connaît
    // que la phrase de passe (CDX-49).
    internal static bool ValidQuickWifiPassword(string password, int auth)
    {
        return ValidQuickWifiPassword(password) || (auth != WifiWpa3Personal && IsRawWifiKey(password));
    }

    internal static bool IsRawWifiKey(string password)
    {
        return password != null && password.Length == 64 && password.All(Uri.IsHexDigit);
    }

    internal static string QuickWifiProfile(QuickWifiNetwork network, string password)
    {
        const string ns = "http://www.microsoft.com/networking/WLAN/profile/v1";
        string hex = BitConverter.ToString(network.Ssid).Replace("-", "");
        StringBuilder xml = new StringBuilder();
        using (XmlWriter w = XmlWriter.Create(xml, new XmlWriterSettings { OmitXmlDeclaration = true }))
        {
            w.WriteStartElement("WLANProfile", ns);
            w.WriteElementString("name", ns, hex);
            w.WriteStartElement("SSIDConfig", ns);
            w.WriteStartElement("SSID", ns);
            w.WriteElementString("hex", ns, hex);
            w.WriteEndElement();
            w.WriteEndElement();
            w.WriteElementString("connectionType", ns, "ESS");
            w.WriteElementString("connectionMode", ns, "manual");
            w.WriteStartElement("MSM", ns);
            w.WriteStartElement("security", ns);
            w.WriteStartElement("authEncryption", ns);
            w.WriteElementString("authentication", ns, network.Auth == WifiWpa3Personal ? "WPA3SAE" : "WPA2PSK");
            w.WriteElementString("encryption", ns, "AES");
            w.WriteElementString("useOneX", ns, "false");
            w.WriteEndElement();
            w.WriteStartElement("sharedKey", ns);
            w.WriteElementString("keyType", ns, network.Auth != WifiWpa3Personal && IsRawWifiKey(password) ? "networkKey" : "passPhrase");
            w.WriteElementString("protected", ns, "false");
            w.WriteElementString("keyMaterial", ns, password);
            w.WriteEndElement();
            w.WriteEndElement();
            w.WriteEndElement();
            w.WriteEndElement();
        }
        return xml.ToString();
    }
}
