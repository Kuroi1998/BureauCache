// BureauCache — batterie et luminosité des écrans
using System;
using System.Management;
using System.Runtime.InteropServices;

static partial class Native
{
    [StructLayout(LayoutKind.Sequential)]
    struct QuickPowerStatus
    {
        public byte AcLine, BatteryFlag, Percent, Saver;
        public uint Remaining, FullLifetime;
    }

    [DllImport("kernel32.dll")]
    static extern bool GetSystemPowerStatus(out QuickPowerStatus status);

    internal static int? QuickBattery(out bool pluggedIn, out bool saver)
    {
        pluggedIn = saver = false;
        QuickPowerStatus status;
        if (Sandbox.Active || !GetSystemPowerStatus(out status)) return null;
        pluggedIn = status.AcLine == 1;
        saver = status.Saver == 1;
        return (status.BatteryFlag & 128) != 0 || status.Percent > 100 ? (int?)null : status.Percent;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct PhysicalMonitor
    {
        public IntPtr Handle;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
    }

    [DllImport("user32.dll")] static extern IntPtr MonitorFromWindow(IntPtr window, uint flags);
    [DllImport("dxva2.dll")] static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr monitor, out uint count);
    [DllImport("dxva2.dll")] static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr monitor, uint count, [Out] PhysicalMonitor[] monitors);
    [DllImport("dxva2.dll")] static extern bool DestroyPhysicalMonitors(uint count, [In] PhysicalMonitor[] monitors);
    [DllImport("dxva2.dll")] static extern bool GetMonitorCapabilities(IntPtr monitor, out uint capabilities, out uint temperatures);
    [DllImport("dxva2.dll")] static extern bool GetMonitorBrightness(IntPtr monitor, out uint min, out uint current, out uint max);
    [DllImport("dxva2.dll")] static extern bool SetMonitorBrightness(IntPtr monitor, uint brightness);

    internal static int? QuickBrightness(IntPtr owner, int? requested, bool? useInternal, out bool internalDisplay)
    {
        internalDisplay = false;
        if (Sandbox.Active) return null;
        try
        {
            if (useInternal != false)
            {
                int? level = InternalBrightness(requested, out internalDisplay);
                if (internalDisplay || requested.HasValue) return level;
            }
            return ExternalBrightness(owner, requested);
        }
        catch (EntryPointNotFoundException) { return null; }
        catch (DllNotFoundException) { return null; }
    }

    static int? InternalBrightness(int? requested, out bool found)
    {
        found = false;
        try
        {
            using (ManagementObjectSearcher query = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM WmiMonitorBrightness WHERE Active = TRUE"))
            using (ManagementObjectCollection screens = query.Get())
            {
                foreach (ManagementObject screen in screens)
                using (screen)
                {
                    int current = (byte)screen["CurrentBrightness"];
                    found = true;
                    if (!requested.HasValue) return current;
                    int target = NearestBrightnessLevel(requested.Value, screen["Level"] as byte[]);
                    if (current == target) return current;
                    if (!SetInternalBrightness((string)screen["InstanceName"], target)) return null;
                    screen.Get();
                    return (byte)screen["CurrentBrightness"];
                }
                return null;
            }
        }
        catch (ManagementException) { return null; }
        catch (COMException) { return null; }
        catch (UnauthorizedAccessException) { return null; }
    }

    internal static int NearestBrightnessLevel(int requested, byte[] levels)
    {
        if (levels == null || levels.Length == 0) return requested;
        int best = levels[0];
        foreach (byte level in levels)
        {
            int distance = Math.Abs(level - requested), bestDistance = Math.Abs(best - requested);
            if (distance < bestDistance || (distance == bestDistance && level < best)) best = level;
        }
        return best;
    }

    static bool SetInternalBrightness(string instance, int requested)
    {
        using (ManagementObjectSearcher query = new ManagementObjectSearcher(@"root\WMI", "SELECT * FROM WmiMonitorBrightnessMethods WHERE Active = TRUE"))
        using (ManagementObjectCollection methods = query.Get())
        {
            foreach (ManagementObject method in methods)
            using (method)
            {
                if (!string.Equals((string)method["InstanceName"], instance, StringComparison.OrdinalIgnoreCase)) continue;
                using (ManagementBaseObject args = method.GetMethodParameters("WmiSetBrightness"))
                {
                    args["Timeout"] = 0u;
                    args["Brightness"] = (byte)requested;
                    using (ManagementBaseObject result = method.InvokeMethod("WmiSetBrightness", args, null))
                        return (uint)result["ReturnValue"] == 0;
                }
            }
            return false;
        }
    }

    static int? ExternalBrightness(IntPtr monitor, int? requested)
    {
        uint count;
        if (monitor == IntPtr.Zero || !GetNumberOfPhysicalMonitorsFromHMONITOR(monitor, out count) || count != 1) return null;
        PhysicalMonitor[] displays = new PhysicalMonitor[count];
        if (!GetPhysicalMonitorsFromHMONITOR(monitor, count, displays)) return null;
        try
        {
            IntPtr display = displays[0].Handle;
            uint capabilities, temperatures, min, current, max;
            const uint Brightness = 2;
            if (!GetMonitorCapabilities(display, out capabilities, out temperatures) || (capabilities & Brightness) == 0) return null;
            if (!GetMonitorBrightness(display, out min, out current, out max) || min >= max || current < min || current > max) return null;
            if (requested.HasValue)
            {
                uint target = min + (uint)Math.Round((max - min) * (requested.Value / 100.0));
                if (target != current &&
                    (!SetMonitorBrightness(display, target) || !GetMonitorBrightness(display, out min, out current, out max))) return null;
            }
            return min < max && current >= min && current <= max ? (int?)Math.Round((current - min) * 100.0 / (max - min)) : null;
        }
        finally { DestroyPhysicalMonitors(count, displays); }
    }

    internal static IntPtr QuickMonitor(IntPtr owner)
    {
        return Sandbox.Active ? IntPtr.Zero : MonitorFromWindow(owner, 2 /* MONITOR_DEFAULTTONEAREST */);
    }
}
