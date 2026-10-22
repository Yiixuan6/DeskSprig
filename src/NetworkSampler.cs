using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace DesktopWidgets
{
    public sealed class NetworkCounter
    {
        public ulong Id, Received, Sent;
        public bool Eligible;
    }
    public sealed class NetworkReading
    {
        public string State;
        public double Upload, Download;
        public int Interfaces;
    }
    public sealed class NetworkSampler
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct Row
        {
            public ulong Luid; public uint Index; public Guid Guid;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)] public string Alias;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 257)] public string Description;
            public uint PhysicalLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] Physical;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)] public byte[] Permanent;
            public uint Mtu, Type, Tunnel, Media, PhysicalMedia, Access, Direction;
            public byte Flags;
            public uint OperStatus, AdminStatus, ConnectState;
            public Guid NetworkGuid;
            public uint ConnectionType;
            public ulong TxSpeed, RxSpeed, InOctets, InUcastPkts, InNUcastPkts, InDiscards, InErrors, InUnknown, InUcastOctets, InMulticastOctets, InBroadcastOctets;
            public ulong OutOctets, OutUcastPkts, OutNUcastPkts, OutDiscards, OutErrors, OutUcastOctets, OutMulticastOctets, OutBroadcastOctets, OutQLen;
        }
        [DllImport("iphlpapi.dll")] static extern uint GetIfTable2(out IntPtr table);
        [DllImport("iphlpapi.dll")] static extern void FreeMibTable(IntPtr table);
        Dictionary<ulong, NetworkCounter> previous = new Dictionary<ulong, NetworkCounter>();
        double lastTime;
        bool baseline;
        public int Samples { get; private set; }
        public static int NativeRowSize { get { return Marshal.SizeOf(typeof(Row)); } }
        public static bool Eligible(uint type, uint tunnel, byte flags, uint oper, uint connection)
        { return (type == 6 || type == 71) && tunnel == 0 && (flags & 1) != 0 && (flags & 0xFA) == 0 && oper == 1 && connection == 1; }
        public static List<NetworkCounter> ReadCounters()
        {
            IntPtr table = IntPtr.Zero;
            try
            {
                uint hr = GetIfTable2(out table); if (hr != 0) throw new System.ComponentModel.Win32Exception((int)hr);
                int count = Marshal.ReadInt32(table); if (count < 0 || count > 100000) throw new InvalidOperationException("网卡表数量无效。");
                var result = new List<NetworkCounter>(); int size = NativeRowSize;
                for (int i = 0; i < count; i++)
                {
                    var row = (Row)Marshal.PtrToStructure(IntPtr.Add(table, 8 + i * size), typeof(Row));
                    result.Add(new NetworkCounter { Id = row.Luid, Received = row.InOctets, Sent = row.OutOctets, Eligible = Eligible(row.Type, row.Tunnel, row.Flags, row.OperStatus, row.ConnectState) });
                }
                return result;
            }
            finally { if (table != IntPtr.Zero) FreeMibTable(table); }
        }
        public void Reset() { previous.Clear(); baseline = false; }
        public NetworkReading Sample()
        {
            Samples++;
            try { return Update(ReadCounters(), (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency); }
            catch { Reset(); return new NetworkReading { State = "暂不可用" }; }
        }
        public NetworkReading Update(IEnumerable<NetworkCounter> counters, double now)
        {
            var current = new Dictionary<ulong, NetworkCounter>();
            foreach (var c in counters) if (c.Eligible && !current.ContainsKey(c.Id)) current.Add(c.Id, c);
            var reading = new NetworkReading { State = current.Count == 0 ? "断网" : "采样中", Interfaces = current.Count };
            double elapsed = now - lastTime;
            if (baseline && elapsed > 0 && elapsed <= 10 && current.Count > 0)
            {
                reading.State = "正常";
                foreach (var c in current.Values)
                {
                    NetworkCounter old;
                    if (!previous.TryGetValue(c.Id, out old)) continue;
                    if (c.Received < old.Received || c.Sent < old.Sent) continue;
                    reading.Download += (c.Received - old.Received) / elapsed;
                    reading.Upload += (c.Sent - old.Sent) / elapsed;
                }
            }
            previous = current; lastTime = now; baseline = current.Count > 0;
            return reading;
        }
        public static string Format(double speed)
        {
            string[] units = { "B/s", "KB/s", "MB/s", "GB/s" }; int unit = 0;
            while (speed >= 1000 && unit < 3) { speed /= 1000; unit++; }
            return speed.ToString(unit == 0 ? "0" : "0.0") + " " + units[unit];
        }
    }
}
