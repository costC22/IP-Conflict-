using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace IPConflictMonitor.Launcher
{
    internal static partial class Program
    {
        private enum SettingsSection
        {
            Network,
            Trust,
            Monitoring,
            Verification,
            Integrations,
            Output
        }

        private sealed class InterfaceOption
        {
            public int Index;
            public string Label;
            public override string ToString() { return Label; }
        }

        private static class ConfigurationStore
        {
            private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue, RecursionLimit = 100 };

            public static string ResolveActivePath()
            {
                string applicationDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                string directory = Path.Combine(GetUserDataDirectory(), "config");
                Directory.CreateDirectory(directory);
                string activePath = Path.Combine(directory, "config.json");
                if (File.Exists(activePath)) { return activePath; }

                string sidecar = Path.Combine(applicationDirectory, "config", "config.json");
                byte[] seed = File.Exists(sidecar)
                    ? File.ReadAllBytes(sidecar)
                    : new UTF8Encoding(false).GetBytes(ReadResourceText(ConfigResource));
                WriteNewFile(activePath, seed);
                return activePath;
            }

            public static MonitorConfiguration Load(string path)
            {
                return NativeMonitor.LoadConfiguration(String.IsNullOrWhiteSpace(path) ? ResolveActivePath() : Path.GetFullPath(path));
            }

            public static MonitorConfiguration Load()
            {
                return Load(null);
            }

            public static string ResolveOutputDirectory()
            {
                try
                {
                    MonitorConfiguration configuration = Load();
                    string configured = configuration.Output == null ? String.Empty : configuration.Output.Directory;
                    if (!String.IsNullOrWhiteSpace(configured)) { return Path.GetFullPath(Environment.ExpandEnvironmentVariables(configured)); }
                }
                catch { }
                return GetUserDataDirectory();
            }

            public static List<string> Validate(MonitorConfiguration configuration)
            {
                var errors = NativeMonitor.ValidateConfiguration(configuration);
                if (configuration.Network.InterfaceIndex < 0) { errors.Add("Network.InterfaceIndex não pode ser negativo."); }
                if (configuration.Monitoring.CaptureSeconds < 2 || configuration.Monitoring.CaptureSeconds > 120) { errors.Add("Monitoring.CaptureSeconds deve estar entre 2 e 120."); }
                if (configuration.Monitoring.CaptureWarmupMilliseconds < 0 || configuration.Monitoring.CaptureWarmupMilliseconds > 10000) { errors.Add("Monitoring.CaptureWarmupMilliseconds deve estar entre 0 e 10000."); }
                if (configuration.Monitoring.EvidenceWindowMinutes < 1 || configuration.Monitoring.EvidenceWindowMinutes > 1440) { errors.Add("Monitoring.EvidenceWindowMinutes deve estar entre 1 e 1440."); }
                if (configuration.Monitoring.HistoryExpirationMinutes < configuration.Monitoring.EvidenceWindowMinutes || configuration.Monitoring.HistoryExpirationMinutes > 10080) { errors.Add("Monitoring.HistoryExpirationMinutes deve ser maior ou igual à janela de evidência e no máximo 10080."); }
                if (configuration.Monitoring.AlertCooldownMinutes < 0 || configuration.Monitoring.AlertCooldownMinutes > 1440) { errors.Add("Monitoring.AlertCooldownMinutes deve estar entre 0 e 1440."); }
                if (configuration.Monitoring.MaxConcurrentPings < 1 || configuration.Monitoring.MaxConcurrentPings > 256) { errors.Add("Monitoring.MaxConcurrentPings deve estar entre 1 e 256."); }
                if (configuration.Monitoring.HostnameTimeoutMs < 100 || configuration.Monitoring.HostnameTimeoutMs > 10000) { errors.Add("Monitoring.HostnameTimeoutMs deve estar entre 100 e 10000."); }
                if (configuration.Monitoring.ProxyArpIpThreshold < 2 || configuration.Monitoring.ProxyArpIpThreshold > 4096) { errors.Add("Monitoring.ProxyArpIpThreshold deve estar entre 2 e 4096."); }
                if (configuration.Output.LogRetentionFiles < 1 || configuration.Output.LogRetentionFiles > 100) { errors.Add("Output.LogRetentionFiles deve estar entre 1 e 100."); }

                foreach (string pair in configuration.Network.TrustedPairs ?? new string[0])
                {
                    string[] parts = (pair ?? String.Empty).Split('|');
                    IPAddress ip;
                    if (parts.Length == 2 && IPAddress.TryParse(parts[0].Trim(), out ip) && ip.AddressFamily != AddressFamily.InterNetwork)
                    {
                        errors.Add("TrustedPairs aceita somente IPv4: " + pair);
                    }
                }

                if (!String.IsNullOrWhiteSpace(configuration.Network.CIDR))
                {
                    long hostCount;
                    if (TryCountHosts(configuration.Network.CIDR, out hostCount) && hostCount > configuration.Network.MaxHosts)
                    {
                        errors.Add("Network.MaxHosts é menor que a quantidade de endereços do CIDR configurado.");
                    }
                }

                if (configuration.Integrations.WebhookEnabled)
                {
                    Uri uri;
                    if (!Uri.TryCreate(configuration.Integrations.WebhookUrl, UriKind.Absolute, out uri) || !IsAllowedWebhook(uri))
                    {
                        errors.Add("Webhook habilitado exige uma URL HTTPS ou um endereço loopback.");
                    }
                }

                if (!String.IsNullOrWhiteSpace(configuration.Output.Directory))
                {
                    try { Path.GetFullPath(Environment.ExpandEnvironmentVariables(configuration.Output.Directory)); }
                    catch { errors.Add("Output.Directory contém um caminho inválido."); }
                }
                return errors.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }

            public static void Save(MonitorConfiguration configuration)
            {
                Save(configuration, null);
            }

            public static void Save(MonitorConfiguration configuration, string targetPath)
            {
                List<string> errors = Validate(configuration);
                if (errors.Count > 0) { throw new InvalidDataException(String.Join(Environment.NewLine, errors.ToArray())); }

                string path = String.IsNullOrWhiteSpace(targetPath) ? ResolveActivePath() : Path.GetFullPath(targetPath);
                string directory = Path.GetDirectoryName(path);
                Directory.CreateDirectory(directory);
                string temporary = Path.Combine(directory, ".config-" + Guid.NewGuid().ToString("N") + ".tmp");
                string backup = path + ".previous";
                try
                {
                    string json = PrettyJson(Serializer.Serialize(configuration)) + Environment.NewLine;
                    byte[] bytes = new UTF8Encoding(false).GetBytes(json);
                    using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        stream.Write(bytes, 0, bytes.Length);
                        stream.Flush(true);
                    }
                    MonitorConfiguration written = NativeMonitor.LoadConfiguration(temporary);
                    List<string> writtenErrors = Validate(written);
                    if (writtenErrors.Count > 0) { throw new InvalidDataException(String.Join(Environment.NewLine, writtenErrors.ToArray())); }

                    if (File.Exists(path))
                    {
                        if (File.Exists(backup)) { File.Delete(backup); }
                        File.Replace(temporary, path, backup, true);
                    }
                    else
                    {
                        File.Move(temporary, path);
                    }
                }
                finally
                {
                    if (File.Exists(temporary)) { File.Delete(temporary); }
                }
            }

            private static void WriteNewFile(string path, byte[] bytes)
            {
                string directory = Path.GetDirectoryName(path);
                Directory.CreateDirectory(directory);
                string temporary = Path.Combine(directory, ".seed-" + Guid.NewGuid().ToString("N") + ".tmp");
                try
                {
                    using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        stream.Write(bytes, 0, bytes.Length);
                        stream.Flush(true);
                    }
                    try { File.Move(temporary, path); }
                    catch (IOException) { if (!File.Exists(path)) { throw; } }
                }
                finally
                {
                    if (File.Exists(temporary)) { File.Delete(temporary); }
                }
            }

            private static bool IsAllowedWebhook(Uri uri)
            {
                if (uri == null) { return false; }
                if (String.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)) { return true; }
                if (!String.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)) { return false; }
                if (String.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)) { return true; }
                IPAddress address;
                return IPAddress.TryParse(uri.Host, out address) && IPAddress.IsLoopback(address);
            }

            private static bool TryCountHosts(string cidr, out long count)
            {
                count = 0;
                string[] parts = (cidr ?? String.Empty).Split('/');
                int prefix;
                IPAddress address;
                if (parts.Length != 2 || !IPAddress.TryParse(parts[0], out address) || address.AddressFamily != AddressFamily.InterNetwork || !Int32.TryParse(parts[1], out prefix) || prefix < 0 || prefix > 32) { return false; }
                if (prefix == 32) { count = 1; return true; }
                if (prefix == 31) { count = 2; return true; }
                count = (1L << (32 - prefix)) - 2L;
                return true;
            }

            private static string PrettyJson(string json)
            {
                var output = new StringBuilder();
                int indent = 0;
                bool quoted = false;
                bool escaped = false;
                for (int index = 0; index < json.Length; index++)
                {
                    char character = json[index];
                    if (quoted)
                    {
                        output.Append(character);
                        if (escaped) { escaped = false; }
                        else if (character == '\\') { escaped = true; }
                        else if (character == '"') { quoted = false; }
                        continue;
                    }
                    if (character == '"') { quoted = true; output.Append(character); continue; }
                    if (Char.IsWhiteSpace(character)) { continue; }
                    if (character == '{' || character == '[')
                    {
                        int next = index + 1;
                        while (next < json.Length && Char.IsWhiteSpace(json[next])) { next++; }
                        char closing = character == '{' ? '}' : ']';
                        if (next < json.Length && json[next] == closing) { output.Append(character).Append(closing); index = next; continue; }
                        output.Append(character).AppendLine(); indent++; AppendIndent(output, indent); continue;
                    }
                    if (character == '}' || character == ']') { output.AppendLine(); indent = Math.Max(0, indent - 1); AppendIndent(output, indent); output.Append(character); continue; }
                    if (character == ',') { output.Append(character).AppendLine(); AppendIndent(output, indent); continue; }
                    if (character == ':') { output.Append(": "); continue; }
                    output.Append(character);
                }
                return output.ToString();
            }

            private static void AppendIndent(StringBuilder output, int indent)
            {
                output.Append(' ', Math.Max(0, indent) * 2);
            }
        }

        private sealed class SettingsExperiencePage : UserControl
        {
            private readonly string _configurationPath;
            private readonly bool _workerRunning;
            private readonly Dictionary<SettingsSection, Control> _sections = new Dictionary<SettingsSection, Control>();
            private readonly Dictionary<SettingsSection, FieldActionButton> _sectionButtons = new Dictionary<SettingsSection, FieldActionButton>();
            private Panel _sectionHost;
            private Label _statusTitle;
            private Label _statusDetail;
            private Panel _statusAccent;
            private FieldActionButton _saveButton;
            private FieldActionButton _discardButton;
            private MonitorConfiguration _configuration;
            private bool _loading;
            private bool _dirty;
            private bool _sensitiveConfirmationArmed;
            private string _baselineSensitive;

            private ComboBox _interface;
            private TextBox _cidr;
            private NumericUpDown _maxHosts;
            private TextBox _excludedIps;
            private TextBox _excludedMacs;
            private TextBox _trustedPairs;
            private TextBox _trustedVirtualIps;
            private TextBox _trustedMacs;
            private CheckBox _continuous;
            private NumericUpDown _intervalSeconds;
            private NumericUpDown _captureSeconds;
            private NumericUpDown _captureWarmup;
            private NumericUpDown _evidenceWindow;
            private NumericUpDown _historyExpiration;
            private NumericUpDown _alertCooldown;
            private CheckBox _pingSweep;
            private NumericUpDown _maxConcurrentPings;
            private NumericUpDown _pingTimeout;
            private CheckBox _resolveHostnames;
            private NumericUpDown _hostnameTimeout;
            private CheckBox _packetCapture;
            private CheckBox _activeArpProbe;
            private NumericUpDown _verificationRounds;
            private NumericUpDown _requiredPositiveRounds;
            private NumericUpDown _requiredConfirmedCycles;
            private NumericUpDown _arpResponseWindow;
            private CheckBox _detectProxyArp;
            private NumericUpDown _proxyArpThreshold;
            private NumericUpDown _arpProbeRate;
            private TextBox _tsharkPath;
            private TextBox _webhookUrl;
            private CheckBox _webhookEnabled;
            private TextBox _outputDirectory;
            private NumericUpDown _logMaxMb;
            private NumericUpDown _logRetention;
            private CheckBox _snapshotCsv;
            private CheckBox _conflictCsv;
            private CheckBox _jsonState;

            public event Action BackRequested;
            public bool HasUnsavedChanges { get { return _dirty; } }

            public SettingsExperiencePage(bool workerRunning)
                : this(workerRunning, null)
            {
            }

            public SettingsExperiencePage(bool workerRunning, string previewPath)
            {
                _workerRunning = workerRunning;
                _configurationPath = String.IsNullOrWhiteSpace(previewPath) ? ConfigurationStore.ResolveActivePath() : Path.GetFullPath(previewPath);
                Dock = DockStyle.Fill;
                BackColor = FieldTheme.Canvas;
                ForeColor = FieldTheme.MainText;
                Font = new Font("Segoe UI", 9F);
                Padding = new Padding(20, 12, 20, 14);
                AutoScaleMode = AutoScaleMode.Dpi;
                BuildInterface();
                LoadConfiguration();
            }

            public void ShowUnsavedWarning()
            {
                if (!_dirty) { return; }
                _discardButton.Visible = true;
                ShowStatus(FieldTheme.Amber, "ALTERAÇÕES NÃO SALVAS", "Salve as mudanças ou use Descartar e voltar antes de fechar esta tela.");
                _saveButton.Focus();
            }

            private void BuildInterface()
            {
                var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = FieldTheme.Canvas, Margin = new Padding(0), Padding = new Padding(0) };
                shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 122));
                shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
                Controls.Add(shell);

                var header = new FieldSurfacePanel { Dock = DockStyle.Fill, HeaderTreatment = true, AccentColor = FieldTheme.Cyan, Margin = new Padding(0, 0, 0, 12), Padding = new Padding(22, 12, 18, 10) };
                var headerLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, BackColor = Color.Transparent, Margin = new Padding(0) };
                headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 228));
                headerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
                headerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                headerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
                headerLayout.Controls.Add(new Label { Text = "CONFIGURAÇÃO / PERFIL ATIVO", Dock = DockStyle.Fill, ForeColor = FieldTheme.Cyan, Font = new Font("Segoe UI Semibold", 7.4F), TextAlign = ContentAlignment.BottomLeft, BackColor = Color.Transparent }, 0, 0);
                headerLayout.Controls.Add(new Label { Text = "Configuração do monitor", Dock = DockStyle.Fill, ForeColor = FieldTheme.MainText, Font = new Font("Bahnschrift SemiCondensed", 22F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent }, 0, 1);
                headerLayout.Controls.Add(new Label { Text = _configurationPath, Dock = DockStyle.Fill, ForeColor = FieldTheme.SoftText, Font = new Font("Consolas", 7.6F), TextAlign = ContentAlignment.TopLeft, AutoEllipsis = true, BackColor = Color.Transparent }, 0, 2);
                var policy = new Label { Text = "STRICT EVIDENCE  /  PROTEGIDO", Dock = DockStyle.Fill, Margin = new Padding(12, 14, 0, 14), ForeColor = FieldTheme.Green, BackColor = Color.FromArgb(8, 34, 38), Font = new Font("Consolas", 8F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleCenter };
                headerLayout.Controls.Add(policy, 1, 0); headerLayout.SetRowSpan(policy, 3);
                header.Controls.Add(headerLayout); shell.Controls.Add(header, 0, 0);

                var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = FieldTheme.Canvas, Margin = new Padding(0), Padding = new Padding(0) };
                body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 188));
                body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                body.Controls.Add(BuildSectionNavigation(), 0, 0);
                _sectionHost = new Panel { Dock = DockStyle.Fill, BackColor = FieldTheme.Surface, Margin = new Padding(12, 0, 0, 0), Padding = new Padding(0) };
                _sectionHost.Paint += delegate(object sender, PaintEventArgs args) { using (var pen = new Pen(FieldTheme.Stroke)) { args.Graphics.DrawRectangle(pen, 0, 0, Math.Max(0, _sectionHost.Width - 1), Math.Max(0, _sectionHost.Height - 1)); } };
                body.Controls.Add(_sectionHost, 1, 0);
                shell.Controls.Add(body, 0, 1);

                _sections[SettingsSection.Network] = BuildNetworkSection();
                _sections[SettingsSection.Trust] = BuildTrustSection();
                _sections[SettingsSection.Monitoring] = BuildMonitoringSection();
                _sections[SettingsSection.Verification] = BuildVerificationSection();
                _sections[SettingsSection.Integrations] = BuildIntegrationsSection();
                _sections[SettingsSection.Output] = BuildOutputSection();

                shell.Controls.Add(BuildFooter(), 0, 2);
                ShowSection(SettingsSection.Network);
            }

            private Control BuildSectionNavigation()
            {
                var panel = new FieldSurfacePanel { Dock = DockStyle.Fill, AccentColor = FieldTheme.Blue, BackColor = FieldTheme.Sidebar, Margin = new Padding(0), Padding = new Padding(10, 14, 10, 12) };
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 8, BackColor = FieldTheme.Sidebar, Margin = new Padding(0) };
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
                for (int index = 0; index < 6; index++) { layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); }
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                layout.Controls.Add(new Label { Text = "SEÇÕES DO PERFIL", Dock = DockStyle.Fill, ForeColor = FieldTheme.MutedText, Font = new Font("Segoe UI Semibold", 7.2F), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
                AddSectionButton(layout, 1, SettingsSection.Network, "[NW]  REDE LOCAL");
                AddSectionButton(layout, 2, SettingsSection.Trust, "[TR]  EXCEÇÕES");
                AddSectionButton(layout, 3, SettingsSection.Monitoring, "[CL]  COLETA");
                AddSectionButton(layout, 4, SettingsSection.Verification, "[VF]  VERIFICAÇÃO");
                AddSectionButton(layout, 5, SettingsSection.Integrations, "[IN]  INTEGRAÇÕES");
                AddSectionButton(layout, 6, SettingsSection.Output, "[IO]  SAÍDA");
                panel.Controls.Add(layout);
                return panel;
            }

            private void AddSectionButton(TableLayoutPanel layout, int row, SettingsSection section, string text)
            {
                var button = new FieldActionButton { Text = text, Dock = DockStyle.Fill, Margin = new Padding(0, 3, 0, 3), CaptionAlignment = ContentAlignment.MiddleLeft, Radius = 3, StartColor = FieldTheme.Sidebar, EndColor = FieldTheme.Sidebar, HoverStartColor = Color.FromArgb(16, 39, 59), HoverEndColor = Color.FromArgb(13, 31, 49), BorderColor = Color.Transparent, ForeColor = FieldTheme.SoftText, Font = new Font("Segoe UI Semibold", 7.8F) };
                button.Click += delegate { ShowSection(section); };
                _sectionButtons[section] = button;
                layout.Controls.Add(button, 0, row);
            }

            private Control BuildFooter()
            {
                var footer = new FieldSurfacePanel { Dock = DockStyle.Fill, AccentColor = FieldTheme.Cyan, Margin = new Padding(0, 12, 0, 0), Padding = new Padding(12, 8, 12, 8) };
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 2, BackColor = FieldTheme.Surface, Margin = new Padding(0) };
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 5));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 154));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 164));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 184));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 48));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
                _statusAccent = new Panel { Dock = DockStyle.Fill, BackColor = FieldTheme.Cyan, Margin = new Padding(0, 2, 10, 2), Width = 4 };
                layout.Controls.Add(_statusAccent, 0, 0); layout.SetRowSpan(_statusAccent, 2);
                var statusBox = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = FieldTheme.Surface, Margin = new Padding(12, 0, 8, 0) };
                statusBox.RowStyles.Add(new RowStyle(SizeType.Percent, 46)); statusBox.RowStyles.Add(new RowStyle(SizeType.Percent, 54));
                _statusTitle = new Label { Text = "PERFIL CARREGADO", Dock = DockStyle.Fill, ForeColor = FieldTheme.Cyan, Font = new Font("Consolas", 7.6F, FontStyle.Bold), TextAlign = ContentAlignment.BottomLeft };
                _statusDetail = new Label { Text = "Revise os parâmetros e salve para a próxima análise.", Dock = DockStyle.Fill, ForeColor = FieldTheme.SoftText, Font = new Font("Segoe UI", 8F), TextAlign = ContentAlignment.TopLeft, AutoEllipsis = true };
                statusBox.Controls.Add(_statusTitle, 0, 0); statusBox.Controls.Add(_statusDetail, 0, 1);
                layout.Controls.Add(statusBox, 1, 0); layout.SetRowSpan(statusBox, 2);

                var defaults = SecondaryButton("CARREGAR PADRÕES");
                defaults.Click += delegate { LoadDefaults(); };
                layout.Controls.Add(defaults, 2, 0); layout.SetRowSpan(defaults, 2);
                _discardButton = SecondaryButton("DESCARTAR E VOLTAR");
                _discardButton.ForeColor = FieldTheme.Amber; _discardButton.Visible = false;
                _discardButton.Click += delegate { RequestBack(true); };
                layout.Controls.Add(_discardButton, 3, 0); layout.SetRowSpan(_discardButton, 2);
                var back = SecondaryButton("VOLTAR AO PAINEL");
                back.Click += delegate { RequestBack(false); };
                layout.Controls.Add(back, 4, 0); layout.SetRowSpan(back, 2);
                _saveButton = new FieldActionButton { Text = "SALVAR ALTERAÇÕES", Dock = DockStyle.Fill, Margin = new Padding(10, 4, 0, 4), Font = new Font("Segoe UI Semibold", 8.3F), Radius = 4 };
                _saveButton.Click += delegate { SaveConfiguration(); };
                layout.Controls.Add(_saveButton, 5, 0); layout.SetRowSpan(_saveButton, 2);
                footer.Controls.Add(layout);
                return footer;
            }

            private FieldActionButton SecondaryButton(string text)
            {
                return new FieldActionButton { Text = text, Dock = DockStyle.Fill, Margin = new Padding(10, 4, 0, 4), StartColor = FieldTheme.SurfaceRaised, EndColor = Color.FromArgb(13, 27, 45), HoverStartColor = Color.FromArgb(28, 48, 70), HoverEndColor = Color.FromArgb(20, 38, 58), BorderColor = FieldTheme.Stroke, ForeColor = FieldTheme.SoftText, Font = new Font("Segoe UI Semibold", 7.6F), Radius = 4 };
            }

            private Control BuildNetworkSection()
            {
                FlowLayoutPanel flow = CreateSection("REDE LOCAL", "Selecione a interface e limite o domínio IPv4 que será analisado.", FieldTheme.Cyan);
                _interface = CreateInterfaceSelector();
                AddField(flow, "Interface de rede", "Automático é recomendado; a lista mostra somente interfaces IPv4 ativas.", _interface, 88);
                _cidr = CreateTextBox(false); AddField(flow, "Rede IPv4 / CIDR", "Opcional. Exemplo: 192.168.1.0/24. Vazio usa a rede da interface escolhida.", _cidr, 88);
                _maxHosts = CreateNumber(1, 65534, 4094, 1); AddField(flow, "Limite de endereços", "Evita uma varredura maior que o esperado pelo técnico.", _maxHosts, 88);
                return flow;
            }

            private Control BuildTrustSection()
            {
                FlowLayoutPanel flow = CreateSection("EXCLUSÕES E AUTORIZAÇÕES", "Itens nesta seção podem impedir a emissão de um conflito. Cada alteração exige confirmação adicional.", FieldTheme.Amber);
                AddNotice(flow, "EFEITO OPERACIONAL", "Use apenas para equipamentos, VIPs ou pares IP/MAC conhecidos. Um item incorreto pode ocultar um conflito verdadeiro.", FieldTheme.Amber);
                _excludedIps = CreateTextBox(true); AddField(flow, "IPv4 fora do escopo", "Um endereço por linha.", _excludedIps, 142);
                _excludedMacs = CreateTextBox(true); AddField(flow, "MACs fora do escopo", "Um endereço MAC unicast por linha.", _excludedMacs, 142);
                _trustedPairs = CreateTextBox(true); AddField(flow, "Pares autorizados", "Formato por linha: IPv4|MAC", _trustedPairs, 142);
                _trustedVirtualIps = CreateTextBox(true); AddField(flow, "VIPs autorizados", "IPv4 de alta disponibilidade conhecidos, um por linha.", _trustedVirtualIps, 142);
                _trustedMacs = CreateTextBox(true); AddField(flow, "MACs autorizados globalmente", "Use somente quando o comportamento do equipamento estiver documentado.", _trustedMacs, 142);
                return flow;
            }

            private Control BuildMonitoringSection()
            {
                FlowLayoutPanel flow = CreateSection("COLETA E CICLOS", "Controle frequência, descoberta auxiliar e resolução de nomes.", FieldTheme.Blue);
                _continuous = CreateToggle("Executar ciclos contínuos por padrão"); AddToggleField(flow, _continuous, "O botão Analisar rede continua executando apenas uma varredura.");
                _intervalSeconds = CreateNumber(1, Int32.MaxValue, 15, 1); AddField(flow, "Intervalo entre ciclos (segundos)", "Aplicado ao monitoramento contínuo; valores longos são preservados integralmente.", _intervalSeconds, 88);
                _captureSeconds = CreateNumber(2, 120, 5, 1); AddField(flow, "Captura de descoberta (segundos)", "Tempo da captura usada para encontrar candidatos.", _captureSeconds, 88);
                _captureWarmup = CreateNumber(0, 10000, 500, 100); AddField(flow, "Aquecimento da captura (ms)", "Espera antes de iniciar sondagens ativas.", _captureWarmup, 88);
                _evidenceWindow = CreateNumber(1, 1440, 10, 1); AddField(flow, "Janela de evidência (minutos)", "Somente observações recentes participam da decisão.", _evidenceWindow, 88);
                _historyExpiration = CreateNumber(1, 10080, 120, 10); AddField(flow, "Expiração do histórico (minutos)", "Deve ser igual ou maior que a janela de evidência.", _historyExpiration, 88);
                _alertCooldown = CreateNumber(0, 1440, 10, 1); AddField(flow, "Intervalo entre alertas (minutos)", "Evita notificações repetidas do mesmo conflito.", _alertCooldown, 88);
                _pingSweep = CreateToggle("Usar ICMP apenas para descoberta auxiliar"); AddToggleField(flow, _pingSweep, "Ping nunca confirma conflito.");
                _maxConcurrentPings = CreateNumber(1, 256, 48, 1); AddField(flow, "Pings simultâneos", "Faixa segura: 1 a 256.", _maxConcurrentPings, 88);
                _pingTimeout = CreateNumber(50, Int32.MaxValue, 400, 50); AddField(flow, "Timeout de ping (ms)", "Também limita sondagens auxiliares.", _pingTimeout, 88);
                _resolveHostnames = CreateToggle("Resolver nomes dos dispositivos"); AddToggleField(flow, _resolveHostnames, "A resolução é auxiliar e não altera o diagnóstico.");
                _hostnameTimeout = CreateNumber(100, 10000, 750, 50); AddField(flow, "Timeout de nome do dispositivo (ms)", "Limite para cada consulta de nome.", _hostnameTimeout, 88);
                _packetCapture = CreateToggle("Habilitar captura TShark"); AddToggleField(flow, _packetCapture, "Desativar impede a confirmação estrita e produz monitoramento limitado.");
                _activeArpProbe = CreateToggle("Habilitar sondagem ARP ativa"); AddToggleField(flow, _activeArpProbe, "Desativar impede a confirmação estrita e produz monitoramento limitado.");
                return flow;
            }

            private Control BuildVerificationSection()
            {
                FlowLayoutPanel flow = CreateSection("VERIFICAÇÃO ESTRITA", "Ajuste a repetição da prova sem desativar as garantias obrigatórias.", FieldTheme.Green);
                AddNotice(flow, "PROTEÇÕES BLOQUEADAS", "StrictEvidence | requisição capturada | respostas correlacionadas | fail-closed | uma verificação por vez", FieldTheme.Green);
                _verificationRounds = CreateNumber(2, 5, 3, 1); AddField(flow, "Rodadas de verificação", "Quantidade total de rodadas ARP por candidato.", _verificationRounds, 88);
                _requiredPositiveRounds = CreateNumber(2, 5, 2, 1); AddField(flow, "Rodadas positivas exigidas", "Não pode superar o total de rodadas.", _requiredPositiveRounds, 88);
                _requiredConfirmedCycles = CreateNumber(2, 5, 2, 1); AddField(flow, "Ciclos confirmados exigidos", "A mesma prova deve reaparecer em ciclos consecutivos.", _requiredConfirmedCycles, 88);
                _arpResponseWindow = CreateNumber(500, 5000, 1500, 100); AddField(flow, "Janela de resposta ARP (ms)", "Somente respostas correlacionadas dentro desta janela são aceitas.", _arpResponseWindow, 88);
                _detectProxyArp = CreateToggle("Bloquear confirmação sob risco de Proxy ARP"); AddToggleField(flow, _detectProxyArp, "Mantém o diagnóstico conservador em gateways e proxies.");
                _proxyArpThreshold = CreateNumber(2, 4096, 4, 1); AddField(flow, "Limiar de IPs por MAC", "Ajuda a identificar comportamento de Proxy ARP.", _proxyArpThreshold, 88);
                _arpProbeRate = CreateNumber(100, Int32.MaxValue, 250, 50); AddField(flow, "Intervalo mínimo entre sondagens (ms)", "Protege a rede contra excesso de solicitações ARP.", _arpProbeRate, 88);
                return flow;
            }

            private Control BuildIntegrationsSection()
            {
                FlowLayoutPanel flow = CreateSection("INTEGRAÇÕES", "Defina a captura e, opcionalmente, um canal seguro para alertas confirmados.", FieldTheme.Purple);
                _tsharkPath = CreateTextBox(false); AddField(flow, "Caminho do TShark", "Opcional. Deixe vazio para descoberta automática.", _tsharkPath, 88);
                _webhookEnabled = CreateToggle("Enviar conflitos confirmados por webhook"); AddToggleField(flow, _webhookEnabled, "Somente HTTPS ou loopback é aceito.");
                _webhookUrl = CreateTextBox(false); AddField(flow, "URL do webhook", "Nenhuma credencial é criada ou armazenada automaticamente.", _webhookUrl, 88);
                AddNotice(flow, "CAMPOS RESERVADOS", "Servidor DHCP, base OUI e Log de Eventos permanecem preservados no arquivo, mas ainda não são executados por esta versão.", FieldTheme.MutedText);
                return flow;
            }

            private Control BuildOutputSection()
            {
                FlowLayoutPanel flow = CreateSection("SAÍDA E RETENÇÃO", "Escolha onde guardar relatórios, estado e logs locais.", FieldTheme.Cyan);
                _outputDirectory = CreateTextBox(false); AddField(flow, "Pasta de dados", "Vazio usa %LOCALAPPDATA%\\IPConflictMonitor. Variáveis de ambiente são aceitas.", _outputDirectory, 88);
                _logMaxMb = CreateNumber(1, Int32.MaxValue, 10, 1); AddField(flow, "Tamanho máximo do log (MB)", "Ao atingir o limite, o log é rotacionado.", _logMaxMb, 88);
                _logRetention = CreateNumber(1, 100, 7, 1); AddField(flow, "Arquivos de log mantidos", "Quantidade de versões rotacionadas.", _logRetention, 88);
                _snapshotCsv = CreateToggle("Gerar snapshot CSV"); AddToggleField(flow, _snapshotCsv, "Visão atual de todos os endereços observados.");
                _conflictCsv = CreateToggle("Registrar conflitos em CSV"); AddToggleField(flow, _conflictCsv, "Histórico dos conflitos confirmados.");
                _jsonState = CreateToggle("Persistir estado JSON"); AddToggleField(flow, _jsonState, "Necessário para acompanhar ciclos e recuperação entre execuções.");
                return flow;
            }

            private FlowLayoutPanel CreateSection(string title, string description, Color accent)
            {
                var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, BackColor = FieldTheme.Surface, Padding = new Padding(18, 16, 18, 18), Margin = new Padding(0) };
                var heading = new FieldSurfacePanel { Height = 70, BackColor = FieldTheme.SurfaceRaised, AccentColor = accent, Padding = new Padding(18, 8, 14, 7) };
                var headingLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = FieldTheme.SurfaceRaised, Margin = new Padding(0) };
                headingLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 48)); headingLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 52));
                headingLayout.Controls.Add(new Label { Text = title, Dock = DockStyle.Fill, ForeColor = accent, Font = new Font("Bahnschrift SemiCondensed", 12F, FontStyle.Bold), TextAlign = ContentAlignment.BottomLeft }, 0, 0);
                headingLayout.Controls.Add(new Label { Text = description, Dock = DockStyle.Fill, ForeColor = FieldTheme.SoftText, Font = new Font("Segoe UI", 8.2F), TextAlign = ContentAlignment.TopLeft, AutoEllipsis = true }, 0, 1);
                heading.Controls.Add(headingLayout); AddBlock(flow, heading);
                flow.SizeChanged += delegate { ResizeBlocks(flow); };
                return flow;
            }

            private void AddField(FlowLayoutPanel flow, string title, string hint, Control input, int height)
            {
                var panel = new Panel { Height = height, BackColor = FieldTheme.Surface, Padding = new Padding(14, 7, 14, 9) };
                panel.Paint += delegate(object sender, PaintEventArgs args) { using (var pen = new Pen(Color.FromArgb(30, 50, 72))) { args.Graphics.DrawRectangle(pen, 0, 0, Math.Max(0, panel.Width - 1), Math.Max(0, panel.Height - 1)); } };
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, BackColor = FieldTheme.Surface, Margin = new Padding(0) };
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 20)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                layout.Controls.Add(new Label { Text = title, Dock = DockStyle.Fill, ForeColor = FieldTheme.MainText, Font = new Font("Segoe UI Semibold", 8.5F), TextAlign = ContentAlignment.BottomLeft }, 0, 0);
                layout.Controls.Add(new Label { Text = hint, Dock = DockStyle.Fill, ForeColor = FieldTheme.MutedText, Font = new Font("Segoe UI", 7.6F), TextAlign = ContentAlignment.TopLeft, AutoEllipsis = true }, 0, 1);
                input.Dock = DockStyle.Fill; input.Margin = new Padding(0, 3, 0, 0); layout.Controls.Add(input, 0, 2);
                panel.Controls.Add(layout); AddBlock(flow, panel); Track(input);
            }

            private void AddToggleField(FlowLayoutPanel flow, CheckBox toggle, string hint)
            {
                var panel = new Panel { Height = 68, BackColor = FieldTheme.Surface, Padding = new Padding(14, 8, 14, 7) };
                panel.Paint += delegate(object sender, PaintEventArgs args) { using (var pen = new Pen(Color.FromArgb(30, 50, 72))) { args.Graphics.DrawRectangle(pen, 0, 0, Math.Max(0, panel.Width - 1), Math.Max(0, panel.Height - 1)); } };
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = FieldTheme.Surface, Margin = new Padding(0) };
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 55)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
                toggle.Dock = DockStyle.Fill; layout.Controls.Add(toggle, 0, 0);
                layout.Controls.Add(new Label { Text = hint, Dock = DockStyle.Fill, ForeColor = FieldTheme.MutedText, Font = new Font("Segoe UI", 7.6F), TextAlign = ContentAlignment.TopLeft, AutoEllipsis = true }, 0, 1);
                panel.Controls.Add(layout); AddBlock(flow, panel); Track(toggle);
            }

            private void AddNotice(FlowLayoutPanel flow, string title, string detail, Color color)
            {
                var panel = new FieldSurfacePanel { Height = 72, BackColor = FieldTheme.SurfaceRaised, AccentColor = color, Padding = new Padding(18, 8, 14, 8) };
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = FieldTheme.SurfaceRaised, Margin = new Padding(0) };
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 45)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
                layout.Controls.Add(new Label { Text = title, Dock = DockStyle.Fill, ForeColor = color, Font = new Font("Consolas", 7.8F, FontStyle.Bold), TextAlign = ContentAlignment.BottomLeft }, 0, 0);
                layout.Controls.Add(new Label { Text = detail, Dock = DockStyle.Fill, ForeColor = FieldTheme.SoftText, Font = new Font("Segoe UI", 7.9F), TextAlign = ContentAlignment.TopLeft, AutoEllipsis = true }, 0, 1);
                panel.Controls.Add(layout); AddBlock(flow, panel);
            }

            private static void AddBlock(FlowLayoutPanel flow, Control control)
            {
                control.Width = 720; control.Margin = new Padding(0, 0, 0, 10); flow.Controls.Add(control);
            }

            private static void ResizeBlocks(FlowLayoutPanel flow)
            {
                int width = Math.Max(420, flow.ClientSize.Width - SystemInformation.VerticalScrollBarWidth - 38);
                foreach (Control control in flow.Controls) { control.Width = width; }
            }

            private ComboBox CreateInterfaceSelector()
            {
                var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 23, FlatStyle = FlatStyle.Flat, BackColor = FieldTheme.SurfaceRaised, ForeColor = FieldTheme.MainText, Font = new Font("Segoe UI", 8.7F), IntegralHeight = false, MaxDropDownItems = 10, AccessibleName = "Interface de rede" };
                combo.DrawItem += delegate(object sender, DrawItemEventArgs args)
                {
                    if (args.Index < 0) { return; }
                    bool selected = (args.State & DrawItemState.Selected) == DrawItemState.Selected;
                    Color background = selected ? Color.FromArgb(20, 58, 76) : FieldTheme.SurfaceRaised;
                    Color foreground = selected ? FieldTheme.Cyan : FieldTheme.MainText;
                    using (var fill = new SolidBrush(background))
                    {
                        args.Graphics.FillRectangle(fill, args.Bounds);
                    }
                    string label = combo.GetItemText(combo.Items[args.Index]);
                    TextRenderer.DrawText(args.Graphics, label, combo.Font, new Rectangle(args.Bounds.X + 8, args.Bounds.Y, Math.Max(1, args.Bounds.Width - 12), args.Bounds.Height), foreground, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    if ((args.State & DrawItemState.Focus) == DrawItemState.Focus)
                    {
                        using (var focus = new Pen(FieldTheme.Cyan, 1F)) { args.Graphics.DrawRectangle(focus, args.Bounds.X, args.Bounds.Y, Math.Max(0, args.Bounds.Width - 1), Math.Max(0, args.Bounds.Height - 1)); }
                    }
                };
                combo.Items.Add(new InterfaceOption { Index = 0, Label = "Automático (recomendado)" });
                try
                {
                    foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces().Where(delegate(NetworkInterface item) { return item.OperationalStatus == OperationalStatus.Up && item.NetworkInterfaceType != NetworkInterfaceType.Loopback; }))
                    {
                        IPInterfaceProperties properties; IPv4InterfaceProperties ipv4;
                        try { properties = adapter.GetIPProperties(); ipv4 = properties.GetIPv4Properties(); } catch { continue; }
                        if (ipv4 == null) { continue; }
                        IPAddress address = properties.UnicastAddresses.Select(delegate(UnicastIPAddressInformation item) { return item.Address; }).FirstOrDefault(delegate(IPAddress item) { return item != null && item.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(item); });
                        if (address == null) { continue; }
                        combo.Items.Add(new InterfaceOption { Index = ipv4.Index, Label = adapter.Name + "  /  " + address + "  /  índice " + ipv4.Index });
                    }
                }
                catch { }
                return combo;
            }

            private static TextBox CreateTextBox(bool multiline)
            {
                return new TextBox { Multiline = multiline, AcceptsReturn = multiline, ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None, BorderStyle = BorderStyle.FixedSingle, BackColor = FieldTheme.SurfaceRaised, ForeColor = FieldTheme.MainText, Font = new Font(multiline ? "Consolas" : "Segoe UI", multiline ? 8.1F : 8.7F), WordWrap = false };
            }

            private static NumericUpDown CreateNumber(decimal minimum, decimal maximum, decimal value, decimal increment)
            {
                return new NumericUpDown { Minimum = minimum, Maximum = maximum, Value = Math.Max(minimum, Math.Min(maximum, value)), Increment = increment, ThousandsSeparator = true, BorderStyle = BorderStyle.FixedSingle, BackColor = FieldTheme.SurfaceRaised, ForeColor = FieldTheme.MainText, Font = new Font("Consolas", 8.6F), TextAlign = HorizontalAlignment.Left };
            }

            private static CheckBox CreateToggle(string text)
            {
                return new CheckBox { Text = text, AutoSize = false, FlatStyle = FlatStyle.Flat, ForeColor = FieldTheme.MainText, BackColor = FieldTheme.Surface, Font = new Font("Segoe UI Semibold", 8.4F), CheckAlign = ContentAlignment.MiddleLeft, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(2, 0, 0, 0), AccessibleName = text };
            }

            private void Track(Control control)
            {
                TextBox text = control as TextBox; if (text != null) { text.TextChanged += delegate { MarkDirty(); }; return; }
                NumericUpDown number = control as NumericUpDown; if (number != null) { number.ValueChanged += delegate { MarkDirty(); }; return; }
                CheckBox check = control as CheckBox; if (check != null) { check.CheckedChanged += delegate { MarkDirty(); }; return; }
                ComboBox combo = control as ComboBox; if (combo != null) { combo.SelectedIndexChanged += delegate { MarkDirty(); }; }
            }

            private void ShowSection(SettingsSection section)
            {
                Control current = _sectionHost.Controls.Count > 0 ? _sectionHost.Controls[0] : null;
                if (current != null) { _sectionHost.Controls.Remove(current); }
                Control selected = _sections[section];
                selected.Dock = DockStyle.Fill; _sectionHost.Controls.Add(selected); selected.BringToFront();
                foreach (KeyValuePair<SettingsSection, FieldActionButton> item in _sectionButtons)
                {
                    bool active = item.Key == section;
                    item.Value.StartColor = active ? Color.FromArgb(17, 57, 73) : FieldTheme.Sidebar;
                    item.Value.EndColor = active ? Color.FromArgb(14, 43, 64) : FieldTheme.Sidebar;
                    item.Value.BorderColor = active ? Color.FromArgb(45, 117, 134) : Color.Transparent;
                    item.Value.ForeColor = active ? FieldTheme.Cyan : FieldTheme.SoftText;
                    item.Value.Invalidate();
                }
            }

            private void LoadConfiguration()
            {
                try
                {
                    _configuration = ConfigurationStore.Load(_configurationPath);
                    Populate(_configuration);
                    _baselineSensitive = SensitiveFingerprint(_configuration);
                    List<string> warnings = ConfigurationStore.Validate(_configuration);
                    if (warnings.Count > 0) { ShowStatus(FieldTheme.Amber, "CONFIGURAÇÃO EXIGE REVISÃO", warnings[0]); }
                    else if (_workerRunning) { ShowStatus(FieldTheme.Amber, "ANÁLISE EM EXECUÇÃO", "Mudanças salvas serão aplicadas somente quando uma nova análise for iniciada."); }
                    else { ShowStatus(FieldTheme.Green, "PERFIL VALIDADO", "As proteções obrigatórias estão ativas. Alterações valem na próxima análise."); }
                }
                catch (Exception exception)
                {
                    _configuration = new MonitorConfiguration();
                    Populate(_configuration);
                    _baselineSensitive = SensitiveFingerprint(_configuration);
                    _dirty = true;
                    _sensitiveConfirmationArmed = false;
                    _discardButton.Visible = true;
                    _saveButton.Enabled = true;
                    ShowStatus(FieldTheme.Red, "PERFIL PRECISA SER RECRIADO", FriendlyConfigurationError(exception) + " Os padrões seguros foram carregados; revise e salve para reparar o perfil.");
                }
            }

            private void Populate(MonitorConfiguration configuration)
            {
                _loading = true;
                try
                {
                    SelectInterface(configuration.Network.InterfaceIndex);
                    _cidr.Text = configuration.Network.CIDR ?? String.Empty;
                    SetNumber(_maxHosts, configuration.Network.MaxHosts);
                    _excludedIps.Text = JoinLines(configuration.Network.ExcludedIPs);
                    _excludedMacs.Text = JoinLines(configuration.Network.ExcludedMACs);
                    _trustedPairs.Text = JoinLines(configuration.Network.TrustedPairs);
                    _trustedVirtualIps.Text = JoinLines(configuration.Network.TrustedVirtualIps);
                    _trustedMacs.Text = JoinLines(configuration.Network.TrustedMacs);
                    _continuous.Checked = configuration.Monitoring.Continuous;
                    SetNumber(_intervalSeconds, configuration.Monitoring.IntervalSeconds);
                    SetNumber(_captureSeconds, configuration.Monitoring.CaptureSeconds);
                    SetNumber(_captureWarmup, configuration.Monitoring.CaptureWarmupMilliseconds);
                    SetNumber(_evidenceWindow, configuration.Monitoring.EvidenceWindowMinutes);
                    SetNumber(_historyExpiration, configuration.Monitoring.HistoryExpirationMinutes);
                    SetNumber(_alertCooldown, configuration.Monitoring.AlertCooldownMinutes);
                    _pingSweep.Checked = configuration.Monitoring.PingSweepEnabled;
                    SetNumber(_maxConcurrentPings, configuration.Monitoring.MaxConcurrentPings);
                    SetNumber(_pingTimeout, configuration.Monitoring.PingTimeoutMs);
                    _resolveHostnames.Checked = configuration.Monitoring.ResolveHostnames;
                    SetNumber(_hostnameTimeout, configuration.Monitoring.HostnameTimeoutMs);
                    _packetCapture.Checked = configuration.Monitoring.PacketCaptureEnabled;
                    _activeArpProbe.Checked = configuration.Monitoring.ActiveArpProbeEnabled;
                    SetNumber(_verificationRounds, configuration.Monitoring.VerificationRounds);
                    SetNumber(_requiredPositiveRounds, configuration.Monitoring.RequiredPositiveRounds);
                    SetNumber(_requiredConfirmedCycles, configuration.Monitoring.RequiredConfirmedCycles);
                    SetNumber(_arpResponseWindow, configuration.Monitoring.ArpResponseWindowMs);
                    _detectProxyArp.Checked = configuration.Monitoring.DetectProxyArp;
                    SetNumber(_proxyArpThreshold, configuration.Monitoring.ProxyArpIpThreshold);
                    SetNumber(_arpProbeRate, configuration.Monitoring.ArpProbeRateLimitMs);
                    _tsharkPath.Text = configuration.Integrations.TSharkPath ?? String.Empty;
                    _webhookEnabled.Checked = configuration.Integrations.WebhookEnabled;
                    _webhookUrl.Text = configuration.Integrations.WebhookUrl ?? String.Empty;
                    _outputDirectory.Text = configuration.Output.Directory ?? String.Empty;
                    SetNumber(_logMaxMb, configuration.Output.LogMaxMB);
                    SetNumber(_logRetention, configuration.Output.LogRetentionFiles);
                    _snapshotCsv.Checked = configuration.Output.SnapshotCsv;
                    _conflictCsv.Checked = configuration.Output.ConflictCsv;
                    _jsonState.Checked = configuration.Output.JsonState;
                    _dirty = false; _sensitiveConfirmationArmed = false; _discardButton.Visible = false; _saveButton.Text = "SALVAR ALTERAÇÕES";
                }
                finally { _loading = false; }
            }

            private void SelectInterface(int index)
            {
                for (int itemIndex = 0; itemIndex < _interface.Items.Count; itemIndex++)
                {
                    InterfaceOption option = _interface.Items[itemIndex] as InterfaceOption;
                    if (option != null && option.Index == index) { _interface.SelectedIndex = itemIndex; return; }
                }
                if (index > 0)
                {
                    _interface.Items.Add(new InterfaceOption { Index = index, Label = "Índice configurado " + index + " (não disponível agora)" });
                    _interface.SelectedIndex = _interface.Items.Count - 1;
                }
                else { _interface.SelectedIndex = 0; }
            }

            private void SaveConfiguration()
            {
                if (!_dirty) { ShowStatus(FieldTheme.Cyan, "SEM ALTERAÇÕES", "O perfil ativo já contém estes valores."); return; }
                MonitorConfiguration candidate = BuildConfiguration();
                List<string> errors = ConfigurationStore.Validate(candidate);
                if (errors.Count > 0)
                {
                    ShowStatus(FieldTheme.Red, "CORRIJA A CONFIGURAÇÃO", errors[0] + (errors.Count > 1 ? "  +" + (errors.Count - 1) + " item(ns)." : String.Empty));
                    FocusFirstError(errors[0]); return;
                }
                if (!String.Equals(_baselineSensitive, SensitiveFingerprint(candidate), StringComparison.Ordinal) && !_sensitiveConfirmationArmed)
                {
                    _sensitiveConfirmationArmed = true;
                    _saveButton.Text = "CONFIRMAR E SALVAR";
                    ShowStatus(FieldTheme.Amber, "REVISE AS EXCEÇÕES", "Esses itens podem suprimir alertas. Pressione Confirmar e salvar para aplicar conscientemente.");
                    _saveButton.Focus(); return;
                }

                _saveButton.Enabled = false;
                string originalText = _saveButton.Text;
                _saveButton.Text = "SALVANDO...";
                try
                {
                    ConfigurationStore.Save(candidate, _configurationPath);
                    _configuration = ConfigurationStore.Load(_configurationPath);
                    Populate(_configuration);
                    _baselineSensitive = SensitiveFingerprint(_configuration);
                    string detail = _workerRunning ? "Perfil salvo com backup. Reinicie a análise para aplicar as mudanças." : "Perfil salvo com backup e pronto para a próxima análise.";
                    if (!String.IsNullOrWhiteSpace(_configuration.Integrations.TSharkPath) && !File.Exists(Environment.ExpandEnvironmentVariables(_configuration.Integrations.TSharkPath)))
                    {
                        ShowStatus(FieldTheme.Amber, "PERFIL SALVO COM AVISO", "O caminho informado para o TShark ainda não existe. " + detail);
                    }
                    else { ShowStatus(FieldTheme.Green, "ALTERAÇÕES SALVAS", detail); }
                }
                catch (UnauthorizedAccessException)
                {
                    ShowStatus(FieldTheme.Red, "SEM PERMISSÃO PARA SALVAR", "Verifique as permissões da pasta de perfil do Windows e tente novamente.");
                }
                catch (Exception exception)
                {
                    ShowStatus(FieldTheme.Red, "FALHA AO SALVAR", FriendlyConfigurationError(exception));
                }
                finally
                {
                    _saveButton.Enabled = true;
                    if (_dirty) { _saveButton.Text = originalText; }
                }
            }

            private MonitorConfiguration BuildConfiguration()
            {
                MonitorConfiguration configuration = _configuration ?? new MonitorConfiguration();
                InterfaceOption selected = _interface.SelectedItem as InterfaceOption;
                configuration.Network.InterfaceIndex = selected == null ? 0 : selected.Index;
                configuration.Network.CIDR = (_cidr.Text ?? String.Empty).Trim();
                configuration.Network.MaxHosts = Decimal.ToInt32(_maxHosts.Value);
                configuration.Network.ExcludedIPs = NormalizeIpLines(_excludedIps.Text);
                configuration.Network.ExcludedMACs = NormalizeMacLines(_excludedMacs.Text);
                configuration.Network.TrustedPairs = NormalizePairLines(_trustedPairs.Text);
                configuration.Network.TrustedVirtualIps = NormalizeIpLines(_trustedVirtualIps.Text);
                configuration.Network.TrustedMacs = NormalizeMacLines(_trustedMacs.Text);

                configuration.Monitoring.Continuous = _continuous.Checked;
                configuration.Monitoring.IntervalSeconds = Decimal.ToInt32(_intervalSeconds.Value);
                configuration.Monitoring.CaptureSeconds = Decimal.ToInt32(_captureSeconds.Value);
                configuration.Monitoring.CaptureWarmupMilliseconds = Decimal.ToInt32(_captureWarmup.Value);
                configuration.Monitoring.EvidenceWindowMinutes = Decimal.ToInt32(_evidenceWindow.Value);
                configuration.Monitoring.HistoryExpirationMinutes = Decimal.ToInt32(_historyExpiration.Value);
                configuration.Monitoring.AlertCooldownMinutes = Decimal.ToInt32(_alertCooldown.Value);
                configuration.Monitoring.PingSweepEnabled = _pingSweep.Checked;
                configuration.Monitoring.MaxConcurrentPings = Decimal.ToInt32(_maxConcurrentPings.Value);
                configuration.Monitoring.PingTimeoutMs = Decimal.ToInt32(_pingTimeout.Value);
                configuration.Monitoring.ResolveHostnames = _resolveHostnames.Checked;
                configuration.Monitoring.HostnameTimeoutMs = Decimal.ToInt32(_hostnameTimeout.Value);
                configuration.Monitoring.PacketCaptureEnabled = _packetCapture.Checked;
                configuration.Monitoring.ActiveArpProbeEnabled = _activeArpProbe.Checked;
                configuration.Monitoring.DetectionMode = "StrictEvidence";
                configuration.Monitoring.VerificationRounds = Decimal.ToInt32(_verificationRounds.Value);
                configuration.Monitoring.RequiredPositiveRounds = Decimal.ToInt32(_requiredPositiveRounds.Value);
                configuration.Monitoring.RequiredConfirmedCycles = Decimal.ToInt32(_requiredConfirmedCycles.Value);
                configuration.Monitoring.ArpResponseWindowMs = Decimal.ToInt32(_arpResponseWindow.Value);
                configuration.Monitoring.RequireCapturedArpRequest = true;
                configuration.Monitoring.RequireCorrelatedArpResponses = true;
                configuration.Monitoring.FailClosedWithoutCapture = true;
                configuration.Monitoring.DetectProxyArp = _detectProxyArp.Checked;
                configuration.Monitoring.ProxyArpIpThreshold = Decimal.ToInt32(_proxyArpThreshold.Value);
                configuration.Monitoring.MaxConcurrentVerifications = 1;
                configuration.Monitoring.ArpProbeRateLimitMs = Decimal.ToInt32(_arpProbeRate.Value);

                configuration.Integrations.TSharkPath = (_tsharkPath.Text ?? String.Empty).Trim();
                configuration.Integrations.WebhookEnabled = _webhookEnabled.Checked;
                configuration.Integrations.WebhookUrl = (_webhookUrl.Text ?? String.Empty).Trim();
                configuration.Output.Directory = (_outputDirectory.Text ?? String.Empty).Trim();
                configuration.Output.LogMaxMB = Decimal.ToInt32(_logMaxMb.Value);
                configuration.Output.LogRetentionFiles = Decimal.ToInt32(_logRetention.Value);
                configuration.Output.SnapshotCsv = _snapshotCsv.Checked;
                configuration.Output.ConflictCsv = _conflictCsv.Checked;
                configuration.Output.JsonState = _jsonState.Checked;
                return configuration;
            }

            private void LoadDefaults()
            {
                _configuration = new MonitorConfiguration();
                Populate(_configuration);
                _dirty = true;
                _discardButton.Visible = true;
                _saveButton.Enabled = true;
                ShowStatus(FieldTheme.Cyan, "PADRÕES CARREGADOS", "Revise os valores e use Salvar alterações para aplicá-los.");
            }

            private void MarkDirty()
            {
                if (_loading) { return; }
                _dirty = true; _sensitiveConfirmationArmed = false; _saveButton.Text = "SALVAR ALTERAÇÕES"; _discardButton.Visible = true;
                ShowStatus(FieldTheme.Cyan, "ALTERAÇÕES PENDENTES", "Os valores ainda não foram gravados. Alterações valem na próxima análise.");
            }

            private void RequestBack(bool discard)
            {
                if (_dirty && !discard) { ShowUnsavedWarning(); return; }
                if (discard) { _dirty = false; _sensitiveConfirmationArmed = false; }
                if (BackRequested != null) { BackRequested(); }
            }

            private void ShowStatus(Color color, string title, string detail)
            {
                _statusAccent.BackColor = color; _statusTitle.ForeColor = color; _statusTitle.Text = title; _statusDetail.Text = detail;
            }

            private void FocusFirstError(string error)
            {
                if (error.IndexOf("CIDR", StringComparison.OrdinalIgnoreCase) >= 0) { ShowSection(SettingsSection.Network); _cidr.Focus(); }
                else if (error.IndexOf("Trusted", StringComparison.OrdinalIgnoreCase) >= 0 || error.IndexOf("MAC", StringComparison.OrdinalIgnoreCase) >= 0 || error.IndexOf("IPv4", StringComparison.OrdinalIgnoreCase) >= 0) { ShowSection(SettingsSection.Trust); _trustedPairs.Focus(); }
                else if (error.IndexOf("Webhook", StringComparison.OrdinalIgnoreCase) >= 0) { ShowSection(SettingsSection.Integrations); _webhookUrl.Focus(); }
                else if (error.IndexOf("Output", StringComparison.OrdinalIgnoreCase) >= 0) { ShowSection(SettingsSection.Output); _outputDirectory.Focus(); }
                else { ShowSection(SettingsSection.Verification); _verificationRounds.Focus(); }
            }

            private static void SetNumber(NumericUpDown control, int value)
            {
                control.Value = Math.Max(control.Minimum, Math.Min(control.Maximum, value));
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    foreach (Control section in _sections.Values.ToArray())
                    {
                        if (section != null && !section.IsDisposed) { section.Dispose(); }
                    }
                    _sections.Clear();
                    _sectionButtons.Clear();
                    BackRequested = null;
                }
                base.Dispose(disposing);
            }

            private static string JoinLines(IEnumerable<string> values)
            {
                return String.Join(Environment.NewLine, (values ?? new string[0]).Where(delegate(string value) { return !String.IsNullOrWhiteSpace(value); }).ToArray());
            }

            private static string[] SplitLines(string value)
            {
                return (value ?? String.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(delegate(string item) { return item.Trim(); }).Where(delegate(string item) { return item.Length > 0; }).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            }

            private static string[] NormalizeIpLines(string value)
            {
                return SplitLines(value).Select(delegate(string item) { IPAddress ip; return IPAddress.TryParse(item, out ip) && ip.AddressFamily == AddressFamily.InterNetwork ? ip.ToString() : item; }).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            }

            private static string[] NormalizeMacLines(string value)
            {
                return SplitLines(value).Select(delegate(string item) { string normalized = StrictEvidenceDecisionEngine.NormalizeAndValidateMac(item); return normalized ?? item; }).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            }

            private static string[] NormalizePairLines(string value)
            {
                return SplitLines(value).Select(delegate(string item)
                {
                    string[] parts = item.Split('|');
                    if (parts.Length != 2) { return item; }
                    IPAddress ip; string normalized = StrictEvidenceDecisionEngine.NormalizeAndValidateMac(parts[1].Trim());
                    string address = IPAddress.TryParse(parts[0].Trim(), out ip) && ip.AddressFamily == AddressFamily.InterNetwork ? ip.ToString() : parts[0].Trim();
                    return address + "|" + (normalized ?? parts[1].Trim());
                }).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            }

            private static string SensitiveFingerprint(MonitorConfiguration configuration)
            {
                if (configuration == null || configuration.Network == null) { return String.Empty; }
                return String.Join("\n", new[]
                {
                    JoinLines(configuration.Network.ExcludedIPs), JoinLines(configuration.Network.ExcludedMACs), JoinLines(configuration.Network.TrustedPairs), JoinLines(configuration.Network.TrustedVirtualIps), JoinLines(configuration.Network.TrustedMacs)
                });
            }

            private static string FriendlyConfigurationError(Exception exception)
            {
                Exception current = exception;
                while (current.InnerException != null) { current = current.InnerException; }
                string message = current.Message ?? "Falha desconhecida.";
                return message.Replace(Environment.NewLine, "  ");
            }
        }

        private sealed class OperatorGuidePage : UserControl
        {
            public event Action BackRequested;

            public OperatorGuidePage()
            {
                Dock = DockStyle.Fill; BackColor = FieldTheme.Canvas; ForeColor = FieldTheme.MainText; Padding = new Padding(20, 12, 20, 14); Font = new Font("Segoe UI", 9F); AutoScaleMode = AutoScaleMode.Dpi;
                var shell = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, BackColor = FieldTheme.Canvas, Margin = new Padding(0) };
                shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 122)); shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
                var header = new FieldSurfacePanel { Dock = DockStyle.Fill, HeaderTreatment = true, AccentColor = FieldTheme.Blue, Margin = new Padding(0, 0, 0, 12), Padding = new Padding(22, 12, 18, 10) };
                var head = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, BackColor = Color.Transparent, Margin = new Padding(0) };
                head.RowStyles.Add(new RowStyle(SizeType.Absolute, 20)); head.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); head.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));
                head.Controls.Add(new Label { Text = "GUIA OPERACIONAL / CAMPO", Dock = DockStyle.Fill, ForeColor = FieldTheme.Cyan, Font = new Font("Segoe UI Semibold", 7.4F), TextAlign = ContentAlignment.BottomLeft, BackColor = Color.Transparent }, 0, 0);
                head.Controls.Add(new Label { Text = "Como interpretar o diagnóstico", Dock = DockStyle.Fill, ForeColor = FieldTheme.MainText, Font = new Font("Bahnschrift SemiCondensed", 22F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.Transparent }, 0, 1);
                head.Controls.Add(new Label { Text = "Um conflito só aparece depois de prova ARP ativa, correlacionada e repetida.", Dock = DockStyle.Fill, ForeColor = FieldTheme.SoftText, Font = new Font("Segoe UI", 8.3F), TextAlign = ContentAlignment.TopLeft, BackColor = Color.Transparent }, 0, 2);
                header.Controls.Add(head); shell.Controls.Add(header, 0, 0);

                var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, BackColor = FieldTheme.Canvas, Margin = new Padding(0), Padding = new Padding(0) };
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
                grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50)); grid.RowStyles.Add(new RowStyle(SizeType.Percent, 50));
                grid.Controls.Add(GuideCard("[01]  ANALISAR", "Analisar rede executa discovery e verificação estrita uma vez. Monitorar repete os ciclos enquanto o painel estiver aberto.", FieldTheme.Cyan, new Padding(0, 0, 6, 6)), 0, 0);
                grid.Controls.Add(GuideCard("[02]  LER O ESTADO", "Normal não possui prova atual. Não verificado é informação insuficiente. Limitado indica ausência de captura confiável.", FieldTheme.Blue, new Padding(6, 0, 0, 6)), 1, 0);
                grid.Controls.Add(GuideCard("[03]  VALIDAR A PROVA", "Conflito confirmado exige dois MACs estáveis, rodadas positivas e ciclos consecutivos. Abra a linha para conferir o Evidence ID.", FieldTheme.Red, new Padding(0, 6, 6, 0)), 0, 1);
                grid.Controls.Add(GuideCard("[04]  GARANTIR A CAPTURA", "TShark, Npcap, permissão de captura e visibilidade Layer 2 são necessários. Sem eles, o sistema permanece conservador.", FieldTheme.Green, new Padding(6, 6, 0, 0)), 1, 1);
                shell.Controls.Add(grid, 0, 1);

                var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = FieldTheme.Canvas, Margin = new Padding(0), Padding = new Padding(0, 12, 0, 0) };
                footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
                footer.Controls.Add(new Label { Text = "STRICT EVIDENCE  /  DECISÃO CONSERVADORA", Dock = DockStyle.Fill, ForeColor = FieldTheme.Green, Font = new Font("Consolas", 7.7F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
                var back = new FieldActionButton { Text = "VOLTAR AO PAINEL", Dock = DockStyle.Fill, Margin = new Padding(8, 0, 0, 0), StartColor = FieldTheme.SurfaceRaised, EndColor = Color.FromArgb(13, 27, 45), HoverStartColor = Color.FromArgb(28, 48, 70), HoverEndColor = Color.FromArgb(20, 38, 58), BorderColor = FieldTheme.Stroke, ForeColor = FieldTheme.SoftText, Font = new Font("Segoe UI Semibold", 8F), Radius = 4 };
                back.Click += delegate { if (BackRequested != null) { BackRequested(); } };
                footer.Controls.Add(back, 1, 0); shell.Controls.Add(footer, 0, 2);
                Controls.Add(shell);
            }

            private static Control GuideCard(string title, string body, Color color, Padding margin)
            {
                var card = new FieldSurfacePanel { Dock = DockStyle.Fill, Margin = margin, Padding = new Padding(20, 16, 18, 14), AccentColor = color, BackColor = FieldTheme.Surface };
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = FieldTheme.Surface, Margin = new Padding(0) };
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                layout.Controls.Add(new Label { Text = title, Dock = DockStyle.Fill, ForeColor = color, Font = new Font("Bahnschrift SemiCondensed", 12F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
                layout.Controls.Add(new Label { Text = body, Dock = DockStyle.Fill, ForeColor = FieldTheme.SoftText, Font = new Font("Segoe UI", 9F), TextAlign = ContentAlignment.TopLeft }, 0, 1);
                card.Controls.Add(layout); return card;
            }
        }

        private static int CaptureSettingsInterface(string outputPath)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (String.IsNullOrWhiteSpace(outputPath)) { outputPath = Path.Combine(Path.GetTempPath(), "IPConflictMonitor-settings.png"); }
            outputPath = Path.GetFullPath(outputPath);
            string directory = Path.GetDirectoryName(outputPath);
            if (!String.IsNullOrWhiteSpace(directory)) { Directory.CreateDirectory(directory); }
            using (var form = new NetworkOperationsForm(true))
            {
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(20, 20);
                form.Show();
                Application.DoEvents();
                form.PrepareSettingsPreview();
                Application.DoEvents();
                using (var bitmap = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                    TableLayoutPanel sidebar = FindSettingsPreviewSidebar(form);
                    if (sidebar != null)
                    {
                        Point screenLocation = sidebar.PointToScreen(Point.Empty);
                        Rectangle sidebarBounds = new Rectangle(screenLocation.X - form.Left, screenLocation.Y - form.Top, sidebar.Width, sidebar.Height);
                        sidebar.DrawToBitmap(bitmap, sidebarBounds);
                    }
                    bitmap.Save(outputPath, ImageFormat.Png);
                }
                form.Close();
            }
            return 0;
        }

        private static TableLayoutPanel FindSettingsPreviewSidebar(Control root)
        {
            foreach (Control control in root.Controls)
            {
                var table = control as TableLayoutPanel;
                if (table != null && table.ColumnCount == 1 && table.RowCount == 9) { return table; }
                TableLayoutPanel nested = FindSettingsPreviewSidebar(control);
                if (nested != null) { return nested; }
            }
            return null;
        }
    }
}
