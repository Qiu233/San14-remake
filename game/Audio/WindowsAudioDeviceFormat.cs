using System.Runtime.InteropServices;
using System.Runtime.Versioning;

/// <summary>Read-only format metadata for the same WASAPI device selection as
/// the host. No stream is initialized, started, rendered or owned here.</summary>
internal sealed record WindowsAudioDeviceFormat(int Channels,uint NativeChannelMask)
{
    internal static bool TryRead(string deviceName,out WindowsAudioDeviceFormat? format,out string? error)
    {
        format=null;error=null;
        if(!OperatingSystem.IsWindows()){error="audio.output.windows_device_format";return false;}
        try{format=ReadWindows(deviceName);return true;}
        catch(Exception failure) when(failure is COMException or InvalidDataException or NotSupportedException)
        {error="audio.output.device_format: "+failure.Message;return false;}
    }

    [SupportedOSPlatform("windows")]
    private static WindowsAudioDeviceFormat ReadWindows(string deviceName)
    {
        int initialized=CoInitializeEx(IntPtr.Zero,0);
        if(initialized<0&&initialized!=unchecked((int)0x80010106))Marshal.ThrowExceptionForHR(initialized);
        IMMDeviceEnumerator? enumerator=null;IMMDevice? selected=null;IAudioClient? client=null;
        IntPtr wave=IntPtr.Zero;
        try
        {
            var clsid=new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
            var iid=typeof(IMMDeviceEnumerator).GUID;
            Check(CoCreateInstance(ref clsid,IntPtr.Zero,23,ref iid,out enumerator));
            if(deviceName!="Default")
            {
                IMMDeviceCollection? devices=null;
                try
                {
                    Check(enumerator.EnumAudioEndpoints(0,1,out devices));Check(devices.GetCount(out uint count));
                    for(uint index=0;index<count&&selected is null;index++)
                    {
                        IMMDevice? device=null;IPropertyStore? properties=null;PropVariant name=default;
                        try
                        {
                            Check(devices.Item(index,out device));Check(device.OpenPropertyStore(0,out properties));
                            var key=new PropertyKey(new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"),14);
                            Check(properties.GetValue(ref key,out name));
                            if(name.Type==31&&Marshal.PtrToStringUni(name.Pointer)==deviceName)
                            {selected=device;device=null;}
                        }
                        finally{PropVariantClear(ref name);Release(properties);Release(device);}
                    }
                }
                finally{Release(devices);}
            }
            // The host uses the Console default when a requested name no
            // longer resolves. The first matching active friendly name wins.
            if(selected is null)Check(enumerator.GetDefaultAudioEndpoint(0,0,out selected));
            var audioClient=typeof(IAudioClient).GUID;
            Check(selected.Activate(ref audioClient,23,IntPtr.Zero,out client));
            Check(client.GetMixFormat(out wave));
            if(wave==IntPtr.Zero)throw new InvalidDataException("audio.output.device_wave_format");
            int channels=(ushort)Marshal.ReadInt16(wave,2);
            ushort tag=(ushort)Marshal.ReadInt16(wave,0),extension=(ushort)Marshal.ReadInt16(wave,16);
            uint mask=tag==0xFFFE&&extension>=22?unchecked((uint)Marshal.ReadInt32(wave,20)):0;
            return new(channels,mask);
        }
        finally
        {
            if(wave!=IntPtr.Zero)Marshal.FreeCoTaskMem(wave);
            Release(client);Release(selected);Release(enumerator);
            if(initialized>=0)CoUninitialize();
        }
    }
    private static void Check(int hr){if(hr<0)Marshal.ThrowExceptionForHR(hr);}
    [SupportedOSPlatform("windows")]
    private static void Release(object? value){if(value is not null)Marshal.ReleaseComObject(value);}
    [DllImport("ole32.dll",ExactSpelling=true)]private static extern int CoInitializeEx(IntPtr reserved,uint mode);
    [DllImport("ole32.dll",ExactSpelling=true)]private static extern void CoUninitialize();
    [DllImport("ole32.dll",ExactSpelling=true)]private static extern int CoCreateInstance(ref Guid clsid,IntPtr outer,uint context,ref Guid iid,[MarshalAs(UnmanagedType.Interface)]out IMMDeviceEnumerator result);
    [DllImport("ole32.dll",ExactSpelling=true)]private static extern int PropVariantClear(ref PropVariant value);
    [StructLayout(LayoutKind.Sequential)]private struct PropertyKey(Guid formatId,uint propertyId)
    {public Guid FormatId=formatId;public uint PropertyId=propertyId;}
    [StructLayout(LayoutKind.Explicit,Size=24)]private struct PropVariant
    {[FieldOffset(0)]public ushort Type;[FieldOffset(8)]public IntPtr Pointer;}
    [ComImport,Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig]int EnumAudioEndpoints(int flow,uint state,out IMMDeviceCollection devices);
        [PreserveSig]int GetDefaultAudioEndpoint(int flow,int role,out IMMDevice device);
        [PreserveSig]int GetDevice([MarshalAs(UnmanagedType.LPWStr)]string id,out IMMDevice device);
        [PreserveSig]int RegisterEndpointNotificationCallback(IntPtr client);
        [PreserveSig]int UnregisterEndpointNotificationCallback(IntPtr client);
    }
    [ComImport,Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection
    {[PreserveSig]int GetCount(out uint count);[PreserveSig]int Item(uint index,out IMMDevice device);}
    [ComImport,Guid("D666063F-1587-4E43-81F1-B948E807363F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]int Activate(ref Guid iid,uint context,IntPtr activationParameters,out IAudioClient client);
        [PreserveSig]int OpenPropertyStore(uint access,out IPropertyStore properties);
        [PreserveSig]int GetId(out IntPtr id);
        [PreserveSig]int GetState(out uint state);
    }
    [ComImport,Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig]int GetCount(out uint count);[PreserveSig]int GetAt(uint index,out PropertyKey key);
        [PreserveSig]int GetValue(ref PropertyKey key,out PropVariant value);
        [PreserveSig]int SetValue(ref PropertyKey key,ref PropVariant value);[PreserveSig]int Commit();
    }
    [ComImport,Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioClient
    {
        [PreserveSig]int Initialize(uint shareMode,uint flags,long duration,long periodicity,IntPtr format,IntPtr session);
        [PreserveSig]int GetBufferSize(out uint frames);[PreserveSig]int GetStreamLatency(out long latency);
        [PreserveSig]int GetCurrentPadding(out uint frames);
        [PreserveSig]int IsFormatSupported(uint mode,IntPtr format,out IntPtr closest);
        [PreserveSig]int GetMixFormat(out IntPtr format);
    }
}
