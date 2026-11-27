// BureauCache — volume de la sortie audio par défaut (Core Audio)
using System;
using System.Runtime.InteropServices;

static partial class Native
{
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    class AudioDeviceEnumerator { }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int flow, uint mask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IAudioDevice device);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioDevice
    {
        [PreserveSig] int Activate(ref Guid iid, uint context, IntPtr parameters, [MarshalAs(UnmanagedType.IUnknown)] out object endpoint);
    }

    // Ordre des méthodes dans endpointvolume.h, jusqu'au dernier membre utilisé.
    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr callback);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr callback);
        [PreserveSig] int GetChannelCount(out uint count);
        [PreserveSig] int SetMasterVolumeLevel(float level, IntPtr context);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, IntPtr context);
        [PreserveSig] int GetMasterVolumeLevel(out float level);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint channel, float level, IntPtr context);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, IntPtr context);
        [PreserveSig] int GetChannelVolumeLevel(uint channel, out float level);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool muted, IntPtr context);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool muted);
    }

    internal static int? QuickVolume(int? requested, bool toggleMute, out bool muted)
    {
        muted = false;
        if (Sandbox.Active) return null;
        object enumerator = null, endpoint = null;
        IAudioDevice device = null;
        try
        {
            enumerator = new AudioDeviceEnumerator();
            const int Render = 0, Multimedia = 1;
            const uint InProcess = 1;
            if (((IAudioDeviceEnumerator)enumerator).GetDefaultAudioEndpoint(Render, Multimedia, out device) < 0) return null;
            Guid iid = typeof(IAudioEndpointVolume).GUID;
            if (device.Activate(ref iid, InProcess, IntPtr.Zero, out endpoint) < 0) return null;
            IAudioEndpointVolume volume = (IAudioEndpointVolume)endpoint;
            if (requested.HasValue && volume.SetMasterVolumeLevelScalar(requested.Value / 100f, IntPtr.Zero) < 0) return null;
            if (volume.GetMute(out muted) < 0) return null;
            if (toggleMute)
            {
                if (volume.SetMute(!muted, IntPtr.Zero) < 0) return null;
                muted = !muted;
            }
            float scalar;
            return volume.GetMasterVolumeLevelScalar(out scalar) < 0 ? (int?)null : (int)Math.Round(scalar * 100);
        }
        catch (COMException) { return null; }
        finally
        {
            if (endpoint != null) Marshal.ReleaseComObject(endpoint);
            if (device != null) Marshal.ReleaseComObject(device);
            if (enumerator != null) Marshal.ReleaseComObject(enumerator);
        }
    }
}
