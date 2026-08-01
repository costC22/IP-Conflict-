using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace IPConflictMonitor.Launcher
{
    internal sealed class MonitorConfiguration
    {
        public NetworkConfiguration Network { get; set; }
        public MonitoringConfiguration Monitoring { get; set; }
        public IntegrationConfiguration Integrations { get; set; }
        public OutputConfiguration Output { get; set; }

        public MonitorConfiguration()
        {
            Network = new NetworkConfiguration();
            Monitoring = new MonitoringConfiguration();
            Integrations = new IntegrationConfiguration();
            Output = new OutputConfiguration();
        }
    }

    internal sealed class NetworkConfiguration
    {
        public string CIDR { get; set; }
        public int InterfaceIndex { get; set; }
        public int MaxHosts { get; set; }
        public string[] ExcludedIPs { get; set; }
        public string[] ExcludedMACs { get; set; }
        public string[] TrustedPairs { get; set; }

        public NetworkConfiguration()
        {
            CIDR = String.Empty;
            MaxHosts = 4094;
            ExcludedIPs = new string[0];
            ExcludedMACs = new string[0];
            TrustedPairs = new string[0];
        }
    }

    internal sealed class MonitoringConfiguration
    {
        public bool Continuous { get; set; }
        public int IntervalSeconds { get; set; }
        public int CaptureSeconds { get; set; }
        public int CaptureWarmupMilliseconds { get; set; }
        public int EvidenceWindowMinutes { get; set; }
        public int HistoryExpirationMinutes { get; set; }
        public int MinObservationsPerMac { get; set; }
        public int MinMacTransitions { get; set; }
        public int AlertCooldownMinutes { get; set; }
        public bool PingSweepEnabled { get; set; }
        public int MaxConcurrentPings { get; set; }
        public int PingTimeoutMs { get; set; }
        public bool ResolveHostnames { get; set; }
        public int HostnameTimeoutMs { get; set; }
        public bool PacketCaptureEnabled { get; set; }
        public bool ActiveArpProbeEnabled { get; set; }
        public int ActiveArpProbeRounds { get; set; }
        public int ActiveArpProbeDelayMs { get; set; }

        public MonitoringConfiguration()
        {
            Continuous = true;
            IntervalSeconds = 15;
            CaptureSeconds = 8;
            CaptureWarmupMilliseconds = 600;
            EvidenceWindowMinutes = 10;
            HistoryExpirationMinutes = 120;
            MinObservationsPerMac = 2;
            MinMacTransitions = 2;
            AlertCooldownMinutes = 10;
            PingSweepEnabled = true;
            MaxConcurrentPings = 48;
            PingTimeoutMs = 400;
            ResolveHostnames = true;
            HostnameTimeoutMs = 750;
            PacketCaptureEnabled = true;
            ActiveArpProbeEnabled = true;
            ActiveArpProbeRounds = 2;
            ActiveArpProbeDelayMs = 250;
        }
    }

    internal sealed class IntegrationConfiguration
    {
        public string TSharkPath { get; set; }
        public string DhcpServer { get; set; }
        public string OuiDatabasePath { get; set; }
        public string WebhookUrl { get; set; }
        public bool WebhookEnabled { get; set; }
        public bool WindowsEventLogEnabled { get; set; }

        public IntegrationConfiguration()
        {
            TSharkPath = String.Empty;
            DhcpServer = String.Empty;
            OuiDatabasePath = String.Empty;
            WebhookUrl = String.Empty;
        }
    }

    internal sealed class OutputConfiguration
    {
        public string Directory { get; set; }
        public int LogMaxMB { get; set; }
        public int LogRetentionFiles { get; set; }
        public bool SnapshotCsv { get; set; }
        public bool ConflictCsv { get; set; }
        public bool JsonState { get; set; }

        public OutputConfiguration()
        {
            Directory = String.Empty;
            LogMaxMB = 10;
            LogRetentionFiles = 7;
            SnapshotCsv = true;
            ConflictCsv = true;
            JsonState = true;
        }
    }

    internal sealed class NativeObservation
    {
        public DateTime Time { get; set; }
        public string IP { get; set; }
        public string MAC { get; set; }
        public string Source { get; set; }
        public string CycleId { get; set; }
        public int Round { get; set; }
    }

    internal sealed class NativeState
    {
        public DateTime SavedAt { get; set; }
        public List<NativeObservation> History { get; set; }
        public NativeState() { History = new List<NativeObservation>(); }
    }

    internal sealed class NativeSnapshotRow
    {
        public DateTime Timestamp;
        public string IP;
        public string Hostname;
        public string Status;
        public string MACs;
        public string MACDetails;
        public int MacCount;
        public int Observations;
        public int Transitions;
        public int DirectArpMacCount;
        public int ActiveProbeMacCount;
        public bool MappingMismatch;
        public DateTime FirstSeen;
        public DateTime LastSeen;
        public string DhcpHostname;
        public string DhcpClientId;
        public string Reason;
    }

    internal sealed class SelectedNetwork
    {
        public NetworkInterface Adapter;
        public IPAddress Address;
        public int PrefixLength;
        public int InterfaceIndex;
    }

    internal sealed class ArpEntry
    {
        public string IP;
        public string MAC;
        public int InterfaceIndex;
        public MibIpNetRow NativeRow;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MibIpNetRow
    {
        public int dwIndex;
        public int dwPhysAddrLen;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        public byte[] bPhysAddr;
        public uint dwAddr;
        public int dwType;
    }

    internal sealed class TSharkCapture : IDisposable
    {
        private readonly object _sync = new object();
        private readonly List<string> _lines = new List<string>();
        private readonly List<string> _errors = new List<string>();
        public Process Process;

        public void AddLine(string value) { if (value != null) { lock (_sync) { _lines.Add(value); } } }
        public void AddError(string value) { if (value != null) { lock (_sync) { _errors.Add(value); } } }
        public string[] Lines { get { lock (_sync) { return _lines.ToArray(); } } }
        public string ErrorText { get { lock (_sync) { return String.Join(Environment.NewLine, _errors.ToArray()); } } }

        public void Dispose()
        {
            if (Process != null)
            {
                try { if (!Process.HasExited) { Process.Kill(); } } catch { }
                Process.Dispose();
                Process = null;
            }
        }
    }

    internal static class NativeMonitor
    {
        private const string CsvHeader = "Timestamp,IP,Hostname,Status,MACs,MACDetails,MacCount,Observations,Transitions,DirectArpMacCount,ActiveProbeMacCount,MappingMismatch,FirstSeen,LastSeen,DhcpHostname,DhcpClientId,Reason";
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue, RecursionLimit = 100 };

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern int GetIpNetTable(IntPtr table, ref int size, bool order);

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern int DeleteIpNetEntry(ref MibIpNetRow row);

        public static int Run(string[] args, string configPath)
        {
            MonitorConfiguration config = LoadConfiguration(configPath);
            List<string> errors = ValidateConfiguration(config);
            if (errors.Count > 0) { throw new InvalidOperationException(String.Join(Environment.NewLine, errors.ToArray())); }
            if (HasSwitch(args, "-ValidateConfiguration", "--validate-configuration"))
            {
                Console.WriteLine("Configuracao valida: " + configPath);
                return 0;
            }

            bool once = HasSwitch(args, "-Once", "--once") || !config.Monitoring.Continuous;
            bool noCapture = HasSwitch(args, "-NoPacketCapture", "--no-packet-capture");
            bool noActiveProbe = HasSwitch(args, "-NoActiveArpProbe", "--no-active-arp-probe");
            bool noPingSweep = HasSwitch(args, "-NoPingSweep", "--no-ping-sweep");
            string root = ResolveOutputRoot(config);
            Directory.CreateDirectory(root);
            Directory.CreateDirectory(Path.Combine(root, "logs"));
            Directory.CreateDirectory(Path.Combine(root, "data"));
            Directory.CreateDirectory(Path.Combine(root, "reports"));
            string logPath = Path.Combine(root, "logs", "monitor.log");
            string statePath = Path.Combine(root, "data", "state.json");
            string snapshotPath = Path.Combine(root, "reports", "snapshot.csv");
            string conflictPath = Path.Combine(root, "reports", "conflicts.csv");

            bool created;
            using (var mutex = new Mutex(true, "Global\\IPConflictMonitor.Native", out created))
            {
                if (!created) { throw new InvalidOperationException("Outra instancia do monitor ja esta em execucao."); }
                var history = LoadState(statePath, config, logPath);
                var alertState = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    SelectedNetwork selected = SelectNetwork(config.Network.InterfaceIndex);
                    string baseAddress;
                    int prefix;
                    if (String.IsNullOrWhiteSpace(config.Network.CIDR))
                    {
                        baseAddress = selected.Address.ToString();
                        prefix = selected.PrefixLength;
                    }
                    else
                    {
                        string[] cidrParts = config.Network.CIDR.Split('/');
                        baseAddress = cidrParts[0];
                        prefix = Int32.Parse(cidrParts[1]);
                    }
                    string networkCidr;
                    List<string> targets = BuildTargets(baseAddress, prefix, config.Network.MaxHosts, config.Network.ExcludedIPs, out networkCidr);
                    var targetSet = new HashSet<string>(targets, StringComparer.OrdinalIgnoreCase);
                    var excludedMacs = new HashSet<string>((config.Network.ExcludedMACs ?? new string[0]).Select(NormalizeMac).Where(delegate(string value) { return value != null; }), StringComparer.OrdinalIgnoreCase);
                    Dictionary<string, HashSet<string>> trusted = BuildTrustedPairs(config.Network.TrustedPairs);
                    string tshark = (!noCapture && config.Monitoring.PacketCaptureEnabled) ? FindTShark(config.Integrations.TSharkPath) : null;
                    string captureInterface = tshark == null ? null : FindTSharkInterface(tshark, selected.Adapter);
                    bool administrator = IsAdministrator();
                    bool activeProbe = administrator && config.Monitoring.ActiveArpProbeEnabled && !noActiveProbe && config.Monitoring.PingSweepEnabled && !noPingSweep;
                    int rounds = activeProbe ? Math.Max(1, config.Monitoring.ActiveArpProbeRounds) : 1;

                    WriteLog(logPath, config.Output, "INFO", "Motor nativo iniciado. PowerShell: nao utilizado.");
                    WriteLog(logPath, config.Output, "INFO", "Interface=" + selected.Adapter.Name + "; IP=" + selected.Address + "; Rede=" + networkCidr + "; Alvos=" + targets.Count + ".");
                    if (tshark != null && captureInterface != null) { WriteLog(logPath, config.Output, "INFO", "Captura ARP direta ativa com TShark na interface " + captureInterface + "."); }
                    else { WriteLog(logPath, config.Output, "WARN", "Captura ARP direta indisponivel; a deteccao usara a tabela de vizinhos e o historico de MAC."); }
                    if (config.Monitoring.ActiveArpProbeEnabled && !activeProbe)
                    {
                        WriteLog(logPath, config.Output, "WARN", administrator ? "Sondagem ARP ativa desabilitada por parametro." : "Modo portatil sem elevacao: sondagem ARP ativa limitada; use o monitor continuo para maxima precisao.");
                    }

                    do
                    {
                        DateTime cycleStart = DateTime.Now;
                        string cycleId = Guid.NewGuid().ToString("N");
                        TSharkCapture capture = null;
                        try
                        {
                            AddArpObservations(history, ReadArpTable(selected.InterfaceIndex), targetSet, excludedMacs, "NeighborCache", cycleId, 0);
                            if (tshark != null && captureInterface != null)
                            {
                                capture = StartTSharkCapture(tshark, captureInterface, Math.Max(2, config.Monitoring.CaptureSeconds));
                                if (capture != null && config.Monitoring.CaptureWarmupMilliseconds > 0) { Thread.Sleep(config.Monitoring.CaptureWarmupMilliseconds); }
                            }

                            if (config.Monitoring.PingSweepEnabled && !noPingSweep)
                            {
                                for (int round = 1; round <= rounds; round++)
                                {
                                    int cleared = activeProbe ? ClearTargetNeighbors(selected.InterfaceIndex, targetSet) : 0;
                                    int reachable = PingSweep(targets, config.Monitoring.PingTimeoutMs, config.Monitoring.MaxConcurrentPings);
                                    string source = activeProbe ? "ActiveProbe" : "NeighborCache";
                                    AddArpObservations(history, ReadArpTable(selected.InterfaceIndex), targetSet, excludedMacs, source, cycleId, round);
                                    WriteLog(logPath, config.Output, "INFO", "Sondagem " + round + "/" + rounds + ": cache removida=" + cleared + "; ICMP=" + reachable + "/" + targets.Count + ".");
                                    if (round < rounds && config.Monitoring.ActiveArpProbeDelayMs > 0) { Thread.Sleep(config.Monitoring.ActiveArpProbeDelayMs); }
                                }
                            }

                            if (capture != null)
                            {
                                CompleteCapture(capture, history, targetSet, excludedMacs, cycleId, logPath, config.Output);
                                capture.Dispose();
                                capture = null;
                            }

                            DateTime expiration = DateTime.Now.AddMinutes(-Math.Max(config.Monitoring.HistoryExpirationMinutes, config.Monitoring.EvidenceWindowMinutes));
                            history.RemoveAll(delegate(NativeObservation observation) { return observation.Time < expiration; });
                            List<NativeSnapshotRow> rows = Evaluate(history, trusted, config);
                            if (config.Monitoring.ResolveHostnames) { ResolveHostnames(rows, config.Monitoring.HostnameTimeoutMs); }
                            if (config.Output.SnapshotCsv) { WriteSnapshot(snapshotPath, rows); }
                            ProcessAlerts(rows, conflictPath, logPath, alertState, config);
                            if (config.Output.JsonState) { SaveState(statePath, history); }
                            int confirmed = rows.Count(delegate(NativeSnapshotRow row) { return row.Status == "CONFIRMED"; });
                            int suspect = rows.Count(delegate(NativeSnapshotRow row) { return row.Status == "SUSPECT"; });
                            WriteLog(logPath, config.Output, "INFO", "Ciclo concluido: confirmados=" + confirmed + ", suspeitos=" + suspect + ", IPs=" + rows.Count + ".");
                        }
                        catch (Exception exception)
                        {
                            if (capture != null) { capture.Dispose(); }
                            WriteLog(logPath, config.Output, "ERROR", exception.ToString());
                            if (once) { throw; }
                        }
                        if (once) { break; }
                        int delay = Math.Max(1, config.Monitoring.IntervalSeconds - (int)(DateTime.Now - cycleStart).TotalSeconds);
                        Thread.Sleep(TimeSpan.FromSeconds(delay));
                    }
                    while (true);
                    return 0;
                }
                finally
                {
                    try { if (config.Output.JsonState) { SaveState(statePath, history); } } catch { }
                    try { mutex.ReleaseMutex(); } catch { }
                    WriteLog(logPath, config.Output, "INFO", "Monitor nativo encerrado.");
                }
            }
        }

        public static MonitorConfiguration LoadConfiguration(string path)
        {
            if (String.IsNullOrWhiteSpace(path) || !File.Exists(path)) { throw new FileNotFoundException("Configuracao nao encontrada.", path); }
            MonitorConfiguration config = Json.Deserialize<MonitorConfiguration>(File.ReadAllText(path, Encoding.UTF8));
            if (config == null) { throw new InvalidDataException("O arquivo de configuracao esta vazio."); }
            if (config.Network == null) { config.Network = new NetworkConfiguration(); }
            if (config.Monitoring == null) { config.Monitoring = new MonitoringConfiguration(); }
            if (config.Integrations == null) { config.Integrations = new IntegrationConfiguration(); }
            if (config.Output == null) { config.Output = new OutputConfiguration(); }
            if (config.Network.ExcludedIPs == null) { config.Network.ExcludedIPs = new string[0]; }
            if (config.Network.ExcludedMACs == null) { config.Network.ExcludedMACs = new string[0]; }
            if (config.Network.TrustedPairs == null) { config.Network.TrustedPairs = new string[0]; }
            return config;
        }

        public static List<string> ValidateConfiguration(MonitorConfiguration config)
        {
            var errors = new List<string>();
            if (config.Network.MaxHosts < 1 || config.Network.MaxHosts > 65534) { errors.Add("Network.MaxHosts deve estar entre 1 e 65534."); }
            if (!String.IsNullOrWhiteSpace(config.Network.CIDR))
            {
                string[] parts = config.Network.CIDR.Split('/');
                IPAddress address;
                int prefix;
                if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out address) || address.AddressFamily != AddressFamily.InterNetwork || !Int32.TryParse(parts[1], out prefix) || prefix < 0 || prefix > 32) { errors.Add("Network.CIDR deve usar o formato IPv4/prefixo, por exemplo 192.168.1.0/24."); }
            }
            foreach (string ip in config.Network.ExcludedIPs) { IPAddress parsed; if (!IPAddress.TryParse(ip, out parsed) || parsed.AddressFamily != AddressFamily.InterNetwork) { errors.Add("ExcludedIPs contem IPv4 invalido: " + ip); } }
            foreach (string mac in config.Network.ExcludedMACs) { if (NormalizeMac(mac) == null) { errors.Add("ExcludedMACs contem MAC invalido: " + mac); } }
            foreach (string pair in config.Network.TrustedPairs)
            {
                string[] parts = (pair ?? String.Empty).Split('|'); IPAddress parsed;
                if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out parsed) || parsed.AddressFamily != AddressFamily.InterNetwork || NormalizeMac(parts[1]) == null) { errors.Add("TrustedPairs contem par invalido: " + pair); }
            }
            if (config.Monitoring.IntervalSeconds < 1) { errors.Add("Monitoring.IntervalSeconds deve ser maior que zero."); }
            if (config.Monitoring.EvidenceWindowMinutes < 1) { errors.Add("Monitoring.EvidenceWindowMinutes deve ser maior que zero."); }
            if (config.Monitoring.PingTimeoutMs < 50) { errors.Add("Monitoring.PingTimeoutMs deve ser pelo menos 50."); }
            if (config.Monitoring.MaxConcurrentPings < 1 || config.Monitoring.MaxConcurrentPings > 256) { errors.Add("Monitoring.MaxConcurrentPings deve estar entre 1 e 256."); }
            if (config.Monitoring.ActiveArpProbeRounds < 1 || config.Monitoring.ActiveArpProbeRounds > 10) { errors.Add("Monitoring.ActiveArpProbeRounds deve estar entre 1 e 10."); }
            if (config.Output.LogMaxMB < 1) { errors.Add("Output.LogMaxMB deve ser maior que zero."); }
            return errors;
        }

        private static string ResolveOutputRoot(MonitorConfiguration config)
        {
            if (!String.IsNullOrWhiteSpace(config.Output.Directory)) { return Path.GetFullPath(Environment.ExpandEnvironmentVariables(config.Output.Directory)); }
            string basePath = IsAdministrator() ? Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData) : Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(basePath, "IPConflictMonitor");
        }

        private static SelectedNetwork SelectNetwork(int requestedIndex)
        {
            var candidates = new List<SelectedNetwork>();
            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) { continue; }
                IPInterfaceProperties properties;
                try { properties = adapter.GetIPProperties(); } catch { continue; }
                IPv4InterfaceProperties ipv4;
                try { ipv4 = properties.GetIPv4Properties(); } catch { continue; }
                if (ipv4 == null) { continue; }
                foreach (UnicastIPAddressInformation unicast in properties.UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(unicast.Address) || unicast.Address.ToString().StartsWith("169.254.", StringComparison.Ordinal)) { continue; }
                    int prefix = MaskToPrefix(unicast.IPv4Mask);
                    candidates.Add(new SelectedNetwork { Adapter = adapter, Address = unicast.Address, PrefixLength = prefix, InterfaceIndex = ipv4.Index });
                }
            }
            SelectedNetwork selected = requestedIndex > 0 ? candidates.FirstOrDefault(delegate(SelectedNetwork item) { return item.InterfaceIndex == requestedIndex; }) : candidates.OrderBy(delegate(SelectedNetwork item) { return item.Adapter.NetworkInterfaceType == NetworkInterfaceType.Ethernet ? 0 : 1; }).FirstOrDefault();
            if (selected == null) { throw new InvalidOperationException(requestedIndex > 0 ? "A interface configurada nao esta ativa ou nao possui IPv4." : "Nenhuma interface IPv4 ativa foi encontrada."); }
            return selected;
        }

        private static int MaskToPrefix(IPAddress mask)
        {
            if (mask == null) { return 24; }
            int prefix = 0;
            foreach (byte value in mask.GetAddressBytes()) { for (int bit = 7; bit >= 0; bit--) { if ((value & (1 << bit)) != 0) { prefix++; } else { return prefix; } } }
            return prefix;
        }

        private static List<string> BuildTargets(string address, int prefix, int maxHosts, string[] exclusions, out string cidr)
        {
            uint value = IpToUInt32(IPAddress.Parse(address));
            uint mask = prefix == 0 ? 0U : UInt32.MaxValue << (32 - prefix);
            uint network = value & mask;
            uint broadcast = network | ~mask;
            ulong first = prefix <= 30 ? (ulong)network + 1UL : network;
            ulong last = prefix <= 30 ? (ulong)broadcast - 1UL : broadcast;
            ulong count = last >= first ? last - first + 1UL : 0UL;
            if (count > (ulong)maxHosts) { throw new InvalidOperationException("Rede " + UInt32ToIp(network) + "/" + prefix + " possui " + count + " hosts e excede MaxHosts=" + maxHosts + "."); }
            var excluded = new HashSet<string>(exclusions ?? new string[0], StringComparer.OrdinalIgnoreCase);
            var targets = new List<string>();
            for (ulong current = first; current <= last; current++) { string ip = UInt32ToIp((uint)current); if (!excluded.Contains(ip)) { targets.Add(ip); } }
            cidr = UInt32ToIp(network) + "/" + prefix;
            return targets;
        }

        private static uint IpToUInt32(IPAddress address)
        {
            byte[] bytes = address.GetAddressBytes();
            return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
        }

        private static string UInt32ToIp(uint value)
        {
            return ((value >> 24) & 255) + "." + ((value >> 16) & 255) + "." + ((value >> 8) & 255) + "." + (value & 255);
        }

        private static List<ArpEntry> ReadArpTable(int interfaceIndex)
        {
            int size = 0;
            GetIpNetTable(IntPtr.Zero, ref size, false);
            if (size <= 4) { return new List<ArpEntry>(); }
            IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                int result = GetIpNetTable(buffer, ref size, false);
                if (result != 0) { throw new InvalidOperationException("GetIpNetTable falhou com codigo " + result + "."); }
                int count = Marshal.ReadInt32(buffer);
                int rowSize = Marshal.SizeOf(typeof(MibIpNetRow));
                IntPtr rowPointer = IntPtr.Add(buffer, 4);
                var output = new List<ArpEntry>();
                for (int index = 0; index < count; index++)
                {
                    var row = (MibIpNetRow)Marshal.PtrToStructure(rowPointer, typeof(MibIpNetRow));
                    rowPointer = IntPtr.Add(rowPointer, rowSize);
                    if (row.dwIndex != interfaceIndex || row.dwPhysAddrLen < 6 || row.bPhysAddr == null || row.dwType == 2) { continue; }
                    string mac = String.Join(":", row.bPhysAddr.Take(6).Select(delegate(byte item) { return item.ToString("X2"); }).ToArray());
                    string ip = new IPAddress(row.dwAddr).ToString();
                    output.Add(new ArpEntry { IP = ip, MAC = mac, InterfaceIndex = row.dwIndex, NativeRow = row });
                }
                return output;
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }

        private static int ClearTargetNeighbors(int interfaceIndex, HashSet<string> targets)
        {
            int cleared = 0;
            foreach (ArpEntry entry in ReadArpTable(interfaceIndex))
            {
                if (!targets.Contains(entry.IP)) { continue; }
                MibIpNetRow row = entry.NativeRow;
                if (DeleteIpNetEntry(ref row) == 0) { cleared++; }
            }
            return cleared;
        }

        private static void AddArpObservations(List<NativeObservation> history, IEnumerable<ArpEntry> entries, HashSet<string> targets, HashSet<string> excludedMacs, string source, string cycleId, int round)
        {
            DateTime now = DateTime.Now;
            foreach (ArpEntry entry in entries)
            {
                string mac = NormalizeMac(entry.MAC);
                if (!targets.Contains(entry.IP) || mac == null || excludedMacs.Contains(mac)) { continue; }
                history.Add(new NativeObservation { Time = now, IP = entry.IP, MAC = mac, Source = source, CycleId = cycleId, Round = round });
            }
        }

        private static int PingSweep(List<string> targets, int timeout, int concurrency)
        {
            int reachable = 0;
            var options = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Math.Min(256, concurrency)) };
            Parallel.ForEach(targets, options, delegate(string target)
            {
                try
                {
                    using (var ping = new Ping())
                    {
                        PingReply reply = ping.Send(target, Math.Max(50, timeout));
                        if (reply != null && reply.Status == IPStatus.Success) { Interlocked.Increment(ref reachable); }
                    }
                }
                catch { }
            });
            return reachable;
        }

        private static string FindTShark(string configured)
        {
            var candidates = new List<string>();
            if (!String.IsNullOrWhiteSpace(configured)) { candidates.Add(Environment.ExpandEnvironmentVariables(configured)); }
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Wireshark", "tshark.exe"));
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (!String.IsNullOrWhiteSpace(programFilesX86)) { candidates.Add(Path.Combine(programFilesX86, "Wireshark", "tshark.exe")); }
            return candidates.FirstOrDefault(File.Exists);
        }

        private static string FindTSharkInterface(string tshark, NetworkInterface adapter)
        {
            try
            {
                var info = new ProcessStartInfo { FileName = tshark, Arguments = "-D", UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                using (Process process = Process.Start(info))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit(5000);
                    foreach (string line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (line.IndexOf(adapter.Id, StringComparison.OrdinalIgnoreCase) < 0 && line.IndexOf(adapter.Name, StringComparison.OrdinalIgnoreCase) < 0 && line.IndexOf(adapter.Description, StringComparison.OrdinalIgnoreCase) < 0) { continue; }
                        int dot = line.IndexOf('.');
                        if (dot > 0) { return line.Substring(0, dot).Trim(); }
                    }
                }
            }
            catch { }
            return null;
        }

        private static TSharkCapture StartTSharkCapture(string tshark, string interfaceNumber, int seconds)
        {
            try
            {
                var capture = new TSharkCapture();
                var info = new ProcessStartInfo
                {
                    FileName = tshark,
                    Arguments = "-l -n -i " + interfaceNumber + " -a duration:" + seconds + " -f arp -T fields -E separator=| -e frame.time_epoch -e arp.src.proto_ipv4 -e arp.src.hw_mac",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                capture.Process = new Process { StartInfo = info, EnableRaisingEvents = true };
                capture.Process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs eventArgs) { capture.AddLine(eventArgs.Data); };
                capture.Process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs eventArgs) { capture.AddError(eventArgs.Data); };
                if (!capture.Process.Start()) { capture.Dispose(); return null; }
                capture.Process.BeginOutputReadLine();
                capture.Process.BeginErrorReadLine();
                return capture;
            }
            catch { return null; }
        }

        private static void CompleteCapture(TSharkCapture capture, List<NativeObservation> history, HashSet<string> targets, HashSet<string> excludedMacs, string cycleId, string logPath, OutputConfiguration output)
        {
            if (!capture.Process.WaitForExit(30000)) { try { capture.Process.Kill(); } catch { } }
            int added = 0;
            foreach (string line in capture.Lines)
            {
                string[] parts = line.Split('|');
                if (parts.Length < 3 || !targets.Contains(parts[1].Trim())) { continue; }
                string mac = NormalizeMac(parts[2]);
                if (mac == null || excludedMacs.Contains(mac)) { continue; }
                double epoch;
                DateTime when = DateTime.Now;
                if (Double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out epoch)) { when = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(epoch).ToLocalTime(); }
                history.Add(new NativeObservation { Time = when, IP = parts[1].Trim(), MAC = mac, Source = "ARP-Packet", CycleId = cycleId, Round = 0 });
                added++;
            }
            WriteLog(logPath, output, "INFO", "Observacoes ARP diretas: " + added + ".");
            if (capture.Process.ExitCode != 0 && !String.IsNullOrWhiteSpace(capture.ErrorText)) { WriteLog(logPath, output, "WARN", "TShark: " + capture.ErrorText.Split('\n')[0].Trim()); }
        }

        private static List<NativeSnapshotRow> Evaluate(List<NativeObservation> history, Dictionary<string, HashSet<string>> trusted, MonitorConfiguration config)
        {
            DateTime now = DateTime.Now;
            DateTime window = now.AddMinutes(-config.Monitoring.EvidenceWindowMinutes);
            var rows = new List<NativeSnapshotRow>();
            foreach (IGrouping<string, NativeObservation> ipGroup in history.Where(delegate(NativeObservation item) { return item.Time >= window; }).GroupBy(delegate(NativeObservation item) { return item.IP; }, StringComparer.OrdinalIgnoreCase))
            {
                List<NativeObservation> events = ipGroup.OrderBy(delegate(NativeObservation item) { return item.Time; }).ToList();
                string[] macs = events.Select(delegate(NativeObservation item) { return item.MAC; }).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(delegate(string item) { return item; }).ToArray();
                int transitions = 0;
                string previous = null;
                foreach (NativeObservation item in events) { if (previous != null && !String.Equals(previous, item.MAC, StringComparison.OrdinalIgnoreCase)) { transitions++; } previous = item.MAC; }
                bool directConflict = events.Where(delegate(NativeObservation item) { return item.Source == "ARP-Packet"; }).GroupBy(delegate(NativeObservation item) { return item.CycleId; }).Any(delegate(IGrouping<string, NativeObservation> group) { return group.Select(delegate(NativeObservation item) { return item.MAC; }).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1; });
                bool activeConflict = events.Where(delegate(NativeObservation item) { return item.Source == "ActiveProbe"; }).GroupBy(delegate(NativeObservation item) { return item.CycleId; }).Any(delegate(IGrouping<string, NativeObservation> group) { return group.Select(delegate(NativeObservation item) { return item.MAC; }).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1; });
                HashSet<string> trustedMacs;
                bool hasTrusted = trusted.TryGetValue(ipGroup.Key, out trustedMacs) && trustedMacs.Count > 0;
                string[] untrusted = hasTrusted ? macs.Where(delegate(string mac) { return !trustedMacs.Contains(mac); }).ToArray() : new string[0];
                bool mismatch = hasTrusted && untrusted.Length > 0;
                string status = "NORMAL";
                string reason = "Um unico MAC observado.";
                if (macs.Length > 1)
                {
                    if (hasTrusted && untrusted.Length == 0) { reason = "Todos os MACs observados estao autorizados em TrustedPairs."; }
                    else
                    {
                        status = "SUSPECT";
                        reason = "Mais de um MAC observado na janela de evidencias.";
                        bool enough = macs.Count(delegate(string mac) { return events.Count(delegate(NativeObservation item) { return String.Equals(item.MAC, mac, StringComparison.OrdinalIgnoreCase); }) >= config.Monitoring.MinObservationsPerMac; }) >= 2;
                        if (directConflict) { status = "CONFIRMED"; reason = "Dois MACs responderam durante a mesma captura ARP."; }
                        else if (activeConflict) { status = "CONFIRMED"; reason = "Dois MACs venceram sondagens ARP ativas no mesmo ciclo."; }
                        else if (enough && transitions >= config.Monitoring.MinMacTransitions) { status = "CONFIRMED"; reason = "Alternancia recorrente do MAC confirmada na tabela de vizinhos."; }
                    }
                }
                else if (mismatch) { status = "SUSPECT"; reason = "O MAC observado difere do par autorizado para este IP."; }

                int directCount = events.Where(delegate(NativeObservation item) { return item.Source == "ARP-Packet"; }).Select(delegate(NativeObservation item) { return item.MAC; }).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                int activeCount = events.Where(delegate(NativeObservation item) { return item.Source == "ActiveProbe"; }).Select(delegate(NativeObservation item) { return item.MAC; }).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                rows.Add(new NativeSnapshotRow
                {
                    Timestamp = now, IP = ipGroup.Key, Hostname = String.Empty, Status = status, MACs = String.Join(", ", macs),
                    MACDetails = mismatch ? "MAC nao autorizado: " + String.Join(", ", untrusted) : (macs.Length > 1 ? "Multiplas identidades de camada 2" : "Associacao estavel"),
                    MacCount = macs.Length, Observations = events.Count, Transitions = transitions, DirectArpMacCount = directCount, ActiveProbeMacCount = activeCount,
                    MappingMismatch = mismatch, FirstSeen = events.First().Time, LastSeen = events.Last().Time, DhcpHostname = String.Empty, DhcpClientId = String.Empty, Reason = reason
                });
            }
            return rows.OrderBy(delegate(NativeSnapshotRow row) { return row.Status == "CONFIRMED" ? 0 : row.Status == "SUSPECT" ? 1 : 2; }).ThenBy(delegate(NativeSnapshotRow row) { return IpToUInt32(IPAddress.Parse(row.IP)); }).ToList();
        }

        private static void ResolveHostnames(List<NativeSnapshotRow> rows, int timeout)
        {
            var options = new ParallelOptions { MaxDegreeOfParallelism = 12 };
            Parallel.ForEach(rows, options, delegate(NativeSnapshotRow row)
            {
                try
                {
                    Task<IPHostEntry> task = Task.Factory.StartNew(delegate { return Dns.GetHostEntry(row.IP); });
                    if (task.Wait(Math.Max(100, timeout)) && task.Result != null) { row.Hostname = task.Result.HostName ?? String.Empty; }
                }
                catch { }
            });
        }

        private static Dictionary<string, HashSet<string>> BuildTrustedPairs(string[] pairs)
        {
            var output = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (string pair in pairs ?? new string[0])
            {
                string[] parts = pair.Split('|'); if (parts.Length != 2) { continue; }
                string mac = NormalizeMac(parts[1]); if (mac == null) { continue; }
                HashSet<string> values; if (!output.TryGetValue(parts[0], out values)) { values = new HashSet<string>(StringComparer.OrdinalIgnoreCase); output[parts[0]] = values; }
                values.Add(mac);
            }
            return output;
        }

        private static List<NativeObservation> LoadState(string path, MonitorConfiguration config, string logPath)
        {
            if (!config.Output.JsonState || !File.Exists(path)) { return new List<NativeObservation>(); }
            try
            {
                NativeState state = Json.Deserialize<NativeState>(File.ReadAllText(path, Encoding.UTF8));
                return state != null && state.History != null ? state.History : new List<NativeObservation>();
            }
            catch (Exception exception) { WriteLog(logPath, config.Output, "WARN", "Estado anterior ignorado: " + exception.Message); return new List<NativeObservation>(); }
        }

        private static void SaveState(string path, List<NativeObservation> history)
        {
            string temporary = path + ".new";
            File.WriteAllText(temporary, Json.Serialize(new NativeState { SavedAt = DateTime.Now, History = history }), new UTF8Encoding(false));
            File.Copy(temporary, path, true);
            File.Delete(temporary);
        }

        private static void WriteSnapshot(string path, List<NativeSnapshotRow> rows)
        {
            var builder = new StringBuilder();
            builder.AppendLine(CsvHeader);
            foreach (NativeSnapshotRow row in rows) { builder.AppendLine(ToCsv(row)); }
            string temporary = path + ".new";
            File.WriteAllText(temporary, builder.ToString(), new UTF8Encoding(true));
            File.Copy(temporary, path, true);
            File.Delete(temporary);
        }

        private static void ProcessAlerts(List<NativeSnapshotRow> rows, string conflictPath, string logPath, Dictionary<string, DateTime> alertState, MonitorConfiguration config)
        {
            foreach (NativeSnapshotRow row in rows)
            {
                if (row.Status == "CONFIRMED")
                {
                    string fingerprint = row.IP + "|" + row.MACs;
                    DateTime previous;
                    if (!alertState.TryGetValue(fingerprint, out previous) || (DateTime.Now - previous).TotalMinutes >= config.Monitoring.AlertCooldownMinutes)
                    {
                        WriteLog(logPath, config.Output, "CONFIRMED", "Conflito confirmado em " + row.IP + ". MACs: " + row.MACs + ". Motivo: " + row.Reason);
                        if (config.Output.ConflictCsv) { AppendConflict(conflictPath, row); }
                        if (config.Integrations.WebhookEnabled && !String.IsNullOrWhiteSpace(config.Integrations.WebhookUrl)) { SendWebhook(config.Integrations.WebhookUrl, row, logPath, config.Output); }
                        alertState[fingerprint] = DateTime.Now;
                    }
                }
                else if (row.Status == "SUSPECT") { WriteLog(logPath, config.Output, "SUSPECT", "Suspeita em " + row.IP + "; MACs: " + row.MACs + ". Motivo: " + row.Reason); }
            }
        }

        private static void AppendConflict(string path, NativeSnapshotRow row)
        {
            bool header = !File.Exists(path) || new FileInfo(path).Length == 0;
            using (var writer = new StreamWriter(path, true, new UTF8Encoding(true))) { if (header) { writer.WriteLine(CsvHeader); } writer.WriteLine(ToCsv(row)); }
        }

        private static string ToCsv(NativeSnapshotRow row)
        {
            return String.Join(",", new[]
            {
                Csv(row.Timestamp.ToString("s")),Csv(row.IP),Csv(row.Hostname),Csv(row.Status),Csv(row.MACs),Csv(row.MACDetails),Csv(row.MacCount.ToString()),Csv(row.Observations.ToString()),Csv(row.Transitions.ToString()),
                Csv(row.DirectArpMacCount.ToString()),Csv(row.ActiveProbeMacCount.ToString()),Csv(row.MappingMismatch.ToString()),Csv(row.FirstSeen.ToString("s")),Csv(row.LastSeen.ToString("s")),Csv(row.DhcpHostname),Csv(row.DhcpClientId),Csv(row.Reason)
            });
        }

        private static string Csv(string value) { return "\"" + (value ?? String.Empty).Replace("\"", "\"\"") + "\""; }

        private static void SendWebhook(string url, NativeSnapshotRow row, string logPath, OutputConfiguration output)
        {
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "POST"; request.ContentType = "application/json"; request.Timeout = 5000;
                byte[] body = Encoding.UTF8.GetBytes(Json.Serialize(new { type = "ip_conflict", severity = "critical", ip = row.IP, macs = row.MACs, reason = row.Reason, timestamp = row.Timestamp }));
                request.ContentLength = body.Length;
                using (Stream stream = request.GetRequestStream()) { stream.Write(body, 0, body.Length); }
                using (WebResponse response = request.GetResponse()) { }
            }
            catch (Exception exception) { WriteLog(logPath, output, "WARN", "Falha no webhook: " + exception.Message); }
        }

        private static void WriteLog(string path, OutputConfiguration output, string level, string message)
        {
            try
            {
                RotateLog(path, output.LogMaxMB, output.LogRetentionFiles);
                File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [" + level + "] " + message + Environment.NewLine, new UTF8Encoding(false));
            }
            catch { }
            if (Environment.UserInteractive) { Console.WriteLine("[" + level + "] " + message); }
        }

        private static void RotateLog(string path, int maxMb, int retention)
        {
            if (!File.Exists(path) || new FileInfo(path).Length < (long)Math.Max(1, maxMb) * 1024L * 1024L) { return; }
            int keep = Math.Max(1, retention);
            for (int index = keep - 1; index >= 1; index--) { string source = path + "." + index; string destination = path + "." + (index + 1); if (File.Exists(source)) { File.Copy(source, destination, true); } }
            File.Copy(path, path + ".1", true);
            File.WriteAllText(path, String.Empty);
        }

        private static string NormalizeMac(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) { return null; }
            string hex = new string(value.Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
            if (hex.Length != 12 || hex == "000000000000" || hex == "FFFFFFFFFFFF") { return null; }
            return String.Join(":", Enumerable.Range(0, 6).Select(delegate(int index) { return hex.Substring(index * 2, 2); }).ToArray());
        }

        private static bool IsAdministrator()
        {
            try { using (WindowsIdentity identity = WindowsIdentity.GetCurrent()) { return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator); } }
            catch { return false; }
        }

        private static bool HasSwitch(string[] args, params string[] names)
        {
            foreach (string argument in args) { foreach (string name in names) { if (String.Equals(argument, name, StringComparison.OrdinalIgnoreCase)) { return true; } } }
            return false;
        }
    }
}
