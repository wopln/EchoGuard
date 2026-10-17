using System;
using System.Runtime.InteropServices;

namespace EchoGuard
{
    public static class Native
    {
        internal static void Check(int hr)
        {
            if (hr < 0)
                Marshal.ThrowExceptionForHR(hr);
        }

        internal static void Release(object o)
        {
            if (o != null && Marshal.IsComObject(o))
                Marshal.ReleaseComObject(o);
        }

        internal static readonly Guid SessionManagerId = new Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");
        [DllImport("ole32.dll")]
        internal static extern int CoInitializeEx(IntPtr p, uint f);
        [DllImport("ole32.dll")]
        internal static extern void CoUninitialize();
        [DllImport("ole32.dll")]
        internal static extern int PropVariantClear(IntPtr p);
        internal static Enumerator CreateEnumerator()
        {
            return (Enumerator)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")));
        }

        internal static string Property(Device d, string format, uint pid)
        {
            Store s;
            Check(d.OpenPropertyStore(0, out s));
            IntPtr pv = Marshal.AllocCoTaskMem(32);
            try
            {
                for (int i = 0; i < 32; i++)
                    Marshal.WriteByte(pv, i, 0);
                Key k = new Key
                {
                    Format = new Guid(format),
                    Id = pid
                };
                Check(s.GetValue(ref k, pv));
                int vt = Marshal.ReadInt16(pv);
                if (vt == 72)
                {
                    IntPtr guid = Marshal.ReadIntPtr(pv, 8);
                    return guid == IntPtr.Zero ? "" : ((Guid)Marshal.PtrToStructure(guid, typeof(Guid))).ToString("D");
                }

                if (vt == 31)
                    return Marshal.PtrToStringUni(Marshal.ReadIntPtr(pv, 8)) ?? "";
                return "";
            }
            finally
            {
                PropVariantClear(pv);
                Marshal.FreeCoTaskMem(pv);
                Release(s);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Key
        {
            public Guid Format;
            public uint Id;
        }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface Enumerator
        {
            [PreserveSig]
            int EnumAudioEndpoints(int flow, uint states, out Collection c);
            [PreserveSig]
            int GetDefaultAudioEndpoint(int flow, int role, out Device d);
            [PreserveSig]
            int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out Device d);
            [PreserveSig]
            int RegisterEndpointNotificationCallback(DeviceSink sink);
            [PreserveSig]
            int UnregisterEndpointNotificationCallback(DeviceSink sink);
        }

        [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface Collection
        {
            [PreserveSig]
            int GetCount(out uint n);
            [PreserveSig]
            int Item(uint i, out Device d);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface Device
        {
            [PreserveSig]
            int Activate(ref Guid iid, uint ctx, IntPtr p, [MarshalAs(UnmanagedType.IUnknown)] out object o);
            [PreserveSig]
            int OpenPropertyStore(uint mode, out Store s);
            [PreserveSig]
            int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
            [PreserveSig]
            int GetState(out uint state);
        }

        [ComImport, Guid("1BE09788-6894-4089-8586-9A2A6C265AC5"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface Endpoint
        {
            [PreserveSig]
            int GetDataFlow(out int flow);
        }

        [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface Store
        {
            [PreserveSig]
            int GetCount(out uint n);
            [PreserveSig]
            int GetAt(uint i, out Key key);
            [PreserveSig]
            int GetValue(ref Key key, IntPtr pv);
        }

        [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface Manager
        {
            [PreserveSig]
            int GetAudioSessionControl(IntPtr id, uint f, out Control c);
            [PreserveSig]
            int GetSimpleAudioVolume(IntPtr id, uint f, out Volume v);
            [PreserveSig]
            int GetSessionEnumerator(out SessionList list);
            [PreserveSig]
            int RegisterSessionNotification(CreatedSink c);
            [PreserveSig]
            int UnregisterSessionNotification(CreatedSink c);
        }

        [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface SessionList
        {
            [PreserveSig]
            int GetCount(out int n);
            [PreserveSig]
            int GetSession(int i, out Control c);
        }

        [ComImport, Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface Control
        {
            [PreserveSig]
            int GetState(out int s);
            [PreserveSig]
            int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string s);
            [PreserveSig]
            int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string s, IntPtr c);
            [PreserveSig]
            int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string s);
            [PreserveSig]
            int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string s, IntPtr c);
            [PreserveSig]
            int GetGroupingParam(out Guid g);
            [PreserveSig]
            int SetGroupingParam(ref Guid g, IntPtr c);
            [PreserveSig]
            int RegisterAudioSessionNotification(EventSink n);
            [PreserveSig]
            int UnregisterAudioSessionNotification(EventSink n);
        }

        [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface Control2
        {
            [PreserveSig]
            int GetState(out int s);
            [PreserveSig]
            int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string s);
            [PreserveSig]
            int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string s, IntPtr c);
            [PreserveSig]
            int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string s);
            [PreserveSig]
            int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string s, IntPtr c);
            [PreserveSig]
            int GetGroupingParam(out Guid g);
            [PreserveSig]
            int SetGroupingParam(ref Guid g, IntPtr c);
            [PreserveSig]
            int RegisterAudioSessionNotification(EventSink n);
            [PreserveSig]
            int UnregisterAudioSessionNotification(EventSink n);
            [PreserveSig]
            int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string s);
            [PreserveSig]
            int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string s);
            [PreserveSig]
            int GetProcessId(out uint pid);
        }

        [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        internal interface Volume
        {
            [PreserveSig]
            int SetMasterVolume(float v, ref Guid c);
            [PreserveSig]
            int GetMasterVolume(out float v);
            [PreserveSig]
            int SetMute([MarshalAs(UnmanagedType.Bool)] bool v, ref Guid c);
            [PreserveSig]
            int GetMute([MarshalAs(UnmanagedType.Bool)] out bool v);
        }

        [ComVisible(true), Guid("641DD20B-4D41-49CC-ABA3-174B9477BB08"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface CreatedSink
        {
            [PreserveSig]
            int OnSessionCreated(Control c);
        }

        [ComVisible(true), Guid("24918ACC-64B3-37C1-8CA9-74A66E9957A8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface EventSink
        {
            [PreserveSig]
            int OnDisplayNameChanged(IntPtr n, IntPtr c);
            [PreserveSig]
            int OnIconPathChanged(IntPtr n, IntPtr c);
            [PreserveSig]
            int OnSimpleVolumeChanged(float v, [MarshalAs(UnmanagedType.Bool)] bool m, IntPtr c);
            [PreserveSig]
            int OnChannelVolumeChanged(uint n, IntPtr v, uint ch, IntPtr c);
            [PreserveSig]
            int OnGroupingParamChanged(IntPtr g, IntPtr c);
            [PreserveSig]
            int OnStateChanged(int s);
            [PreserveSig]
            int OnSessionDisconnected(int r);
        }

        [ComVisible(true), Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        public interface DeviceSink
        {
            [PreserveSig]
            int OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string id, uint s);
            [PreserveSig]
            int OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string id);
            [PreserveSig]
            int OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string id);
            [PreserveSig]
            int OnDefaultDeviceChanged(int f, int r, [MarshalAs(UnmanagedType.LPWStr)] string id);
            [PreserveSig]
            int OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string id, Key k);
        }
    }
}
