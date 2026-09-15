using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
        public MonitorConfiguration() { Network = new NetworkConfiguration(); Monitoring = new MonitoringConfiguration(); Integrations = new IntegrationConfiguration(); Output = new OutputConfiguration(); }
    }

    internal sealed class NetworkConfiguration
    {
        public string CIDR { get; set; }
        public int InterfaceIndex { get; set; }
        public int MaxHosts { get; set; }
        public string[] ExcludedIPs { get; set; }
        public string[] ExcludedMACs { get; set; }
        public string[] TrustedPairs { get; set; }
        public string[] TrustedVirtualIps { get; set; }
        public string[] TrustedMacs { get; set; }
        public NetworkConfiguration() { CIDR = String.Empty; MaxHosts = 4094; ExcludedIPs = new string[0]; ExcludedMACs = new string[0]; TrustedPairs = new string[0]; TrustedVirtualIps = new string[0]; TrustedMacs = new string[0]; }
    }

    internal sealed class MonitoringConfiguration
    {
        public bool Continuous { get; set; }
        public int IntervalSeconds { get; set; }
        public int CaptureSeconds { get; set; }
        public int CaptureWarmupMilliseconds { get; set; }
        public int EvidenceWindowMinutes { get; set; }
        public int HistoryExpirationMinutes { get; set; }
        public int AlertCooldownMinutes { get; set; }
        public bool PingSweepEnabled { get; set; }
        public int MaxConcurrentPings { get; set; }
        public int PingTimeoutMs { get; set; }
        public bool ResolveHostnames { get; set; }
        public int HostnameTimeoutMs { get; set; }
        public bool PacketCaptureEnabled { get; set; }
        public bool ActiveArpProbeEnabled { get; set; }
        public string DetectionMode { get; set; }
        public int VerificationRounds { get; set; }
        public int RequiredPositiveRounds { get; set; }
        public int RequiredConfirmedCycles { get; set; }
        public int ArpResponseWindowMs { get; set; }
        public bool RequireCapturedArpRequest { get; set; }
        public bool RequireCorrelatedArpResponses { get; set; }
        public bool FailClosedWithoutCapture { get; set; }
        public bool DetectProxyArp { get; set; }
        public int ProxyArpIpThreshold { get; set; }
        public int MaxConcurrentVerifications { get; set; }
        public int ArpProbeRateLimitMs { get; set; }

        public MonitoringConfiguration()
        {
            Continuous = true; IntervalSeconds = 15; CaptureSeconds = 5; CaptureWarmupMilliseconds = 500; EvidenceWindowMinutes = 10; HistoryExpirationMinutes = 120; AlertCooldownMinutes = 10;
            PingSweepEnabled = true; MaxConcurrentPings = 48; PingTimeoutMs = 400; ResolveHostnames = true; HostnameTimeoutMs = 750; PacketCaptureEnabled = true; ActiveArpProbeEnabled = true;
            DetectionMode = "StrictEvidence"; VerificationRounds = 3; RequiredPositiveRounds = 2; RequiredConfirmedCycles = 2; ArpResponseWindowMs = 1500;
            RequireCapturedArpRequest = true; RequireCorrelatedArpResponses = true; FailClosedWithoutCapture = true; DetectProxyArp = true; ProxyArpIpThreshold = 4; MaxConcurrentVerifications = 1; ArpProbeRateLimitMs = 250;
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
        public IntegrationConfiguration() { TSharkPath = String.Empty; DhcpServer = String.Empty; OuiDatabasePath = String.Empty; WebhookUrl = String.Empty; }
    }

    internal sealed class OutputConfiguration
    {
        public string Directory { get; set; }
        public int LogMaxMB { get; set; }
        public int LogRetentionFiles { get; set; }
        public bool SnapshotCsv { get; set; }
        public bool ConflictCsv { get; set; }
        public bool JsonState { get; set; }
        public OutputConfiguration() { Directory = String.Empty; LogMaxMB = 10; LogRetentionFiles = 7; SnapshotCsv = true; ConflictCsv = true; JsonState = true; }
    }

    internal sealed class NativeObservation
    {
        public DateTime TimeUtc { get; set; }
        public string IP { get; set; }
        public string MAC { get; set; }
        public string Source { get; set; }
        public string CycleId { get; set; }
        public long Cycle { get; set; }
        public bool Gratuitous { get; set; }
    }

    internal sealed class DetectionMetrics
    {
        public long TotalIpsObserved { get; set; }
        public long VerificationRequests { get; set; }
        public long PositiveVerificationRounds { get; set; }
        public long ConfirmedConflicts { get; set; }
        public long RejectedHistoricalTransitions { get; set; }
        public long RejectedProxyArp { get; set; }
        public long CaptureFailures { get; set; }
        public long MonitoringLimitedEvents { get; set; }
    }

    internal sealed class NativeState
    {
        public DateTime SavedAtUtc { get; set; }
        public long CycleSequence { get; set; }
        public List<NativeObservation> History { get; set; }
        public Dictionary<string, ConflictDecisionMemory> Decisions { get; set; }
        public DetectionMetrics Metrics { get; set; }
        public NativeState() { History = new List<NativeObservation>(); Decisions = new Dictionary<string, ConflictDecisionMemory>(StringComparer.OrdinalIgnoreCase); Metrics = new DetectionMetrics(); }
    }

    internal sealed class NativeSnapshotRow
    {
        public DateTime TimestampUtc;
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
        public string Reason;
        public string Interface;
        public string MonitorIp;
        public bool RequestObserved;
        public int PositiveRounds;
        public int RequiredRounds;
        public int ConfirmedCycles;
        public int RequiredCycles;
        public int CorrelatedArpReplies;
        public bool ProxyArpRisk;
        public bool GatewayMac;
        public bool TrustedPair;
        public bool CaptureHealthy;
        public int ConfidenceScore;
        public string EvidenceId;
        public string EvidenceHash;
        public string EvidenceQuality;
    }

    internal sealed class SelectedNetwork
    {
        public NetworkInterface Adapter;
        public IPAddress Address;
        public int PrefixLength;
        public int InterfaceIndex;
        public string Mac;
        public IPAddress Gateway;
        public string GatewayMac;
        public string Cidr;
        public string SelectionReason;
        public bool ConfiguredNetworkReachable;
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
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)] public byte[] bPhysAddr;
        public uint dwAddr;
        public int dwType;
    }

    internal sealed class ArpPacket
    {
        public DateTime TimeUtc;
        public int Opcode;
        public string SourceIp;
        public string SourceMac;
        public string TargetIp;
        public string TargetMac;
        public bool Gratuitous;
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
        public void Dispose() { if (Process != null) { try { if (!Process.HasExited) { Process.Kill(); Process.WaitForExit(1500); } } catch { } Process.Dispose(); Process = null; } }
    }

    internal static class NativeMonitor
    {
        private const string StopEventName = "Local\\IPConflictMonitor.StrictEvidence.Stop";
        private const string CsvHeader = "TimestampUtc,IP,Hostname,Status,MACs,MACDetails,MacCount,Observations,Transitions,DirectArpMacCount,ActiveProbeMacCount,MappingMismatch,FirstSeen,LastSeen,Reason,Interface,MonitorIp,RequestObserved,PositiveRounds,RequiredRounds,ConfirmedCycles,RequiredCycles,CorrelatedArpReplies,ProxyArpRisk,GatewayMac,TrustedPair,CaptureHealthy,ConfidenceScore,EvidenceId,EvidenceHash,EvidenceQuality";
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue, RecursionLimit = 100 };
        private static long _evidenceSequence;

        [DllImport("iphlpapi.dll", SetLastError = true)] private static extern int GetIpNetTable(IntPtr table, ref int size, bool order);
        [DllImport("iphlpapi.dll", SetLastError = true)] private static extern int DeleteIpNetEntry(ref MibIpNetRow row);
        [DllImport("iphlpapi.dll", ExactSpelling = true)] private static extern int SendARP(uint destinationIp, uint sourceIp, byte[] macAddress, ref int physicalAddressLength);

        public static int Run(string[] args, string configPath)
        {
            MonitorConfiguration config = LoadConfiguration(configPath);
            List<string> errors = ValidateConfiguration(config);
            if (errors.Count > 0) { throw new InvalidOperationException(String.Join(Environment.NewLine, errors.ToArray())); }
            if (HasSwitch(args, "-ValidateConfiguration", "--validate-configuration")) { Console.WriteLine("Configuracao valida: " + configPath); return 0; }

            bool once = HasSwitch(args, "-Once", "--once") || !config.Monitoring.Continuous;
            bool noCapture = HasSwitch(args, "-NoPacketCapture", "--no-packet-capture");
            bool noPingSweep = HasSwitch(args, "-NoPingSweep", "--no-ping-sweep");
            string root = ResolveOutputRoot(config);
            Directory.CreateDirectory(Path.Combine(root, "logs")); Directory.CreateDirectory(Path.Combine(root, "data")); Directory.CreateDirectory(Path.Combine(root, "reports"));
            string logPath = Path.Combine(root, "logs", "monitor.log");
            string statePath = Path.Combine(root, "data", "state.json");
            string snapshotPath = Path.Combine(root, "reports", "snapshot.csv");
            string conflictPath = Path.Combine(root, "reports", "conflicts.csv");

            bool created;
            using (var mutex = new Mutex(true, "Global\\IPConflictMonitor.Native", out created))
            using (var stopEvent = new EventWaitHandle(false, EventResetMode.ManualReset, StopEventName))
            {
                if (!created) { throw new InvalidOperationException("Outra instancia do monitor ja esta em execucao."); }
                stopEvent.Reset();
                NativeState state = LoadState(statePath, config, logPath);
                var alertState = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    SelectedNetwork selected = SelectNetwork(config.Network.InterfaceIndex, config.Network.CIDR);
                    string networkCidr;
                    string baseAddress = String.IsNullOrWhiteSpace(config.Network.CIDR) ? selected.Address.ToString() : config.Network.CIDR.Split('/')[0];
                    int prefix = String.IsNullOrWhiteSpace(config.Network.CIDR) ? selected.PrefixLength : Int32.Parse(config.Network.CIDR.Split('/')[1]);
                    List<string> targets = BuildTargets(baseAddress, prefix, config.Network.MaxHosts, config.Network.ExcludedIPs, out networkCidr);
                    targets.RemoveAll(delegate(string ip) { return String.Equals(ip, selected.Address.ToString(), StringComparison.OrdinalIgnoreCase); });
                    var targetSet = new HashSet<string>(targets, StringComparer.OrdinalIgnoreCase);
                    var excludedMacs = new HashSet<string>((config.Network.ExcludedMACs ?? new string[0]).Select(StrictEvidenceDecisionEngine.NormalizeAndValidateMac).Where(delegate(string value) { return value != null; }), StringComparer.OrdinalIgnoreCase);
                    Dictionary<string, HashSet<string>> trustedPairs = BuildTrustedPairs(config.Network.TrustedPairs);
                    var trustedVirtualIps = new HashSet<string>(config.Network.TrustedVirtualIps ?? new string[0], StringComparer.OrdinalIgnoreCase);
                    var trustedMacs = new HashSet<string>((config.Network.TrustedMacs ?? new string[0]).Select(StrictEvidenceDecisionEngine.NormalizeAndValidateMac).Where(delegate(string value) { return value != null; }), StringComparer.OrdinalIgnoreCase);
                    string tshark = (!noCapture && config.Monitoring.PacketCaptureEnabled) ? FindTShark(config.Integrations.TSharkPath) : null;
                    string captureInterface = tshark == null ? null : FindTSharkInterface(tshark, selected.Adapter);
                    DetectionHealth health = BuildHealth(tshark, captureInterface, selected, config.Monitoring.ActiveArpProbeEnabled);
                    StrictPolicy policy = BuildPolicy(config);

                    WriteLog(logPath, config.Output, "INFO", "Strict Evidence Detection Engine 3.3.1 iniciado. Historico/cache/ICMP nao podem produzir CONFIRMED.");
                    WriteLog(logPath, config.Output, "INFO", "SelectedInterfaceName=" + selected.Adapter.Name + "; SelectedInterfaceIndex=" + selected.InterfaceIndex + "; SelectedInterfaceIPv4=" + selected.Address + "; SelectedInterfaceMac=" + selected.Mac + "; SelectedInterfaceCidr=" + selected.Cidr + "; SelectionReason=" + selected.SelectionReason + ".");
                    WriteLog(logPath, config.Output, health.StrictVerificationReady ? "INFO" : "WARN", "Capture Engine=" + health.Summary + "; Npcap=" + (health.NpcapAvailable ? "OK" : "INDISPONIVEL") + "; TShark=" + (health.TsharkAvailable ? "OK" : "INDISPONIVEL") + "; Strict Verification=" + (health.StrictVerificationReady ? "READY" : "MONITORING_LIMITED") + ".");
                    if (!selected.ConfiguredNetworkReachable) { WriteLog(logPath, config.Output, "WARN", "configured network not reachable through selected interface"); }

                    do
                    {
                        DateTime cycleStartUtc = DateTime.UtcNow;
                        string cycleId = Guid.NewGuid().ToString("N");
                        state.CycleSequence++;
                        long cycle = state.CycleSequence;
                        TSharkCapture discoveryCapture = null;
                        try
                        {
                            if (!health.StrictVerificationReady && health.TsharkAvailable && health.NpcapAvailable && health.InterfaceReady && config.Monitoring.ActiveArpProbeEnabled)
                            {
                                health.StrictVerificationReady = true; health.LastError = String.Empty;
                                WriteLog(logPath, config.Output, "INFO", "Tentando recuperar o mecanismo de captura antes do ciclo " + cycle + ".");
                            }
                            AddArpObservations(state.History, ReadArpTable(selected.InterfaceIndex), targetSet, excludedMacs, "NeighborCache", cycleId, cycle);
                            if (health.StrictVerificationReady)
                            {
                                int discoveryCaptureSeconds = CalculateDiscoveryCaptureSeconds(targets.Count, config.Monitoring.MaxConcurrentPings, config.Monitoring.PingTimeoutMs, config.Monitoring.CaptureWarmupMilliseconds, config.Monitoring.CaptureSeconds);
                                discoveryCapture = StartTSharkCapture(tshark, captureInterface, discoveryCaptureSeconds, null);
                                health.CaptureRunning = discoveryCapture != null;
                                if (discoveryCapture == null) { health.StrictVerificationReady = false; health.LastError = "falha ao iniciar TShark"; state.Metrics.CaptureFailures++; }
                                else if (config.Monitoring.CaptureWarmupMilliseconds > 0) { WaitCancelable(stopEvent, config.Monitoring.CaptureWarmupMilliseconds); }
                            }

                            if (config.Monitoring.PingSweepEnabled && !noPingSweep && !stopEvent.WaitOne(0))
                            {
                                int refreshed = discoveryCapture != null && config.Monitoring.ActiveArpProbeEnabled ? ClearDynamicNeighbors(selected.InterfaceIndex, targetSet) : 0;
                                int reachable = PingSweep(targets, config.Monitoring.PingTimeoutMs, config.Monitoring.MaxConcurrentPings);
                                AddArpObservations(state.History, ReadArpTable(selected.InterfaceIndex), targetSet, excludedMacs, "NeighborCache", cycleId, cycle);
                                RefreshGatewayMac(selected);
                                WriteLog(logPath, config.Output, "DISCOVERY", "Ciclo=" + cycle + "; renovacoes ARP forçadas=" + refreshed + "; ICMP auxiliar=" + reachable + "/" + targets.Count + "; nenhuma decisao usa respostas ICMP.");
                            }

                            if (discoveryCapture != null)
                            {
                                CompleteDiscoveryCapture(discoveryCapture, state.History, targetSet, excludedMacs, cycleId, cycle, health, logPath, config.Output);
                                discoveryCapture.Dispose(); discoveryCapture = null;
                            }

                            DateTime expirationUtc = DateTime.UtcNow.AddMinutes(-Math.Max(config.Monitoring.HistoryExpirationMinutes, config.Monitoring.EvidenceWindowMinutes));
                            state.History.RemoveAll(delegate(NativeObservation observation) { return observation.TimeUtc < expirationUtc; });
                            List<NativeSnapshotRow> rows = EvaluateCycle(state, selected, targetSet, excludedMacs, trustedPairs, trustedVirtualIps, trustedMacs, tshark, captureInterface, health, policy, config, cycleId, cycle, stopEvent, logPath);
                            if (config.Monitoring.ResolveHostnames) { ResolveHostnames(rows, config.Monitoring.HostnameTimeoutMs); }
                            if (config.Output.SnapshotCsv) { WriteSnapshot(snapshotPath, rows); }
                            ProcessAlerts(rows, conflictPath, logPath, alertState, config);
                            if (config.Output.JsonState) { SaveState(statePath, state); }
                            int confirmed = rows.Count(delegate(NativeSnapshotRow row) { return row.Status == "CONFIRMED"; });
                            int unverified = rows.Count(delegate(NativeSnapshotRow row) { return row.Status == "UNVERIFIED"; });
                            int limited = rows.Count(delegate(NativeSnapshotRow row) { return row.Status == "MONITORING_LIMITED"; });
                            WriteLog(logPath, config.Output, "INFO", "Ciclo concluido: confirmados=" + confirmed + ", nao_verificados=" + unverified + ", monitoramento_limitado=" + limited + ", IPs=" + rows.Count + ".");
                        }
                        catch (Exception exception)
                        {
                            if (discoveryCapture != null) { discoveryCapture.Dispose(); }
                            health.StrictVerificationReady = false; health.LastError = exception.Message; state.Metrics.CaptureFailures++;
                            WriteLog(logPath, config.Output, "ERROR", exception.ToString());
                            if (once) { throw; }
                        }
                        if (once || stopEvent.WaitOne(0)) { break; }
                        int elapsed = (int)(DateTime.UtcNow - cycleStartUtc).TotalSeconds;
                        WaitCancelable(stopEvent, Math.Max(1, config.Monitoring.IntervalSeconds - elapsed) * 1000);
                    }
                    while (!stopEvent.WaitOne(0));
                    return 0;
                }
                finally
                {
                    try { if (config.Output.JsonState) { SaveState(statePath, state); } } catch { }
                    try { mutex.ReleaseMutex(); } catch { }
                    WriteLog(logPath, config.Output, "INFO", "Monitor Strict Evidence encerrado com liberacao dos recursos.");
                }
            }
        }

        private static StrictPolicy BuildPolicy(MonitorConfiguration config)
        {
            return new StrictPolicy { VerificationRounds = config.Monitoring.VerificationRounds, RequiredPositiveRounds = config.Monitoring.RequiredPositiveRounds, RequiredConfirmedCycles = config.Monitoring.RequiredConfirmedCycles, RequireCapturedArpRequest = true, RequireCorrelatedArpResponses = true, FailClosedWithoutCapture = true, DetectProxyArp = config.Monitoring.DetectProxyArp };
        }

        private static DetectionHealth BuildHealth(string tshark, string captureInterface, SelectedNetwork selected, bool activeProbeEnabled)
        {
            bool tsharkAvailable = !String.IsNullOrWhiteSpace(tshark) && File.Exists(tshark);
            bool captureAvailable = tsharkAvailable && !String.IsNullOrWhiteSpace(captureInterface);
            bool interfaceReady = selected != null && selected.ConfiguredNetworkReachable && StrictEvidenceDecisionEngine.NormalizeAndValidateMac(selected.Mac) != null;
            bool strictReady = captureAvailable && interfaceReady && activeProbeEnabled;
            string error = strictReady ? String.Empty : (!activeProbeEnabled ? "sondagem ARP ativa desabilitada" : (!interfaceReady ? "interface/CIDR/MAC local invalido" : "TShark/Npcap/interface de captura indisponivel"));
            return new DetectionHealth { TsharkAvailable = tsharkAvailable, NpcapAvailable = captureAvailable, CaptureAvailable = captureAvailable, CaptureRunning = false, InterfaceReady = interfaceReady, StrictVerificationReady = strictReady, LastError = error };
        }

        private static List<NativeSnapshotRow> EvaluateCycle(NativeState state, SelectedNetwork selected, HashSet<string> targets, HashSet<string> excludedMacs, Dictionary<string, HashSet<string>> trustedPairs, HashSet<string> trustedVirtualIps, HashSet<string> trustedMacs, string tshark, string captureInterface, DetectionHealth health, StrictPolicy policy, MonitorConfiguration config, string cycleId, long cycle, EventWaitHandle stopEvent, string logPath)
        {
            DateTime windowUtc = DateTime.UtcNow.AddMinutes(-config.Monitoring.EvidenceWindowMinutes);
            List<NativeObservation> recent = state.History.Where(delegate(NativeObservation item) { return item.TimeUtc >= windowUtc; }).ToList();
            var ipKeys = new HashSet<string>(recent.Select(delegate(NativeObservation item) { return item.IP; }), StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, ConflictDecisionMemory> item in state.Decisions) { if (item.Value != null && item.Value.WasConfirmed) { ipKeys.Add(item.Key); } }
            var macToIps = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (NativeObservation observation in recent.Where(delegate(NativeObservation item) { return item.Source == "ARP-Packet"; }))
            {
                HashSet<string> ips; if (!macToIps.TryGetValue(observation.MAC, out ips)) { ips = new HashSet<string>(StringComparer.OrdinalIgnoreCase); macToIps[observation.MAC] = ips; } ips.Add(observation.IP);
            }
            var rows = new List<NativeSnapshotRow>();
            foreach (string ip in ipKeys.OrderBy(delegate(string value) { return IpToUInt32(IPAddress.Parse(value)); }))
            {
                if (stopEvent.WaitOne(0)) { break; }
                List<NativeObservation> events = recent.Where(delegate(NativeObservation item) { return String.Equals(item.IP, ip, StringComparison.OrdinalIgnoreCase); }).OrderBy(delegate(NativeObservation item) { return item.TimeUtc; }).ToList();
                List<NativeObservation> currentEvents = events.Where(delegate(NativeObservation item) { return item.Cycle == cycle; }).ToList();
                string[] historicalMacs = events.Select(delegate(NativeObservation item) { return item.MAC; }).Where(delegate(string mac) { return StrictEvidenceDecisionEngine.NormalizeAndValidateMac(mac) != null; }).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(delegate(string mac) { return mac; }).ToArray();
                string[] currentMacs = currentEvents.Select(delegate(NativeObservation item) { return item.MAC; }).Where(delegate(string mac) { return StrictEvidenceDecisionEngine.NormalizeAndValidateMac(mac) != null; }).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(delegate(string mac) { return mac; }).ToArray();
                ConflictDecisionMemory memory; if (!state.Decisions.TryGetValue(ip, out memory) || memory == null) { memory = new ConflictDecisionMemory(); state.Decisions[ip] = memory; }
                bool ambiguous = historicalMacs.Length > 1;
                bool verificationRequired = ambiguous || memory.WasConfirmed;
                string evidenceId = NewEvidenceId();
                var evidence = new ConflictEvidence
                {
                    TargetIp = ip, InterfaceId = selected.InterfaceIndex.ToString(CultureInfo.InvariantCulture), InterfaceName = selected.Adapter.Name, MonitorIp = selected.Address.ToString(), EvidenceId = evidenceId, Cycle = cycle,
                    VerificationRequired = verificationRequired, DiscoveryAmbiguous = ambiguous, OnlyHistoricalChange = ambiguous && currentMacs.Length <= 1,
                    CacheOnlyAmbiguity = ambiguous && currentEvents.Where(delegate(NativeObservation item) { return item.Source == "ARP-Packet"; }).Select(delegate(NativeObservation item) { return item.MAC; }).Distinct(StringComparer.OrdinalIgnoreCase).Count() <= 1,
                    InterfaceValidation = selected.ConfiguredNetworkReachable, Health = health, CurrentObservedMacs = currentMacs.ToList(), HistoricalMacs = historicalMacs.ToList(), ObservedGratuitousArp = currentEvents.Any(delegate(NativeObservation item) { return item.Gratuitous; })
                };
                evidence.PossibleProxyArp = historicalMacs.Any(delegate(string mac) { HashSet<string> ips; return macToIps.TryGetValue(mac, out ips) && ips.Count >= config.Monitoring.ProxyArpIpThreshold; });

                if (verificationRequired)
                {
                    WriteLog(logPath, config.Output, "VERIFY", "EvidenceId=" + evidenceId + "; iniciando Strict Verification para " + ip + ".");
                    if (health.StrictVerificationReady)
                    {
                        evidence.Rounds = PerformStrictVerification(tshark, captureInterface, selected, ip, config, excludedMacs, health, stopEvent, evidenceId, logPath);
                        state.Metrics.VerificationRequests += evidence.Rounds.Count;
                        state.Metrics.PositiveVerificationRounds += evidence.Rounds.Count(delegate(VerificationRoundEvidence round) { return round.PairKey != null && round.RequestCorrelationValid; });
                        evidence.CaptureFailure = evidence.Rounds.Any(delegate(VerificationRoundEvidence round) { return !round.CaptureHealthy; });
                    }
                }

                string[] decisionMacs = historicalMacs.Concat(evidence.Rounds.SelectMany(delegate(VerificationRoundEvidence round) { return round.ValidMacs; })).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                evidence.TrustedPair = IsTrusted(ip, decisionMacs, trustedPairs, trustedVirtualIps, trustedMacs);
                evidence.GatewayMacDetected = !String.IsNullOrWhiteSpace(selected.GatewayMac) && decisionMacs.Contains(selected.GatewayMac, StringComparer.OrdinalIgnoreCase);
                DetectionDecision decision = StrictEvidenceDecisionEngine.EvaluateConflict(evidence, policy, memory);
                UpdateMetrics(state.Metrics, evidence, decision);
                LogDecision(logPath, config.Output, evidence, decision);
                rows.Add(BuildSnapshotRow(events, currentMacs, historicalMacs, selected, evidence, decision));
                if (verificationRequired && config.Monitoring.ArpProbeRateLimitMs > 0) { WaitCancelable(stopEvent, config.Monitoring.ArpProbeRateLimitMs); }
            }
            state.Metrics.TotalIpsObserved = ipKeys.Count;
            return rows.OrderBy(delegate(NativeSnapshotRow row) { return StatusRank(row.Status); }).ThenBy(delegate(NativeSnapshotRow row) { return IpToUInt32(IPAddress.Parse(row.IP)); }).ToList();
        }

        private static List<VerificationRoundEvidence> PerformStrictVerification(string tshark, string captureInterface, SelectedNetwork selected, string targetIp, MonitorConfiguration config, HashSet<string> excludedMacs, DetectionHealth health, EventWaitHandle stopEvent, string evidenceId, string logPath)
        {
            var rounds = new List<VerificationRoundEvidence>();
            int count = Math.Max(1, config.Monitoring.VerificationRounds);
            for (int roundNumber = 1; roundNumber <= count && !stopEvent.WaitOne(0); roundNumber++)
            {
                VerificationRoundEvidence round = CaptureVerificationRound(tshark, captureInterface, selected, targetIp, roundNumber, config, excludedMacs, stopEvent);
                rounds.Add(round);
                if (!round.CaptureHealthy) { health.StrictVerificationReady = false; health.LastError = round.Error; }
                if (round.RequestObserved) { WriteLog(logPath, config.Output, "ARP", "EvidenceId=" + evidenceId + "; Round=" + roundNumber + "; requisicao correlacionavel observada: Who has " + targetIp + "."); }
                foreach (KeyValuePair<string, int> response in round.ResponsesByMac) { WriteLog(logPath, config.Output, "ARP", "EvidenceId=" + evidenceId + "; Round=" + roundNumber + "; " + targetIp + " is-at " + response.Key + "; respostas=" + response.Value + "."); }
                WriteLog(logPath, config.Output, "VERIFY", "EvidenceId=" + evidenceId + "; Round " + roundNumber + "/" + count + ": " + (round.PairKey != null && round.RequestCorrelationValid ? "POSITIVO respondentes=" + String.Join("+", round.ValidMacs) : "negativo/inconclusivo") + ".");
                if (!round.CaptureHealthy) { break; }
                if (roundNumber < count && config.Monitoring.ArpProbeRateLimitMs > 0) { WaitCancelable(stopEvent, config.Monitoring.ArpProbeRateLimitMs); }
            }
            return rounds;
        }

        private static VerificationRoundEvidence CaptureVerificationRound(string tshark, string captureInterface, SelectedNetwork selected, string targetIp, int roundNumber, MonitorConfiguration config, HashSet<string> excludedMacs, EventWaitHandle stopEvent)
        {
            string localMac = StrictEvidenceDecisionEngine.NormalizeAndValidateMac(selected.Mac);
            var evidence = new VerificationRoundEvidence { Round = roundNumber, InterfaceValid = selected.ConfiguredNetworkReachable && localMac != null, CaptureHealthy = false };
            if (localMac == null) { evidence.Error = "MAC local da interface selecionada e invalido"; return evidence; }
            int durationSeconds = Math.Max(2, (int)Math.Ceiling((config.Monitoring.CaptureWarmupMilliseconds + config.Monitoring.ArpResponseWindowMs + 750) / 1000.0));
            using (TSharkCapture capture = StartTSharkCapture(tshark, captureInterface, durationSeconds, targetIp))
            {
                if (capture == null) { evidence.Error = "nao foi possivel iniciar a captura da rodada"; return evidence; }
                WaitCancelable(stopEvent, Math.Max(100, config.Monitoring.CaptureWarmupMilliseconds));
                DateTime probeUtc = DateTime.UtcNow;
                evidence.RequestTimestampUtc = probeUtc; evidence.ResponseWindowStartUtc = probeUtc; evidence.ResponseWindowEndUtc = probeUtc.AddMilliseconds(config.Monitoring.ArpResponseWindowMs);
                ClearSingleNeighbor(selected.InterfaceIndex, targetIp);
                SendArpProbe(targetIp, selected.Address.ToString());
                try { using (var ping = new Ping()) { ping.Send(targetIp, Math.Max(50, config.Monitoring.PingTimeoutMs)); } } catch { }
                int exitCode; string error; bool completed = CompleteCapture(capture, 30000, out exitCode, out error);
                evidence.CaptureHealthy = completed && exitCode == 0;
                evidence.Error = evidence.CaptureHealthy ? null : (String.IsNullOrWhiteSpace(error) ? "captura interrompida" : FirstLine(error));
                List<ArpPacket> packets = capture.Lines.Select(ParseArpPacket).Where(delegate(ArpPacket packet) { return packet != null; }).OrderBy(delegate(ArpPacket packet) { return packet.TimeUtc; }).ToList();
                ArpPacket request = packets.FirstOrDefault(delegate(ArpPacket packet) { return packet.Opcode == 1 && String.Equals(packet.SourceIp, selected.Address.ToString(), StringComparison.OrdinalIgnoreCase) && String.Equals(packet.TargetIp, targetIp, StringComparison.OrdinalIgnoreCase) && String.Equals(StrictEvidenceDecisionEngine.NormalizeAndValidateMac(packet.SourceMac), localMac, StringComparison.OrdinalIgnoreCase) && packet.TimeUtc >= probeUtc.AddMilliseconds(-100) && packet.TimeUtc <= probeUtc.AddMilliseconds(Math.Max(1000, config.Monitoring.ArpResponseWindowMs)); });
                evidence.RequestObserved = request != null;
                if (request != null)
                {
                    evidence.RequestTimestampUtc = request.TimeUtc; evidence.ResponseWindowStartUtc = request.TimeUtc; evidence.ResponseWindowEndUtc = request.TimeUtc.AddMilliseconds(config.Monitoring.ArpResponseWindowMs);
                    foreach (ArpPacket reply in packets.Where(delegate(ArpPacket packet) { return packet.Opcode == 2 && packet.TimeUtc >= evidence.ResponseWindowStartUtc && packet.TimeUtc <= evidence.ResponseWindowEndUtc && String.Equals(packet.SourceIp, targetIp, StringComparison.OrdinalIgnoreCase) && String.Equals(packet.TargetIp, selected.Address.ToString(), StringComparison.OrdinalIgnoreCase) && String.Equals(StrictEvidenceDecisionEngine.NormalizeAndValidateMac(packet.TargetMac), localMac, StringComparison.OrdinalIgnoreCase); }))
                    {
                        string mac = StrictEvidenceDecisionEngine.NormalizeAndValidateMac(reply.SourceMac); if (mac == null || String.Equals(mac, localMac, StringComparison.OrdinalIgnoreCase) || excludedMacs.Contains(mac)) { continue; }
                        if (!evidence.CorrelatedMacs.Contains(mac, StringComparer.OrdinalIgnoreCase)) { evidence.CorrelatedMacs.Add(mac); }
                        int responses; evidence.ResponsesByMac.TryGetValue(mac, out responses); evidence.ResponsesByMac[mac] = responses + 1;
                    }
                }
                evidence.RequestCorrelationValid = evidence.RequestObserved && evidence.CorrelatedMacs.Count > 0;
                evidence.GratuitousOnly = !evidence.RequestObserved && packets.Any(delegate(ArpPacket packet) { return packet.Gratuitous; });
            }
            return evidence;
        }

        private static NativeSnapshotRow BuildSnapshotRow(List<NativeObservation> events, string[] currentMacs, string[] historicalMacs, SelectedNetwork selected, ConflictEvidence evidence, DetectionDecision decision)
        {
            string[] proofMacs = evidence.Rounds.Where(delegate(VerificationRoundEvidence round) { return round.RequestCorrelationValid; }).SelectMany(delegate(VerificationRoundEvidence round) { return round.ValidMacs; }).GroupBy(delegate(string mac) { return mac; }, StringComparer.OrdinalIgnoreCase).Where(delegate(IGrouping<string, string> group) { return group.Count() >= decision.RequiredRounds; }).Select(delegate(IGrouping<string, string> group) { return group.Key; }).OrderBy(delegate(string mac) { return mac; }).ToArray();
            string[] displayMacs = decision.State == StrictDetectionState.CONFIRMED && proofMacs.Length >= 2 ? proofMacs : (currentMacs.Length > 0 ? currentMacs : historicalMacs);
            int transitions = 0; string previous = null;
            foreach (NativeObservation item in events) { if (previous != null && !String.Equals(previous, item.MAC, StringComparison.OrdinalIgnoreCase)) { transitions++; } previous = item.MAC; }
            int score = Math.Min(100, decision.PositiveRounds * 30 + decision.ConfirmedCycles * 20 + (decision.RequestCorrelationValid ? 10 : 0));
            return new NativeSnapshotRow
            {
                TimestampUtc = DateTime.UtcNow, IP = evidence.TargetIp, Hostname = String.Empty, Status = decision.State.ToString(), MACs = String.Join(", ", displayMacs),
                MACDetails = decision.State == StrictDetectionState.CONFIRMED ? displayMacs.Length + " MACs com prova estrita: " + String.Join(" + ", displayMacs) : (historicalMacs.Length > 1 ? "Mudanca de associacao observada — conflito nao confirmado" : "Associacao atual sem prova de conflito"),
                MacCount = displayMacs.Length, Observations = events.Count, Transitions = transitions,
                DirectArpMacCount = events.Where(delegate(NativeObservation item) { return item.Source == "ARP-Packet"; }).Select(delegate(NativeObservation item) { return item.MAC; }).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
                ActiveProbeMacCount = evidence.Rounds.SelectMany(delegate(VerificationRoundEvidence round) { return round.CorrelatedMacs; }).Distinct(StringComparer.OrdinalIgnoreCase).Count(), MappingMismatch = evidence.TrustedPair,
                FirstSeen = events.Count > 0 ? events.First().TimeUtc.ToLocalTime() : DateTime.Now, LastSeen = events.Count > 0 ? events.Last().TimeUtc.ToLocalTime() : DateTime.Now, Reason = decision.Reason,
                Interface = selected.Adapter.Name + " (#" + selected.InterfaceIndex + ")", MonitorIp = selected.Address.ToString(), RequestObserved = decision.RequestObserved, PositiveRounds = decision.PositiveRounds, RequiredRounds = decision.RequiredRounds,
                ConfirmedCycles = decision.ConfirmedCycles, RequiredCycles = decision.RequiredCycles, CorrelatedArpReplies = decision.CorrelatedArpReplies, ProxyArpRisk = decision.PossibleProxyArp,
                GatewayMac = decision.GatewayMacDetected, TrustedPair = decision.TrustedPair, CaptureHealthy = decision.MonitoringHealthy, ConfidenceScore = score, EvidenceId = decision.EvidenceId, EvidenceHash = decision.EvidenceHash, EvidenceQuality = decision.EvidenceQuality
            };
        }

        private static void UpdateMetrics(DetectionMetrics metrics, ConflictEvidence evidence, DetectionDecision decision)
        {
            if (decision.State == StrictDetectionState.CONFIRMED) { metrics.ConfirmedConflicts++; }
            if (decision.State == StrictDetectionState.MONITORING_LIMITED) { metrics.MonitoringLimitedEvents++; }
            if (evidence.OnlyHistoricalChange && decision.State != StrictDetectionState.CONFIRMED) { metrics.RejectedHistoricalTransitions++; }
            if ((evidence.PossibleProxyArp || evidence.GatewayMacDetected) && decision.State != StrictDetectionState.CONFIRMED) { metrics.RejectedProxyArp++; }
        }

        private static void LogDecision(string logPath, OutputConfiguration output, ConflictEvidence evidence, DetectionDecision decision)
        {
            string details = "EvidenceId=" + decision.EvidenceId + "; IP=" + decision.TargetIp + "; State=" + decision.State + "; PositiveRounds=" + decision.PositiveRounds + "/" + decision.RequiredRounds + "; ConfirmedCycles=" + decision.ConfirmedCycles + "/" + decision.RequiredCycles + "; EvidenceHash=" + decision.EvidenceHash + "; Reason=" + decision.Reason;
            if (decision.State == StrictDetectionState.CONFIRMED) { WriteLog(logPath, output, "CONFLICT", details); }
            else if (decision.ConflictResolved) { WriteLog(logPath, output, "STATE", "CONFLICT_RESOLVED; " + details); }
            else if (decision.State == StrictDetectionState.MONITORING_LIMITED) { WriteLog(logPath, output, "LIMITED", details); }
            else if (evidence.VerificationRequired) { WriteLog(logPath, output, "STATE", "Conflict rejected; " + details); }
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
            if (config.Network.TrustedVirtualIps == null) { config.Network.TrustedVirtualIps = new string[0]; }
            if (config.Network.TrustedMacs == null) { config.Network.TrustedMacs = new string[0]; }
            return config;
        }

        public static List<string> ValidateConfiguration(MonitorConfiguration config)
        {
            var errors = new List<string>();
            if (!String.Equals(config.Monitoring.DetectionMode, "StrictEvidence", StringComparison.OrdinalIgnoreCase)) { errors.Add("Monitoring.DetectionMode deve permanecer StrictEvidence."); }
            if (!config.Monitoring.RequireCapturedArpRequest || !config.Monitoring.RequireCorrelatedArpResponses || !config.Monitoring.FailClosedWithoutCapture) { errors.Add("STRICT DETECTION SAFETY DISABLED: requisitos de correlacao/fail-closed nao podem ser desativados."); }
            if (config.Network.MaxHosts < 1 || config.Network.MaxHosts > 65534) { errors.Add("Network.MaxHosts deve estar entre 1 e 65534."); }
            if (!String.IsNullOrWhiteSpace(config.Network.CIDR) && !IsValidCidr(config.Network.CIDR)) { errors.Add("Network.CIDR deve usar IPv4/prefixo, por exemplo 192.168.1.0/24."); }
            foreach (string ip in config.Network.ExcludedIPs.Concat(config.Network.TrustedVirtualIps)) { IPAddress parsed; if (!IPAddress.TryParse(ip, out parsed) || parsed.AddressFamily != AddressFamily.InterNetwork) { errors.Add("IPv4 invalido na configuracao: " + ip); } }
            foreach (string mac in config.Network.ExcludedMACs.Concat(config.Network.TrustedMacs)) { if (StrictEvidenceDecisionEngine.NormalizeAndValidateMac(mac) == null) { errors.Add("MAC invalido na configuracao: " + mac); } }
            foreach (string pair in config.Network.TrustedPairs) { string[] parts = (pair ?? String.Empty).Split('|'); IPAddress parsed; if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out parsed) || StrictEvidenceDecisionEngine.NormalizeAndValidateMac(parts[1]) == null) { errors.Add("TrustedPairs contem par invalido: " + pair); } }
            if (config.Monitoring.IntervalSeconds < 1) { errors.Add("Monitoring.IntervalSeconds deve ser maior que zero."); }
            if (config.Monitoring.VerificationRounds < 2 || config.Monitoring.VerificationRounds > 5) { errors.Add("Monitoring.VerificationRounds deve estar entre 2 e 5."); }
            if (config.Monitoring.RequiredPositiveRounds < 2 || config.Monitoring.RequiredPositiveRounds > config.Monitoring.VerificationRounds) { errors.Add("Monitoring.RequiredPositiveRounds deve estar entre 2 e VerificationRounds."); }
            if (config.Monitoring.RequiredConfirmedCycles < 2 || config.Monitoring.RequiredConfirmedCycles > 5) { errors.Add("Monitoring.RequiredConfirmedCycles deve estar entre 2 e 5."); }
            if (config.Monitoring.ArpResponseWindowMs < 500 || config.Monitoring.ArpResponseWindowMs > 5000) { errors.Add("Monitoring.ArpResponseWindowMs deve estar entre 500 e 5000."); }
            if (config.Monitoring.MaxConcurrentVerifications != 1) { errors.Add("Monitoring.MaxConcurrentVerifications deve ser 1 nesta edicao para evitar concorrencia por IP e excesso de ARP."); }
            if (config.Monitoring.ArpProbeRateLimitMs < 100) { errors.Add("Monitoring.ArpProbeRateLimitMs deve ser pelo menos 100."); }
            if (config.Monitoring.PingTimeoutMs < 50) { errors.Add("Monitoring.PingTimeoutMs deve ser pelo menos 50."); }
            if (config.Output.LogMaxMB < 1) { errors.Add("Output.LogMaxMB deve ser maior que zero."); }
            return errors;
        }

        private static SelectedNetwork SelectNetwork(int requestedIndex, string configuredCidr)
        {
            var candidates = new List<Tuple<SelectedNetwork, int>>();
            foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (adapter.OperationalStatus != OperationalStatus.Up || adapter.NetworkInterfaceType == NetworkInterfaceType.Loopback) { continue; }
                IPInterfaceProperties properties; IPv4InterfaceProperties ipv4;
                try { properties = adapter.GetIPProperties(); ipv4 = properties.GetIPv4Properties(); } catch { continue; }
                if (ipv4 == null) { continue; }
                IPAddress gateway = properties.GatewayAddresses.Select(delegate(GatewayIPAddressInformation item) { return item.Address; }).FirstOrDefault(delegate(IPAddress value) { return value != null && value.AddressFamily == AddressFamily.InterNetwork && !value.Equals(IPAddress.Any); });
                foreach (UnicastIPAddressInformation unicast in properties.UnicastAddresses)
                {
                    if (unicast.Address.AddressFamily != AddressFamily.InterNetwork || IPAddress.IsLoopback(unicast.Address) || unicast.Address.ToString().StartsWith("169.254.", StringComparison.Ordinal)) { continue; }
                    bool matches = String.IsNullOrWhiteSpace(configuredCidr) || IsIpInCidr(unicast.Address, configuredCidr);
                    bool ethernet = adapter.NetworkInterfaceType == NetworkInterfaceType.Ethernet || adapter.NetworkInterfaceType == NetworkInterfaceType.GigabitEthernet;
                    bool wireless = adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211;
                    string text = (adapter.Name + " " + adapter.Description).ToLowerInvariant();
                    bool virtualAdapter = new[] { "vpn", "tap", "tun", "hyper-v", "vmware", "virtualbox", "wsl", "docker", "bluetooth", "zerotier", "tailscale", "virtual" }.Any(delegate(string term) { return text.Contains(term); });
                    int score = StrictEvidenceDecisionEngine.InterfaceCandidateScore(adapter.Name, adapter.Description, ethernet, wireless, gateway != null, adapter.Speed, virtualAdapter) + (matches ? 1000 : -2000);
                    var selected = new SelectedNetwork { Adapter = adapter, Address = unicast.Address, PrefixLength = MaskToPrefix(unicast.IPv4Mask), InterfaceIndex = ipv4.Index, Mac = StrictEvidenceDecisionEngine.NormalizeAndValidateMac(adapter.GetPhysicalAddress().ToString()), Gateway = gateway, ConfiguredNetworkReachable = matches };
                    selected.Cidr = NetworkCidr(unicast.Address, selected.PrefixLength); selected.SelectionReason = "score=" + score + "; physical=" + (!virtualAdapter) + "; gateway=" + (gateway != null) + "; configuredCidrReachable=" + matches;
                    candidates.Add(Tuple.Create(selected, score));
                }
            }
            Tuple<SelectedNetwork, int> chosen = requestedIndex > 0 ? candidates.FirstOrDefault(delegate(Tuple<SelectedNetwork, int> item) { return item.Item1.InterfaceIndex == requestedIndex; }) : candidates.OrderByDescending(delegate(Tuple<SelectedNetwork, int> item) { return item.Item2; }).FirstOrDefault();
            if (chosen == null) { throw new InvalidOperationException(requestedIndex > 0 ? "A interface configurada nao esta ativa ou nao possui IPv4." : "Nenhuma interface IPv4 adequada foi encontrada."); }
            SelectedNetwork result = chosen.Item1;
            if (result.Gateway != null) { result.GatewayMac = ReadArpTable(result.InterfaceIndex).Where(delegate(ArpEntry entry) { return String.Equals(entry.IP, result.Gateway.ToString(), StringComparison.OrdinalIgnoreCase); }).Select(delegate(ArpEntry entry) { return StrictEvidenceDecisionEngine.NormalizeAndValidateMac(entry.MAC); }).FirstOrDefault(delegate(string mac) { return mac != null; }); }
            return result;
        }

        private static string ResolveOutputRoot(MonitorConfiguration config) { return !String.IsNullOrWhiteSpace(config.Output.Directory) ? Path.GetFullPath(Environment.ExpandEnvironmentVariables(config.Output.Directory)) : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IPConflictMonitor"); }
        private static int MaskToPrefix(IPAddress mask) { if (mask == null) { return 24; } int prefix = 0; foreach (byte value in mask.GetAddressBytes()) { for (int bit = 7; bit >= 0; bit--) { if ((value & (1 << bit)) != 0) { prefix++; } else { return prefix; } } } return prefix; }
        private static bool IsValidCidr(string cidr) { string[] parts = (cidr ?? String.Empty).Split('/'); IPAddress address; int prefix; return parts.Length == 2 && IPAddress.TryParse(parts[0], out address) && address.AddressFamily == AddressFamily.InterNetwork && Int32.TryParse(parts[1], out prefix) && prefix >= 0 && prefix <= 32; }
        private static bool IsIpInCidr(IPAddress address, string cidr) { if (!IsValidCidr(cidr)) { return false; } string[] parts = cidr.Split('/'); int prefix = Int32.Parse(parts[1]); uint mask = prefix == 0 ? 0U : UInt32.MaxValue << (32 - prefix); return (IpToUInt32(address) & mask) == (IpToUInt32(IPAddress.Parse(parts[0])) & mask); }
        private static string NetworkCidr(IPAddress address, int prefix) { uint mask = prefix == 0 ? 0U : UInt32.MaxValue << (32 - prefix); return UInt32ToIp(IpToUInt32(address) & mask) + "/" + prefix; }

        private static List<string> BuildTargets(string address, int prefix, int maxHosts, string[] exclusions, out string cidr)
        {
            uint value = IpToUInt32(IPAddress.Parse(address)); uint mask = prefix == 0 ? 0U : UInt32.MaxValue << (32 - prefix); uint network = value & mask; uint broadcast = network | ~mask;
            ulong first = prefix <= 30 ? (ulong)network + 1UL : network; ulong last = prefix <= 30 ? (ulong)broadcast - 1UL : broadcast; ulong count = last >= first ? last - first + 1UL : 0UL;
            if (count > (ulong)maxHosts) { throw new InvalidOperationException("Rede " + UInt32ToIp(network) + "/" + prefix + " possui " + count + " hosts e excede MaxHosts=" + maxHosts + "."); }
            var excluded = new HashSet<string>(exclusions ?? new string[0], StringComparer.OrdinalIgnoreCase); var targets = new List<string>();
            for (ulong current = first; current <= last; current++) { string ip = UInt32ToIp((uint)current); if (!excluded.Contains(ip)) { targets.Add(ip); } }
            cidr = UInt32ToIp(network) + "/" + prefix; return targets;
        }

        private static uint IpToUInt32(IPAddress address) { byte[] bytes = address.GetAddressBytes(); return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3]; }
        private static string UInt32ToIp(uint value) { return ((value >> 24) & 255) + "." + ((value >> 16) & 255) + "." + ((value >> 8) & 255) + "." + (value & 255); }

        private static List<ArpEntry> ReadArpTable(int interfaceIndex)
        {
            int size = 0; GetIpNetTable(IntPtr.Zero, ref size, false); if (size <= 4) { return new List<ArpEntry>(); } IntPtr buffer = Marshal.AllocHGlobal(size);
            try
            {
                int result = GetIpNetTable(buffer, ref size, false); if (result != 0) { throw new InvalidOperationException("GetIpNetTable falhou com codigo " + result + "."); }
                int count = Marshal.ReadInt32(buffer); int rowSize = Marshal.SizeOf(typeof(MibIpNetRow)); IntPtr pointer = IntPtr.Add(buffer, 4); var output = new List<ArpEntry>();
                for (int index = 0; index < count; index++) { var row = (MibIpNetRow)Marshal.PtrToStructure(pointer, typeof(MibIpNetRow)); pointer = IntPtr.Add(pointer, rowSize); if (row.dwIndex != interfaceIndex || row.dwPhysAddrLen < 6 || row.bPhysAddr == null || row.dwType == 2) { continue; } string mac = String.Join(":", row.bPhysAddr.Take(6).Select(delegate(byte item) { return item.ToString("X2"); }).ToArray()); output.Add(new ArpEntry { IP = new IPAddress(row.dwAddr).ToString(), MAC = mac, InterfaceIndex = row.dwIndex, NativeRow = row }); }
                return output;
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }

        private static void AddArpObservations(List<NativeObservation> history, IEnumerable<ArpEntry> entries, HashSet<string> targets, HashSet<string> excludedMacs, string source, string cycleId, long cycle)
        {
            DateTime now = DateTime.UtcNow; foreach (ArpEntry entry in entries) { string mac = StrictEvidenceDecisionEngine.NormalizeAndValidateMac(entry.MAC); if (!targets.Contains(entry.IP) || mac == null || excludedMacs.Contains(mac)) { continue; } history.Add(new NativeObservation { TimeUtc = now, IP = entry.IP, MAC = mac, Source = source, CycleId = cycleId, Cycle = cycle }); }
        }

        private static int CalculateDiscoveryCaptureSeconds(int targetCount, int concurrency, int timeoutMs, int warmupMs, int configuredSeconds)
        {
            int workers = Math.Max(1, Math.Min(256, concurrency));
            long batches = targetCount <= 0 ? 1L : ((long)targetCount + workers - 1L) / workers;
            long estimatedMs = Math.Max(0, warmupMs) + batches * Math.Max(50, timeoutMs) + 1500L;
            int estimatedSeconds = (int)Math.Min(120L, Math.Max(2L, (estimatedMs + 999L) / 1000L));
            return Math.Max(Math.Max(2, configuredSeconds), estimatedSeconds);
        }

        private static int PingSweep(List<string> targets, int timeout, int concurrency)
        {
            int reachable = 0; var options = new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Math.Min(256, concurrency)) };
            Parallel.ForEach(targets, options, delegate(string target) { try { using (var ping = new Ping()) { PingReply reply = ping.Send(target, Math.Max(50, timeout)); if (reply != null && reply.Status == IPStatus.Success) { Interlocked.Increment(ref reachable); } } } catch { } }); return reachable;
        }

        private static string FindTShark(string configured)
        {
            var candidates = new List<string>(); if (!String.IsNullOrWhiteSpace(configured)) { candidates.Add(Environment.ExpandEnvironmentVariables(configured)); }
            candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Wireshark", "tshark.exe")); string x86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86); if (!String.IsNullOrWhiteSpace(x86)) { candidates.Add(Path.Combine(x86, "Wireshark", "tshark.exe")); }
            return candidates.FirstOrDefault(File.Exists);
        }

        private static string FindTSharkInterface(string tshark, NetworkInterface adapter)
        {
            try
            {
                var info = new ProcessStartInfo { FileName = tshark, Arguments = "-D", UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                using (Process process = Process.Start(info))
                {
                    string output = process.StandardOutput.ReadToEnd(); string error = process.StandardError.ReadToEnd(); if (!process.WaitForExit(5000)) { process.Kill(); return null; } if (process.ExitCode != 0) { return null; }
                    foreach (string line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (line.IndexOf(adapter.Id, StringComparison.OrdinalIgnoreCase) < 0 && line.IndexOf(adapter.Name, StringComparison.OrdinalIgnoreCase) < 0 && line.IndexOf(adapter.Description, StringComparison.OrdinalIgnoreCase) < 0) { continue; }
                        int dot = line.IndexOf('.'); int number; if (dot > 0 && Int32.TryParse(line.Substring(0, dot).Trim(), out number) && number > 0) { return number.ToString(CultureInfo.InvariantCulture); }
                    }
                }
            }
            catch { }
            return null;
        }

        private static TSharkCapture StartTSharkCapture(string tshark, string interfaceNumber, int seconds, string targetIp)
        {
            int parsedInterface; IPAddress parsedTarget;
            if (String.IsNullOrWhiteSpace(tshark) || !File.Exists(tshark) || !Int32.TryParse(interfaceNumber, out parsedInterface) || parsedInterface < 1) { return null; }
            if (!String.IsNullOrWhiteSpace(targetIp) && (!IPAddress.TryParse(targetIp, out parsedTarget) || parsedTarget.AddressFamily != AddressFamily.InterNetwork)) { return null; }
            try
            {
                string captureFilter = String.IsNullOrWhiteSpace(targetIp) ? "arp" : "arp and host " + targetIp;
                var capture = new TSharkCapture();
                var info = new ProcessStartInfo { FileName = tshark, Arguments = "-l -n -i " + parsedInterface + " -a duration:" + Math.Max(2, seconds) + " -f \"" + captureFilter + "\" -T fields -E separator=| -e frame.time_epoch -e arp.opcode -e arp.src.proto_ipv4 -e arp.src.hw_mac -e arp.dst.proto_ipv4 -e arp.dst.hw_mac", UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                capture.Process = new Process { StartInfo = info, EnableRaisingEvents = true };
                capture.Process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs eventArgs) { capture.AddLine(eventArgs.Data); };
                capture.Process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs eventArgs) { capture.AddError(eventArgs.Data); };
                if (!capture.Process.Start()) { capture.Dispose(); return null; } capture.Process.BeginOutputReadLine(); capture.Process.BeginErrorReadLine(); return capture;
            }
            catch { return null; }
        }

        private static bool CompleteCapture(TSharkCapture capture, int timeoutMs, out int exitCode, out string error)
        {
            exitCode = -1; error = String.Empty; if (capture == null || capture.Process == null) { error = "captura ausente"; return false; }
            try { if (!capture.Process.WaitForExit(timeoutMs)) { capture.Process.Kill(); capture.Process.WaitForExit(1500); error = "timeout da captura"; return false; } capture.Process.WaitForExit(); exitCode = capture.Process.ExitCode; error = capture.ErrorText; return true; }
            catch (Exception exception) { error = exception.Message; return false; }
        }

        private static void CompleteDiscoveryCapture(TSharkCapture capture, List<NativeObservation> history, HashSet<string> targets, HashSet<string> excludedMacs, string cycleId, long cycle, DetectionHealth health, string logPath, OutputConfiguration output)
        {
            int exit; string error; bool completed = CompleteCapture(capture, 30000, out exit, out error); health.CaptureRunning = false;
            if (!completed || exit != 0) { health.StrictVerificationReady = false; health.LastError = String.IsNullOrWhiteSpace(error) ? "TShark terminou inesperadamente" : FirstLine(error); WriteLog(logPath, output, "WARN", "Captura de discovery falhou: " + health.LastError); return; }
            health.StrictVerificationReady = health.CaptureAvailable && health.InterfaceReady; health.LastError = String.Empty;
            int added = 0; foreach (ArpPacket packet in capture.Lines.Select(ParseArpPacket).Where(delegate(ArpPacket item) { return item != null && item.Opcode == 2; })) { string mac = StrictEvidenceDecisionEngine.NormalizeAndValidateMac(packet.SourceMac); if (!targets.Contains(packet.SourceIp) || mac == null || excludedMacs.Contains(mac)) { continue; } history.Add(new NativeObservation { TimeUtc = packet.TimeUtc, IP = packet.SourceIp, MAC = mac, Source = "ARP-Packet", CycleId = cycleId, Cycle = cycle, Gratuitous = packet.Gratuitous }); added++; health.LastCapturePacketUtc = packet.TimeUtc; }
            WriteLog(logPath, output, "DISCOVERY", "Observacoes ARP espontaneas para selecao de candidatos=" + added + "; nenhuma confirma conflito.");
        }

        private static ArpPacket ParseArpPacket(string line)
        {
            if (String.IsNullOrWhiteSpace(line)) { return null; } string[] parts = line.Split('|'); if (parts.Length < 6) { return null; }
            double epoch; int opcode; if (!Double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out epoch) || !Int32.TryParse(FirstValue(parts[1]), out opcode)) { return null; }
            string sourceIp = FirstValue(parts[2]); string sourceMac = FirstValue(parts[3]); string targetIp = FirstValue(parts[4]); string targetMac = FirstValue(parts[5]);
            DateTime timeUtc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(epoch);
            bool gratuitous = !String.IsNullOrWhiteSpace(sourceIp) && String.Equals(sourceIp, targetIp, StringComparison.OrdinalIgnoreCase);
            return new ArpPacket { TimeUtc = timeUtc, Opcode = opcode, SourceIp = sourceIp, SourceMac = sourceMac, TargetIp = targetIp, TargetMac = targetMac, Gratuitous = gratuitous };
        }

        private static string FirstValue(string value) { if (String.IsNullOrWhiteSpace(value)) { return String.Empty; } return value.Split(',')[0].Trim(); }
        private static string FirstLine(string value) { return (value ?? String.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? String.Empty; }

        private static int ClearDynamicNeighbors(int interfaceIndex, HashSet<string> targetIps)
        {
            int cleared = 0;
            foreach (ArpEntry entry in ReadArpTable(interfaceIndex))
            {
                if (targetIps == null || !targetIps.Contains(entry.IP) || entry.NativeRow.dwType != 3) { continue; }
                MibIpNetRow row = entry.NativeRow;
                if (DeleteIpNetEntry(ref row) == 0) { cleared++; }
            }
            return cleared;
        }
        private static void ClearSingleNeighbor(int interfaceIndex, string targetIp) { ClearDynamicNeighbors(interfaceIndex, new HashSet<string>(new[] { targetIp }, StringComparer.OrdinalIgnoreCase)); }
        private static void RefreshGatewayMac(SelectedNetwork selected)
        {
            if (selected == null || selected.Gateway == null) { return; }
            string gatewayIp = selected.Gateway.ToString();
            string refreshed = ReadArpTable(selected.InterfaceIndex).Where(delegate(ArpEntry entry) { return String.Equals(entry.IP, gatewayIp, StringComparison.OrdinalIgnoreCase); }).Select(delegate(ArpEntry entry) { return StrictEvidenceDecisionEngine.NormalizeAndValidateMac(entry.MAC); }).FirstOrDefault(delegate(string mac) { return mac != null; });
            if (!String.IsNullOrWhiteSpace(refreshed)) { selected.GatewayMac = refreshed; }
        }
        private static void SendArpProbe(string targetIp, string sourceIp) { byte[] buffer = new byte[8]; int length = buffer.Length; uint destination = BitConverter.ToUInt32(IPAddress.Parse(targetIp).GetAddressBytes(), 0); uint source = BitConverter.ToUInt32(IPAddress.Parse(sourceIp).GetAddressBytes(), 0); SendARP(destination, source, buffer, ref length); }

        private static Dictionary<string, HashSet<string>> BuildTrustedPairs(string[] pairs)
        {
            var result = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase); foreach (string pair in pairs ?? new string[0]) { string[] parts = pair.Split('|'); if (parts.Length != 2) { continue; } string mac = StrictEvidenceDecisionEngine.NormalizeAndValidateMac(parts[1]); if (mac == null) { continue; } HashSet<string> values; if (!result.TryGetValue(parts[0], out values)) { values = new HashSet<string>(StringComparer.OrdinalIgnoreCase); result[parts[0]] = values; } values.Add(mac); } return result;
        }

        private static bool IsTrusted(string ip, string[] macs, Dictionary<string, HashSet<string>> pairs, HashSet<string> trustedVirtualIps, HashSet<string> trustedMacs)
        {
            if (trustedVirtualIps.Contains(ip)) { return true; } if (macs.Length > 0 && macs.All(delegate(string mac) { return trustedMacs.Contains(mac); })) { return true; }
            HashSet<string> allowed; return pairs.TryGetValue(ip, out allowed) && macs.Length > 0 && macs.All(delegate(string mac) { return allowed.Contains(mac); });
        }

        private static NativeState LoadState(string path, MonitorConfiguration config, string logPath)
        {
            try { if (!File.Exists(path)) { return new NativeState(); } NativeState state = Json.Deserialize<NativeState>(File.ReadAllText(path, Encoding.UTF8)) ?? new NativeState(); if (state.History == null) { state.History = new List<NativeObservation>(); } if (state.Decisions == null) { state.Decisions = new Dictionary<string, ConflictDecisionMemory>(StringComparer.OrdinalIgnoreCase); } if (state.Metrics == null) { state.Metrics = new DetectionMetrics(); } return state; }
            catch (Exception exception) { WriteLog(logPath, config.Output, "WARN", "Estado anterior ignorado: " + exception.Message); return new NativeState(); }
        }

        private static void SaveState(string path, NativeState state) { state.SavedAtUtc = DateTime.UtcNow; WriteAtomicText(path, Json.Serialize(state), new UTF8Encoding(false)); }
        private static void WriteSnapshot(string path, List<NativeSnapshotRow> rows) { var builder = new StringBuilder(); builder.AppendLine(CsvHeader); foreach (NativeSnapshotRow row in rows) { builder.AppendLine(ToCsv(row)); } WriteAtomicText(path, builder.ToString(), new UTF8Encoding(true)); }

        private static void WriteAtomicText(string path, string content, Encoding encoding)
        {
            string directory = Path.GetDirectoryName(path); Directory.CreateDirectory(directory); string temporary = Path.Combine(directory, Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) using (var writer = new StreamWriter(stream, encoding)) { writer.Write(content); writer.Flush(); stream.Flush(true); }
            if (File.Exists(path)) { File.Replace(temporary, path, null, true); } else { File.Move(temporary, path); }
        }

        private static void ResolveHostnames(List<NativeSnapshotRow> rows, int timeout)
        {
            Parallel.ForEach(rows, new ParallelOptions { MaxDegreeOfParallelism = 16 }, delegate(NativeSnapshotRow row) { try { Task<IPHostEntry> task = Dns.GetHostEntryAsync(row.IP); if (task.Wait(Math.Max(100, timeout))) { row.Hostname = task.Result.HostName; } } catch { } });
        }

        private static void ProcessAlerts(List<NativeSnapshotRow> rows, string conflictPath, string logPath, Dictionary<string, DateTime> alertState, MonitorConfiguration config)
        {
            foreach (NativeSnapshotRow row in rows.Where(delegate(NativeSnapshotRow item) { return item.Status == "CONFIRMED"; }))
            {
                string key = row.IP + "|" + String.Join("+", (row.MACs ?? String.Empty).Split(',').Select(delegate(string mac) { return mac.Trim(); }).OrderBy(delegate(string mac) { return mac; }).ToArray()); DateTime previous;
                if (alertState.TryGetValue(key, out previous) && previous > DateTime.Now.AddMinutes(-config.Monitoring.AlertCooldownMinutes)) { continue; }
                alertState[key] = DateTime.Now; if (config.Output.ConflictCsv) { AppendConflict(conflictPath, row); }
                if (config.Integrations.WebhookEnabled && !String.IsNullOrWhiteSpace(config.Integrations.WebhookUrl)) { SendWebhook(config.Integrations.WebhookUrl, row, logPath, config.Output); }
            }
        }

        private static void AppendConflict(string path, NativeSnapshotRow row) { bool header = !File.Exists(path) || new FileInfo(path).Length == 0; using (var writer = new StreamWriter(path, true, new UTF8Encoding(true))) { if (header) { writer.WriteLine(CsvHeader); } writer.WriteLine(ToCsv(row)); } }
        private static string ToCsv(NativeSnapshotRow row)
        {
            return String.Join(",", new[] { Csv(row.TimestampUtc.ToString("o")), Csv(row.IP), Csv(row.Hostname), Csv(row.Status), Csv(row.MACs), Csv(row.MACDetails), Csv(row.MacCount.ToString()), Csv(row.Observations.ToString()), Csv(row.Transitions.ToString()), Csv(row.DirectArpMacCount.ToString()), Csv(row.ActiveProbeMacCount.ToString()), Csv(row.MappingMismatch.ToString()), Csv(row.FirstSeen.ToString("s")), Csv(row.LastSeen.ToString("s")), Csv(row.Reason), Csv(row.Interface), Csv(row.MonitorIp), Csv(row.RequestObserved.ToString()), Csv(row.PositiveRounds.ToString()), Csv(row.RequiredRounds.ToString()), Csv(row.ConfirmedCycles.ToString()), Csv(row.RequiredCycles.ToString()), Csv(row.CorrelatedArpReplies.ToString()), Csv(row.ProxyArpRisk.ToString()), Csv(row.GatewayMac.ToString()), Csv(row.TrustedPair.ToString()), Csv(row.CaptureHealthy.ToString()), Csv(row.ConfidenceScore.ToString()), Csv(row.EvidenceId), Csv(row.EvidenceHash), Csv(row.EvidenceQuality) });
        }
        private static string Csv(string value) { return "\"" + (value ?? String.Empty).Replace("\"", "\"\"") + "\""; }

        private static void SendWebhook(string url, NativeSnapshotRow row, string logPath, OutputConfiguration output)
        {
            try { Uri uri; if (!Uri.TryCreate(url, UriKind.Absolute, out uri) || (uri.Scheme != Uri.UriSchemeHttps && !IPAddress.IsLoopback(Dns.GetHostAddresses(uri.Host).FirstOrDefault() ?? IPAddress.None))) { throw new InvalidOperationException("Webhook deve usar HTTPS."); } var request = (HttpWebRequest)WebRequest.Create(uri); request.Method = "POST"; request.ContentType = "application/json"; request.Timeout = 5000; byte[] body = Encoding.UTF8.GetBytes(Json.Serialize(new { type = "ip_conflict", severity = "critical", ip = row.IP, macs = row.MACs, evidenceId = row.EvidenceId, evidenceHash = row.EvidenceHash, reason = row.Reason, timestampUtc = row.TimestampUtc })); request.ContentLength = body.Length; using (Stream stream = request.GetRequestStream()) { stream.Write(body, 0, body.Length); } using (WebResponse response = request.GetResponse()) { } }
            catch (Exception exception) { WriteLog(logPath, output, "WARN", "Falha no webhook: " + exception.Message); }
        }

        private static void WriteLog(string path, OutputConfiguration output, string level, string message)
        {
            try { RotateLog(path, output.LogMaxMB, output.LogRetentionFiles); File.AppendAllText(path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [" + level + "] " + message + Environment.NewLine, new UTF8Encoding(false)); } catch { }
            if (Environment.UserInteractive) { Console.WriteLine("[" + level + "] " + message); }
        }
        private static void RotateLog(string path, int maxMb, int retention) { if (!File.Exists(path) || new FileInfo(path).Length < (long)Math.Max(1, maxMb) * 1024L * 1024L) { return; } int keep = Math.Max(1, retention); for (int index = keep - 1; index >= 1; index--) { string source = path + "." + index; string destination = path + "." + (index + 1); if (File.Exists(source)) { File.Copy(source, destination, true); } } File.Copy(path, path + ".1", true); File.WriteAllText(path, String.Empty); }
        private static int StatusRank(string status) { return status == "CONFIRMED" ? 0 : status == "MONITORING_LIMITED" ? 1 : status == "UNVERIFIED" ? 2 : 3; }
        private static string NewEvidenceId() { long sequence = Interlocked.Increment(ref _evidenceSequence); return "EVD-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + sequence.ToString("D5"); }
        private static bool IsAdministrator() { try { using (WindowsIdentity identity = WindowsIdentity.GetCurrent()) { return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator); } } catch { return false; } }
        private static bool HasSwitch(string[] args, params string[] names) { foreach (string argument in args) { foreach (string name in names) { if (String.Equals(argument, name, StringComparison.OrdinalIgnoreCase)) { return true; } } } return false; }
        private static void WaitCancelable(EventWaitHandle stopEvent, int milliseconds) { if (milliseconds <= 0) { return; } stopEvent.WaitOne(milliseconds); }
    }
}


