using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace IPConflictMonitor.Launcher
{
    internal enum StrictDetectionState
    {
        NORMAL,
        UNVERIFIED,
        MONITORING_LIMITED,
        CONFIRMED
    }

    internal sealed class StrictPolicy
    {
        public int VerificationRounds = 3;
        public int RequiredPositiveRounds = 2;
        public int RequiredConfirmedCycles = 2;
        public bool RequireCapturedArpRequest = true;
        public bool RequireCorrelatedArpResponses = true;
        public bool FailClosedWithoutCapture = true;
        public bool DetectProxyArp = true;
    }

    internal sealed class DetectionHealth
    {
        public bool CaptureAvailable;
        public bool CaptureRunning;
        public bool NpcapAvailable;
        public bool TsharkAvailable;
        public bool InterfaceReady;
        public bool StrictVerificationReady;
        public DateTime? LastCapturePacketUtc;
        public string LastError;

        public string Summary
        {
            get
            {
                if (StrictVerificationReady) { return "READY"; }
                if (!TsharkAvailable) { return "TShark indisponivel"; }
                if (!NpcapAvailable) { return "Npcap/captura indisponivel"; }
                if (!InterfaceReady) { return "interface de captura invalida"; }
                return String.IsNullOrWhiteSpace(LastError) ? "captura indisponivel" : LastError;
            }
        }
    }

    internal sealed class VerificationRoundEvidence
    {
        public int Round;
        public DateTime RequestTimestampUtc;
        public DateTime ResponseWindowStartUtc;
        public DateTime ResponseWindowEndUtc;
        public bool CaptureHealthy;
        public bool RequestObserved;
        public bool RequestCorrelationValid;
        public bool InterfaceValid;
        public bool GratuitousOnly;
        public string Error;
        public List<string> CorrelatedMacs = new List<string>();
        public Dictionary<string, int> ResponsesByMac = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        public string PairKey
        {
            get
            {
                string[] valid = CorrelatedMacs.Select(StrictEvidenceDecisionEngine.NormalizeAndValidateMac).Where(delegate(string value) { return value != null; }).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(delegate(string value) { return value; }).ToArray();
                return valid.Length == 2 ? valid[0] + "+" + valid[1] : null;
            }
        }
    }

    internal sealed class ConflictEvidence
    {
        public string TargetIp;
        public string InterfaceId;
        public string InterfaceName;
        public string MonitorIp;
        public string EvidenceId;
        public long Cycle;
        public bool VerificationRequired;
        public bool DiscoveryAmbiguous;
        public bool OnlyHistoricalChange;
        public bool CacheOnlyAmbiguity;
        public bool PossibleProxyArp;
        public bool GatewayMacDetected;
        public bool TrustedPair;
        public bool InterfaceValidation;
        public bool CaptureFailure;
        public bool ObservedGratuitousArp;
        public DetectionHealth Health;
        public List<string> CurrentObservedMacs = new List<string>();
        public List<string> HistoricalMacs = new List<string>();
        public List<VerificationRoundEvidence> Rounds = new List<VerificationRoundEvidence>();
    }

    internal sealed class ConflictDecisionMemory
    {
        public string PairKey { get; set; }
        public int ConsecutivePositiveCycles { get; set; }
        public long LastPositiveCycle { get; set; }
        public bool WasConfirmed { get; set; }
        public DateTime? FirstConfirmedUtc { get; set; }
        public DateTime? LastConfirmedConflictUtc { get; set; }
        public string LastEvidenceId { get; set; }
    }

    internal sealed class DetectionDecision
    {
        public StrictDetectionState State;
        public string Reason;
        public string TargetIp;
        public string MacA;
        public string MacB;
        public int PositiveRounds;
        public int RequiredRounds;
        public int ConfirmedCycles;
        public int RequiredCycles;
        public int CorrelatedArpReplies;
        public string EvidenceQuality;
        public bool MonitoringHealthy;
        public bool RequestObserved;
        public bool RequestCorrelationValid;
        public bool SameMacPairAcrossRounds;
        public bool PossibleProxyArp;
        public bool GatewayMacDetected;
        public bool TrustedPair;
        public bool ConflictResolved;
        public string EvidenceId;
        public string EvidenceHash;
    }

    internal static class StrictEvidenceDecisionEngine
    {
        public static DetectionDecision EvaluateConflict(ConflictEvidence evidence, StrictPolicy policy, ConflictDecisionMemory memory)
        {
            if (evidence == null) { throw new ArgumentNullException("evidence"); }
            if (policy == null) { throw new ArgumentNullException("policy"); }
            if (memory == null) { throw new ArgumentNullException("memory"); }

            var decision = NewDecision(evidence, policy);
            bool previouslyConfirmed = memory.WasConfirmed;

            if (!evidence.VerificationRequired && !previouslyConfirmed)
            {
                ResetCurrentProof(memory);
                decision.State = StrictDetectionState.NORMAL;
                decision.Reason = "Nenhuma evidencia contemporanea de conflito foi encontrada.";
                decision.EvidenceQuality = "DISCOVERY_ONLY";
                return FinalizeDecision(decision, evidence);
            }

            DetectionHealth health = evidence.Health ?? new DetectionHealth();
            if (policy.FailClosedWithoutCapture && (!health.StrictVerificationReady || evidence.CaptureFailure || !evidence.InterfaceValidation))
            {
                ResetCurrentProof(memory);
                decision.State = StrictDetectionState.MONITORING_LIMITED;
                decision.Reason = "Confirmacao bloqueada: " + (evidence.CaptureFailure ? "a captura terminou durante a verificacao." : health.Summary + ".");
                decision.EvidenceQuality = "CAPTURE_UNAVAILABLE";
                return FinalizeDecision(decision, evidence);
            }

            if (evidence.TrustedPair)
            {
                ResetCurrentProof(memory);
                decision.State = StrictDetectionState.NORMAL;
                decision.TrustedPair = true;
                decision.Reason = "Associacao autorizada explicitamente pela configuracao.";
                decision.EvidenceQuality = "TRUSTED";
                return FinalizeDecision(decision, evidence);
            }

            if (policy.DetectProxyArp && (evidence.PossibleProxyArp || evidence.GatewayMacDetected))
            {
                ResetCurrentProof(memory);
                decision.State = StrictDetectionState.UNVERIFIED;
                decision.PossibleProxyArp = evidence.PossibleProxyArp;
                decision.GatewayMacDetected = evidence.GatewayMacDetected;
                decision.Reason = evidence.GatewayMacDetected ? "Conflito rejeitado: MAC de gateway compativel com Proxy ARP." : "Conflito rejeitado: comportamento compativel com Proxy ARP.";
                decision.EvidenceQuality = "PROXY_ARP_RISK";
                return FinalizeDecision(decision, evidence);
            }

            List<VerificationRoundEvidence> healthyRounds = evidence.Rounds.Where(delegate(VerificationRoundEvidence round)
            {
                return round != null && round.CaptureHealthy && round.InterfaceValid && !round.GratuitousOnly;
            }).ToList();

            if (evidence.Rounds.Any(delegate(VerificationRoundEvidence round) { return round != null && !round.CaptureHealthy; }))
            {
                ResetCurrentProof(memory);
                decision.State = StrictDetectionState.MONITORING_LIMITED;
                decision.Reason = "Confirmacao bloqueada: uma rodada perdeu a captura; resultados parciais foram descartados.";
                decision.EvidenceQuality = "CAPTURE_FAILED";
                return FinalizeDecision(decision, evidence);
            }

            List<VerificationRoundEvidence> correlated = healthyRounds.Where(delegate(VerificationRoundEvidence round)
            {
                bool requestOk = !policy.RequireCapturedArpRequest || round.RequestObserved;
                bool correlationOk = !policy.RequireCorrelatedArpResponses || round.RequestCorrelationValid;
                return requestOk && correlationOk && round.PairKey != null;
            }).ToList();

            decision.RequestObserved = healthyRounds.Any(delegate(VerificationRoundEvidence round) { return round.RequestObserved; });
            decision.RequestCorrelationValid = correlated.Count > 0;
            decision.CorrelatedArpReplies = correlated.Sum(delegate(VerificationRoundEvidence round) { return round.ResponsesByMac.Values.Sum(); });

            string[] positivePairs = correlated.Select(delegate(VerificationRoundEvidence round) { return round.PairKey; }).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            decision.SameMacPairAcrossRounds = positivePairs.Length == 1;
            string pair = positivePairs.Length == 1 ? positivePairs[0] : null;
            decision.PositiveRounds = pair == null ? 0 : correlated.Count(delegate(VerificationRoundEvidence round) { return String.Equals(round.PairKey, pair, StringComparison.OrdinalIgnoreCase); });

            if (pair == null || decision.PositiveRounds < Math.Max(1, policy.RequiredPositiveRounds))
            {
                bool resolved = previouslyConfirmed && evidence.CurrentObservedMacs.Select(NormalizeAndValidateMac).Where(delegate(string mac) { return mac != null; }).Distinct(StringComparer.OrdinalIgnoreCase).Count() <= 1;
                ResetCurrentProof(memory);
                memory.WasConfirmed = false;
                decision.ConflictResolved = resolved;
                decision.State = resolved ? StrictDetectionState.NORMAL : StrictDetectionState.UNVERIFIED;
                if (evidence.OnlyHistoricalChange) { decision.Reason = "Conflito rejeitado: apenas alteracao historica de MAC."; }
                else if (evidence.CacheOnlyAmbiguity) { decision.Reason = "Conflito rejeitado: multiplos MACs existem somente no cache/historico."; }
                else if (healthyRounds.Count > 0 && !decision.RequestObserved) { decision.Reason = "Conflito rejeitado: respostas ARP sem requisicao correlacionada."; }
                else if (!decision.SameMacPairAcrossRounds && positivePairs.Length > 1) { decision.Reason = "Conflito rejeitado: o par de MACs nao foi consistente entre rodadas."; }
                else if (evidence.ObservedGratuitousArp) { decision.Reason = "Conflito rejeitado: somente ARP espontaneo/gratuitous foi observado."; }
                else { decision.Reason = resolved ? "CONFLICT_RESOLVED: a prova contemporanea deixou de existir." : "Conflito nao confirmado: o mesmo par nao respondeu em rodadas suficientes."; }
                decision.EvidenceQuality = resolved ? "RECOVERED" : "INCONCLUSIVE";
                return FinalizeDecision(decision, evidence);
            }

            string[] macParts = pair.Split('+');
            decision.MacA = macParts[0];
            decision.MacB = macParts[1];
            if (String.Equals(decision.MacA, decision.MacB, StringComparison.OrdinalIgnoreCase) || NormalizeAndValidateMac(decision.MacA) == null || NormalizeAndValidateMac(decision.MacB) == null)
            {
                ResetCurrentProof(memory);
                decision.State = StrictDetectionState.UNVERIFIED;
                decision.Reason = "Conflito rejeitado: enderecos MAC invalidos ou nao distintos.";
                decision.EvidenceQuality = "INVALID_MAC";
                return FinalizeDecision(decision, evidence);
            }

            if (String.Equals(memory.PairKey, pair, StringComparison.OrdinalIgnoreCase) && memory.LastPositiveCycle == evidence.Cycle - 1)
            {
                memory.ConsecutivePositiveCycles++;
            }
            else
            {
                memory.PairKey = pair;
                memory.ConsecutivePositiveCycles = 1;
            }
            memory.LastPositiveCycle = evidence.Cycle;
            memory.LastEvidenceId = evidence.EvidenceId;
            decision.ConfirmedCycles = memory.ConsecutivePositiveCycles;

            if (memory.ConsecutivePositiveCycles >= Math.Max(1, policy.RequiredConfirmedCycles))
            {
                decision.State = StrictDetectionState.CONFIRMED;
                decision.Reason = "Conflito confirmado apos respostas ARP correlacionadas e repetidas de dois MACs distintos para o mesmo IPv4.";
                decision.EvidenceQuality = "STRICT_PROOF";
                memory.WasConfirmed = true;
                if (!memory.FirstConfirmedUtc.HasValue) { memory.FirstConfirmedUtc = DateTime.UtcNow; }
                memory.LastConfirmedConflictUtc = DateTime.UtcNow;
            }
            else
            {
                decision.State = StrictDetectionState.UNVERIFIED;
                decision.Reason = "Prova forte no ciclo atual; aguardando persistencia em ciclo consecutivo (" + memory.ConsecutivePositiveCycles + "/" + policy.RequiredConfirmedCycles + ").";
                decision.EvidenceQuality = "PENDING_CONSECUTIVE_CYCLE";
                memory.WasConfirmed = false;
            }
            return FinalizeDecision(decision, evidence);
        }

        private static DetectionDecision NewDecision(ConflictEvidence evidence, StrictPolicy policy)
        {
            return new DetectionDecision
            {
                TargetIp = evidence.TargetIp,
                EvidenceId = evidence.EvidenceId,
                RequiredRounds = Math.Max(1, policy.RequiredPositiveRounds),
                RequiredCycles = Math.Max(1, policy.RequiredConfirmedCycles),
                MonitoringHealthy = evidence.Health != null && evidence.Health.StrictVerificationReady,
                TrustedPair = evidence.TrustedPair,
                PossibleProxyArp = evidence.PossibleProxyArp,
                GatewayMacDetected = evidence.GatewayMacDetected
            };
        }

        private static DetectionDecision FinalizeDecision(DetectionDecision decision, ConflictEvidence evidence)
        {
            decision.EvidenceHash = ComputeEvidenceHash(evidence, decision);
            return decision;
        }

        private static void ResetCurrentProof(ConflictDecisionMemory memory)
        {
            memory.PairKey = null;
            memory.ConsecutivePositiveCycles = 0;
            memory.LastPositiveCycle = 0;
        }

        public static string NormalizeAndValidateMac(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) { return null; }
            string hex = new string(value.Where(Uri.IsHexDigit).ToArray()).ToUpperInvariant();
            if (hex.Length != 12 || hex == "000000000000" || hex == "FFFFFFFFFFFF") { return null; }
            int first;
            if (!Int32.TryParse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, System.Globalization.CultureInfo.InvariantCulture, out first) || (first & 1) != 0) { return null; }
            return String.Join(":", Enumerable.Range(0, 6).Select(delegate(int index) { return hex.Substring(index * 2, 2); }).ToArray());
        }

        public static int InterfaceCandidateScore(string name, string description, bool ethernet, bool wireless, bool hasGateway, long speed, bool virtualAdapter)
        {
            string text = ((name ?? String.Empty) + " " + (description ?? String.Empty)).ToLowerInvariant();
            string[] virtualTerms = { "vpn", "tap", "tun", "hyper-v", "vmware", "virtualbox", "wsl", "docker", "bluetooth", "zerotier", "tailscale", "loopback", "virtual" };
            bool looksVirtual = virtualAdapter || virtualTerms.Any(delegate(string term) { return text.Contains(term); });
            int score = looksVirtual ? -10000 : 0;
            if (ethernet) { score += 500; }
            if (wireless) { score += 350; }
            if (hasGateway) { score += 250; }
            score += (int)Math.Min(200L, Math.Max(0L, speed / 10000000L));
            return score;
        }

        private static string ComputeEvidenceHash(ConflictEvidence evidence, DetectionDecision decision)
        {
            var normalized = new StringBuilder();
            normalized.Append(evidence.TargetIp).Append('|').Append(evidence.InterfaceId).Append('|').Append(evidence.MonitorIp).Append('|').Append(evidence.Cycle).Append('|');
            foreach (VerificationRoundEvidence round in evidence.Rounds.OrderBy(delegate(VerificationRoundEvidence item) { return item.Round; }))
            {
                normalized.Append(round.Round).Append(':').Append(round.RequestObserved).Append(':').Append(round.RequestCorrelationValid).Append(':').Append(round.PairKey).Append(':');
                foreach (KeyValuePair<string, int> response in round.ResponsesByMac.OrderBy(delegate(KeyValuePair<string, int> item) { return item.Key; })) { normalized.Append(response.Key).Append('=').Append(response.Value).Append(','); }
                normalized.Append(';');
            }
            normalized.Append(decision.State).Append('|').Append(decision.PositiveRounds).Append('|').Append(decision.ConfirmedCycles);
            using (SHA256 sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(normalized.ToString()))).Replace("-", String.Empty);
            }
        }
    }

    internal static class StrictEvidenceSelfTests
    {
        private const string MacA = "00:11:22:33:44:50";
        private const string MacB = "00:11:22:33:44:60";
        private const string MacC = "00:11:22:33:44:70";

        public static int Run(TextWriter output)
        {
            var failures = new List<string>();
            int passed = 0;
            output.WriteLine("Strict Detection Self-Test");
            RunCase(output, failures, ref passed, "single MAC", TestSingleMac);
            RunCase(output, failures, ref passed, "historical transition", TestHistoricalTransition);
            RunCase(output, failures, ref passed, "historical A-B-A", TestHistoricalFlapping);
            RunCase(output, failures, ref passed, "ARP cache ambiguity", TestCacheAmbiguity);
            RunCase(output, failures, ref passed, "single capture occurrence", TestSingleOccurrence);
            RunCase(output, failures, ref passed, "two positive rounds", TestTwoPositiveRounds);
            RunCase(output, failures, ref passed, "two confirmed cycles", TestTwoCycles);
            RunCase(output, failures, ref passed, "pair missing in round two", TestInconsistentRound);
            RunCase(output, failures, ref passed, "proof missing in cycle two", TestMissingSecondCycle);
            RunCase(output, failures, ref passed, "TShark absent", delegate { return TestLimited("TShark"); });
            RunCase(output, failures, ref passed, "Npcap absent", delegate { return TestLimited("Npcap"); });
            RunCase(output, failures, ref passed, "capture interrupted", TestCaptureInterrupted);
            RunCase(output, failures, ref passed, "uncorrelated ARP reply", TestUncorrelatedReply);
            RunCase(output, failures, ref passed, "gratuitous ARP", TestGratuitous);
            RunCase(output, failures, ref passed, "DHCP address reuse", TestHistoricalTransition);
            RunCase(output, failures, ref passed, "HA VIP failover", TestTrustedVirtualIp);
            RunCase(output, failures, ref passed, "trusted pair", TestTrustedVirtualIp);
            RunCase(output, failures, ref passed, "proxy ARP gateway", TestProxyArp);
            RunCase(output, failures, ref passed, "broadcast MAC rejected", delegate { return StrictEvidenceDecisionEngine.NormalizeAndValidateMac("FF:FF:FF:FF:FF:FF") == null; });
            RunCase(output, failures, ref passed, "multicast MAC rejected", delegate { return StrictEvidenceDecisionEngine.NormalizeAndValidateMac("01:00:5E:00:00:01") == null; });
            RunCase(output, failures, ref passed, "wrong interface", TestWrongInterface);
            RunCase(output, failures, ref passed, "physical interface preferred", TestInterfaceSelection);
            RunCase(output, failures, ref passed, "real repeated conflict", TestTwoCycles);
            RunCase(output, failures, ref passed, "confirmed conflict recovery", TestRecovery);
            output.WriteLine();
            output.WriteLine(passed + " passed");
            output.WriteLine(failures.Count + " failed");
            foreach (string failure in failures) { output.WriteLine("[FAIL] " + failure); }
            return failures.Count == 0 ? 0 : 20;
        }

        private static void RunCase(TextWriter output, List<string> failures, ref int passed, string name, Func<bool> test)
        {
            bool success = false;
            try { success = test(); } catch (Exception exception) { failures.Add(name + ": " + exception.Message); }
            if (success) { passed++; output.WriteLine("[PASS] " + name); }
            else if (!failures.Any(delegate(string item) { return item.StartsWith(name + ":", StringComparison.Ordinal); })) { failures.Add(name); }
        }

        private static StrictPolicy Policy() { return new StrictPolicy { VerificationRounds = 3, RequiredPositiveRounds = 2, RequiredConfirmedCycles = 2 }; }
        private static DetectionHealth Healthy() { return new DetectionHealth { CaptureAvailable = true, CaptureRunning = true, NpcapAvailable = true, TsharkAvailable = true, InterfaceReady = true, StrictVerificationReady = true }; }
        private static ConflictEvidence Evidence(long cycle)
        {
            return new ConflictEvidence { TargetIp = "192.168.10.50", InterfaceId = "12", InterfaceName = "Ethernet", MonitorIp = "192.168.10.10", EvidenceId = "EVD-TEST-" + cycle, Cycle = cycle, VerificationRequired = true, DiscoveryAmbiguous = true, InterfaceValidation = true, Health = Healthy(), CurrentObservedMacs = new List<string> { MacA, MacB }, HistoricalMacs = new List<string> { MacA, MacB } };
        }
        private static VerificationRoundEvidence Round(int number, string a, string b)
        {
            var round = new VerificationRoundEvidence { Round = number, CaptureHealthy = true, RequestObserved = true, RequestCorrelationValid = true, InterfaceValid = true, RequestTimestampUtc = DateTime.UtcNow, ResponseWindowStartUtc = DateTime.UtcNow, ResponseWindowEndUtc = DateTime.UtcNow.AddSeconds(1) };
            if (a != null) { round.CorrelatedMacs.Add(a); round.ResponsesByMac[a] = 1; }
            if (b != null) { round.CorrelatedMacs.Add(b); round.ResponsesByMac[b] = 1; }
            return round;
        }
        private static ConflictEvidence PositiveEvidence(long cycle)
        {
            ConflictEvidence evidence = Evidence(cycle);
            evidence.Rounds.Add(Round(1, MacA, MacB));
            evidence.Rounds.Add(Round(2, MacA, MacB));
            evidence.Rounds.Add(Round(3, MacA, null));
            return evidence;
        }
        private static bool TestSingleMac()
        {
            ConflictEvidence evidence = Evidence(1); evidence.VerificationRequired = false; evidence.DiscoveryAmbiguous = false; evidence.CurrentObservedMacs = new List<string> { MacA };
            return StrictEvidenceDecisionEngine.EvaluateConflict(evidence, Policy(), new ConflictDecisionMemory()).State == StrictDetectionState.NORMAL;
        }
        private static bool TestHistoricalTransition()
        {
            ConflictEvidence evidence = Evidence(1); evidence.OnlyHistoricalChange = true; evidence.Rounds.Clear();
            return StrictEvidenceDecisionEngine.EvaluateConflict(evidence, Policy(), new ConflictDecisionMemory()).State == StrictDetectionState.UNVERIFIED;
        }
        private static bool TestHistoricalFlapping()
        {
            ConflictEvidence evidence = Evidence(1); evidence.OnlyHistoricalChange = true; evidence.HistoricalMacs = new List<string> { MacA, MacB, MacA };
            return StrictEvidenceDecisionEngine.EvaluateConflict(evidence, Policy(), new ConflictDecisionMemory()).State != StrictDetectionState.CONFIRMED;
        }
        private static bool TestCacheAmbiguity()
        {
            ConflictEvidence evidence = Evidence(1); evidence.CacheOnlyAmbiguity = true;
            return StrictEvidenceDecisionEngine.EvaluateConflict(evidence, Policy(), new ConflictDecisionMemory()).State == StrictDetectionState.UNVERIFIED;
        }
        private static bool TestSingleOccurrence()
        {
            ConflictEvidence evidence = Evidence(1); evidence.Rounds.Add(Round(1, MacA, MacB));
            return StrictEvidenceDecisionEngine.EvaluateConflict(evidence, Policy(), new ConflictDecisionMemory()).State == StrictDetectionState.UNVERIFIED;
        }
        private static bool TestTwoPositiveRounds()
        {
            DetectionDecision decision = StrictEvidenceDecisionEngine.EvaluateConflict(PositiveEvidence(1), Policy(), new ConflictDecisionMemory());
            return decision.State == StrictDetectionState.UNVERIFIED && decision.PositiveRounds == 2 && decision.SameMacPairAcrossRounds;
        }
        private static bool TestTwoCycles()
        {
            var memory = new ConflictDecisionMemory();
            DetectionDecision first = StrictEvidenceDecisionEngine.EvaluateConflict(PositiveEvidence(1), Policy(), memory);
            DetectionDecision second = StrictEvidenceDecisionEngine.EvaluateConflict(PositiveEvidence(2), Policy(), memory);
            return first.State == StrictDetectionState.UNVERIFIED && second.State == StrictDetectionState.CONFIRMED;
        }
        private static bool TestInconsistentRound()
        {
            ConflictEvidence evidence = Evidence(1); evidence.Rounds.Add(Round(1, MacA, MacB)); evidence.Rounds.Add(Round(2, MacA, null));
            return StrictEvidenceDecisionEngine.EvaluateConflict(evidence, Policy(), new ConflictDecisionMemory()).State == StrictDetectionState.UNVERIFIED;
        }
        private static bool TestMissingSecondCycle()
        {
            var memory = new ConflictDecisionMemory(); StrictEvidenceDecisionEngine.EvaluateConflict(PositiveEvidence(1), Policy(), memory);
            ConflictEvidence second = Evidence(2); second.CurrentObservedMacs = new List<string> { MacA };
            return StrictEvidenceDecisionEngine.EvaluateConflict(second, Policy(), memory).State != StrictDetectionState.CONFIRMED;
        }
        private static bool TestLimited(string component)
        {
            ConflictEvidence evidence = Evidence(1); evidence.Health.StrictVerificationReady = false; evidence.Health.LastError = component + " indisponivel";
            if (component == "TShark") { evidence.Health.TsharkAvailable = false; } else { evidence.Health.NpcapAvailable = false; }
            return StrictEvidenceDecisionEngine.EvaluateConflict(evidence, Policy(), new ConflictDecisionMemory()).State == StrictDetectionState.MONITORING_LIMITED;
        }
        private static bool TestCaptureInterrupted()
        {
            ConflictEvidence evidence = PositiveEvidence(1); evidence.CaptureFailure = true;
            return StrictEvidenceDecisionEngine.EvaluateConflict(evidence, Policy(), new ConflictDecisionMemory()).State == StrictDetectionState.MONITORING_LIMITED;
        }
        private static bool TestUncorrelatedReply()
        {
            ConflictEvidence evidence = Evidence(1); VerificationRoundEvidence round = Round(1, MacA, MacB); round.RequestObserved = false; round.RequestCorrelationValid = false; evidence.Rounds.Add(round);
            return StrictEvidenceDecisionEngine.EvaluateConflict(evidence, Policy(), new ConflictDecisionMemory()).State == StrictDetectionState.UNVERIFIED;
        }
        private static bool TestGratuitous()
        {
            ConflictEvidence evidence = Evidence(1); evidence.ObservedGratuitousArp = true; VerificationRoundEvidence round = Round(1, MacA, MacB); round.GratuitousOnly = true; evidence.Rounds.Add(round);
            return StrictEvidenceDecisionEngine.EvaluateConflict(evidence, Policy(), new ConflictDecisionMemory()).State == StrictDetectionState.UNVERIFIED;
        }
        private static bool TestTrustedVirtualIp()
        {
            ConflictEvidence evidence = PositiveEvidence(1); evidence.TrustedPair = true;
            return StrictEvidenceDecisionEngine.EvaluateConflict(evidence, Policy(), new ConflictDecisionMemory()).State == StrictDetectionState.NORMAL;
        }
        private static bool TestProxyArp()
        {
            ConflictEvidence evidence = PositiveEvidence(1); evidence.PossibleProxyArp = true; evidence.GatewayMacDetected = true;
            DetectionDecision decision = StrictEvidenceDecisionEngine.EvaluateConflict(evidence, Policy(), new ConflictDecisionMemory());
            return decision.State == StrictDetectionState.UNVERIFIED && decision.PossibleProxyArp;
        }
        private static bool TestWrongInterface()
        {
            ConflictEvidence evidence = Evidence(1); evidence.InterfaceValidation = false; evidence.Health.InterfaceReady = false; evidence.Health.StrictVerificationReady = false;
            return StrictEvidenceDecisionEngine.EvaluateConflict(evidence, Policy(), new ConflictDecisionMemory()).State == StrictDetectionState.MONITORING_LIMITED;
        }
        private static bool TestInterfaceSelection()
        {
            int physical = StrictEvidenceDecisionEngine.InterfaceCandidateScore("Ethernet", "Intel Ethernet", true, false, true, 1000000000L, false);
            int vpn = StrictEvidenceDecisionEngine.InterfaceCandidateScore("VPN", "TAP virtual adapter", false, false, true, 1000000000L, true);
            return physical > vpn;
        }
        private static bool TestRecovery()
        {
            var memory = new ConflictDecisionMemory(); StrictEvidenceDecisionEngine.EvaluateConflict(PositiveEvidence(1), Policy(), memory); StrictEvidenceDecisionEngine.EvaluateConflict(PositiveEvidence(2), Policy(), memory);
            ConflictEvidence recovered = Evidence(3); recovered.CurrentObservedMacs = new List<string> { MacA }; recovered.DiscoveryAmbiguous = false;
            DetectionDecision decision = StrictEvidenceDecisionEngine.EvaluateConflict(recovered, Policy(), memory);
            return decision.State == StrictDetectionState.NORMAL && decision.ConflictResolved && !memory.WasConfirmed;
        }
    }
}
