// BureauCache — découverte des appareils Bluetooth Classic et Low Energy.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

static partial class Native
{
    const string DeviceRuntime = ", Windows.Devices, ContentType=WindowsRuntime";
    const string DeviceNamespace = "Windows.Devices.Enumeration.";
    const string BluetoothConnectedProperty = "System.Devices.Aep.IsConnected";

    [ComImport, Guid("3E68D4BD-7135-4D10-8018-9FB6D9F33FA1"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPairingWindow
    {
        void Initialize(IntPtr owner);
    }

    internal sealed class QuickBluetoothDevice
    {
        public string Id, Name;
        public bool Connected, Paired, CanPair;
        internal object Pairing;
        internal Type PairingType;
    }

    internal static bool ReadQuickBluetooth(List<QuickBluetoothDevice> devices, CancellationToken cancellation)
    {
        if (Sandbox.Active || cancellation.IsCancellationRequested) return false;
        Type info = Type.GetType(DeviceNamespace + "DeviceInformation" + DeviceRuntime, false);
        Type kind = Type.GetType(DeviceNamespace + "DeviceInformationKind" + DeviceRuntime, false);
        if (info == null || kind == null || WinRtTypes.AsyncInfo == null) return false;
        MethodInfo find = info.GetMethod("FindAllAsync", new[] { typeof(string), typeof(IEnumerable<string>), kind });
        if (find == null) return false;
        const string selector = "(System.Devices.Aep.ProtocolId:=\"{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}\" OR " +
            "System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\")";
        object[] args = { selector, new[] { BluetoothConnectedProperty }, Enum.Parse(kind, "AssociationEndpoint") };
        IEnumerable found = WinRtResult(find, null, args, 33000, cancellation) as IEnumerable;
        if (found == null) return false;
        PropertyInfo pairingProperty = info.GetProperty("Pairing");
        Type pairingType = pairingProperty.PropertyType;
        foreach (object device in found)
        {
            if (cancellation.IsCancellationRequested) return false;
            object pairing = pairingProperty.GetValue(device, null);
            if (pairing == null) continue;
            var properties = info.GetProperty("Properties").GetValue(device, null) as IReadOnlyDictionary<string, object>;
            object connected;
            devices.Add(new QuickBluetoothDevice
            {
                Id = (string)info.GetProperty("Id").GetValue(device, null),
                Name = (string)info.GetProperty("Name").GetValue(device, null),
                Connected = properties != null && properties.TryGetValue(BluetoothConnectedProperty, out connected) && Equals(connected, true),
                Paired = (bool)pairingType.GetProperty("IsPaired").GetValue(pairing, null),
                CanPair = (bool)pairingType.GetProperty("CanPair").GetValue(pairing, null),
                Pairing = pairing,
                PairingType = pairingType
            });
        }
        return true;
    }

    internal static bool PairQuickBluetooth(QuickBluetoothDevice device, IntPtr owner, CancellationToken cancellation)
    {
        if (Sandbox.Active || cancellation.IsCancellationRequested || !device.CanPair) return false;
        IPairingWindow window = device.Pairing as IPairingWindow;
        // Sans fenêtre propriétaire, Windows ne peut pas afficher sa confirmation : on renvoie aux paramètres (CDX-43)
        if (window == null || owner == IntPtr.Zero) return false;
        window.Initialize(owner);
        MethodInfo pair = device.PairingType.GetMethod("PairAsync", Type.EmptyTypes);
        object result = WinRtResult(pair, device.Pairing, null, 60000, cancellation);
        if (result == null) return false;
        string status = pair.ReturnType.GetGenericArguments()[0].GetProperty("Status").GetValue(result, null).ToString();
        return status == "Paired" || status == "AlreadyPaired";
    }
}
