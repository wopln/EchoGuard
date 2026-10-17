using System;
using System.Collections.Generic;

namespace EchoGuard
{
    internal static class AudioInventory
    {
        internal static List<EndpointInfo> Read(Native.Enumerator enumerator)
        {
            var result = new List<EndpointInfo>();
            Native.Collection devices;
            Native.Check(enumerator.EnumAudioEndpoints(2, 15, out devices));
            try
            {
                uint n;
                Native.Check(devices.GetCount(out n));
                for (uint i = 0; i < n; i++)
                {
                    Native.Device device;
                    Native.Check(devices.Item(i, out device));
                    try
                    {
                        string id;
                        int flow;
                        uint state;
                        Native.Check(device.GetId(out id));
                        Native.Check(((Native.Endpoint)device).GetDataFlow(out flow));
                        Native.Check(device.GetState(out state));
                        result.Add(new EndpointInfo { Id = id, Flow = flow, State = state, Name = Read(device, "a45c254e-df1c-4efd-8020-67d146a850e0", 14), Driver = Read(device, "a8b865dd-2e3d-4094-ad97-e593a70c75d6", 8), Role = Read(device, "f141e7cc-921b-466d-802f-879d6d4aec17", 1), Hardware = Read(device, "b3f8fa53-0004-438e-9003-51a46e139bfc", 2), Container = Read(device, "8c7ed206-3f8a-4827-b3ab-ae9e1faefc6c", 2) });
                        if (String.IsNullOrWhiteSpace(result[result.Count - 1].Name))
                            result[result.Count - 1].Name = "Unnamed audio endpoint";
                    }
                    finally
                    {
                        Native.Release(device);
                    }
                }
            }
            finally
            {
                Native.Release(devices);
            }

            for (int role = 0; role < 3; role++)
            {
                Native.Device d;
                int hr = enumerator.GetDefaultAudioEndpoint(1, role, out d);
                if (hr < 0)
                    continue;
                try
                {
                    string id;
                    Native.Check(d.GetId(out id));
                    foreach (var e in result)
                        if (e.Id == id)
                        {
                            if (role == 0)
                                e.DefaultConsole = true;
                            if (role == 1)
                                e.DefaultMultimedia = true;
                            if (role == 2)
                                e.DefaultCommunications = true;
                        }
                }
                finally
                {
                    Native.Release(d);
                }
            }

            return result;
        }

        static string Read(Native.Device d, string key, uint id)
        {
            try
            {
                return Native.Property(d, key, id);
            }
            catch
            {
                return "";
            }
        }
    }
}
