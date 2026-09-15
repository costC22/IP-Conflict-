using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace IPConflictMonitor.Launcher
{
    internal static partial class Program
    {
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr windowHandle, int command);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr windowHandle, int message, IntPtr parameter, string text);

        private static void HideConsoleWindow()
        {
            IntPtr window = GetConsoleWindow();
            if (window != IntPtr.Zero) { ShowWindow(window, 0); }
        }

        private static int RunGraphicalInterface()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new NetworkOperationsForm(false));
            return 0;
        }

        private static int CaptureGraphicalInterface(string outputPath)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (String.IsNullOrWhiteSpace(outputPath)) { outputPath = Path.Combine(Path.GetTempPath(), "IPConflictMonitor-dashboard.png"); }
            outputPath = Path.GetFullPath(outputPath);
            string directory = Path.GetDirectoryName(outputPath);
            if (!String.IsNullOrWhiteSpace(directory)) { Directory.CreateDirectory(directory); }
            using (var form = new NetworkOperationsForm(true))
            {
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(20, 20);
                form.Show();
                Application.DoEvents();
                UpdateCoordinator.AttachToForm(form);
                form.PreparePreview();
                Application.DoEvents();
                using (var bitmap = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                    bitmap.Save(outputPath, ImageFormat.Png);
                }
                form.Close();
            }
            return 0;
        }

        private sealed class NetworkRow
        {
            public string Timestamp;
            public string IP;
            public string Hostname;
            public string Status;
            public string MACs;
            public string MACDetails;
            public string MacCount;
            public string Observations;
            public string Transitions;
            public string DirectArpMacCount;
            public string ActiveProbeMacCount;
            public string MappingMismatch;
            public string FirstSeen;
            public string LastSeen;
            public string Reason;
            public string Interface;
            public string MonitorIp;
            public string RequestObserved;
            public string PositiveRounds;
            public string RequiredRounds;
            public string ConfirmedCycles;
            public string RequiredCycles;
            public string CorrelatedArpReplies;
            public string ProxyArpRisk;
            public string GatewayMac;
            public string TrustedPair;
            public string CaptureHealthy;
            public string ConfidenceScore;
            public string EvidenceId;
            public string EvidenceHash;
            public string EvidenceQuality;
        }

        private sealed class FeedItem
        {
            public string Time;
            public string Text;
            public Color Color;
        }

        private sealed class HeroPanel : Panel
        {
            public Color AccentColor { get; set; }

            public HeroPanel()
            {
                AccentColor = FieldTheme.Cyan;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            }

            protected override void OnPaintBackground(PaintEventArgs eventArgs)
            {
                Rectangle bounds = ClientRectangle;
                if (bounds.Width <= 0 || bounds.Height <= 0) { return; }
                using (var gradient = new LinearGradientBrush(bounds, Color.FromArgb(14, 29, 51), Color.FromArgb(8, 19, 36), LinearGradientMode.Horizontal))
                {
                    eventArgs.Graphics.FillRectangle(gradient, bounds);
                }

                int right = Math.Max(180, bounds.Width - 28);
                int mid = Math.Max(120, bounds.Width - 318);
                using (var trace = new Pen(Color.FromArgb(49, 83, 114), 1F))
                using (var traceStrong = new Pen(Color.FromArgb(112, AccentColor), 1F))
                using (var node = new SolidBrush(Color.FromArgb(170, AccentColor)))
                {
                    eventArgs.Graphics.DrawLine(trace, mid, 26, right - 92, 26);
                    eventArgs.Graphics.DrawLine(trace, right - 92, 26, right - 92, 56);
                    eventArgs.Graphics.DrawLine(trace, right - 92, 56, right, 56);
                    eventArgs.Graphics.DrawLine(traceStrong, mid + 62, 78, right - 168, 78);
                    eventArgs.Graphics.DrawLine(traceStrong, right - 168, 78, right - 168, 48);
                    eventArgs.Graphics.FillRectangle(node, mid - 3, 23, 7, 7);
                    eventArgs.Graphics.FillRectangle(node, right - 95, 53, 7, 7);
                    eventArgs.Graphics.FillRectangle(node, right - 3, 53, 7, 7);
                    eventArgs.Graphics.FillRectangle(node, mid + 59, 75, 7, 7);
                    eventArgs.Graphics.FillRectangle(node, right - 171, 45, 7, 7);
                }
                using (var accent = new SolidBrush(AccentColor))
                {
                    eventArgs.Graphics.FillRectangle(accent, 0, 0, Math.Min(238, bounds.Width), 3);
                }
            }
        }

        private sealed class MetricPanel : Panel
        {
            public Color AccentColor { get; set; }
            public string MetricTitle { get; set; }
            public string MetricValue { get; set; }
            public string Symbol { get; set; }

            public MetricPanel()
            {
                DoubleBuffered = true;
                AccentColor = FieldTheme.Cyan;
                MetricTitle = String.Empty;
                MetricValue = "0";
                Symbol = "SYS";
                Padding = new Padding(0);
            }

            protected override void OnPaint(PaintEventArgs eventArgs)
            {
                Rectangle bounds = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
                using (var fill = new LinearGradientBrush(bounds, FieldTheme.SurfaceRaised, Color.FromArgb(11, 23, 40), LinearGradientMode.Vertical))
                using (var border = new Pen(FieldTheme.Stroke))
                using (var accent = new SolidBrush(AccentColor))
                using (var moduleFill = new SolidBrush(Color.FromArgb(10, 22, 38)))
                using (var moduleBorder = new Pen(Color.FromArgb(88, AccentColor)))
                using (var valueFont = new Font("Bahnschrift SemiCondensed", 24F, FontStyle.Bold))
                using (var titleFont = new Font("Segoe UI Semibold", 7.4F))
                using (var symbolFont = new Font("Consolas", 8.2F, FontStyle.Bold))
                {
                    eventArgs.Graphics.FillRectangle(fill, bounds);
                    eventArgs.Graphics.DrawRectangle(border, bounds);
                    eventArgs.Graphics.FillRectangle(accent, 0, 0, 4, Height);
                    eventArgs.Graphics.FillRectangle(accent, 4, 0, Math.Min(68, Math.Max(0, Width - 4)), 3);
                    Rectangle module = new Rectangle(Math.Max(8, Width - 76), 18, 52, 43);
                    eventArgs.Graphics.FillRectangle(moduleFill, module);
                    eventArgs.Graphics.DrawRectangle(moduleBorder, module);
                    TextRenderer.DrawText(eventArgs.Graphics, Symbol, symbolFont, module, AccentColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    TextRenderer.DrawText(eventArgs.Graphics, MetricValue, valueFont, new Rectangle(18, 13, Math.Max(20, Width - 100), 48), FieldTheme.MainText, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                    TextRenderer.DrawText(eventArgs.Graphics, MetricTitle, titleFont, new Rectangle(18, 64, Math.Max(20, Width - 36), 25), FieldTheme.SoftText, TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis);
                }
            }
        }

        private sealed class EventFeed : Control
        {
            private readonly List<FeedItem> _items = new List<FeedItem>();

            public EventFeed()
            {
                DoubleBuffered = true;
                BackColor = Color.FromArgb(8, 18, 33);
            }

            public void SetItems(IEnumerable<FeedItem> items)
            {
                _items.Clear();
                _items.AddRange(items.TakeLastCompat(7));
                Invalidate();
            }

            protected override void OnPaint(PaintEventArgs eventArgs)
            {
                eventArgs.Graphics.Clear(BackColor);
                using (var rail = new Pen(Color.FromArgb(42, 67, 91), 1F))
                using (var timeFont = new Font("Consolas", 7.5F))
                using (var textFont = new Font("Segoe UI", 8F))
                {
                    eventArgs.Graphics.DrawLine(rail, 14, 4, 14, Math.Max(4, Height - 6));
                    int y = 8;
                    foreach (FeedItem item in _items)
                    {
                        using (var marker = new SolidBrush(item.Color))
                        {
                            eventArgs.Graphics.FillRectangle(marker, 11, y + 7, 7, 7);
                        }
                        TextRenderer.DrawText(eventArgs.Graphics, item.Time, timeFont, new Rectangle(28, y, 58, 20), FieldTheme.MutedText, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                        TextRenderer.DrawText(eventArgs.Graphics, item.Text, textFont, new Rectangle(88, y, Math.Max(0, Width - 98), 20), item.Color, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                        y += 22;
                        if (y > Height - 19) { break; }
                    }
                    if (_items.Count == 0)
                    {
                        TextRenderer.DrawText(eventArgs.Graphics, "A atividade aparecerá após a primeira análise.", textFont, ClientRectangle, FieldTheme.MutedText, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                    }
                }
            }
        }

        private sealed class ScanBar : Control
        {
            public bool Active { get; set; }
            public int Phase { get; set; }
            public ScanBar() { DoubleBuffered = true; Height = 3; }
            protected override void OnPaint(PaintEventArgs eventArgs)
            {
                eventArgs.Graphics.Clear(Color.FromArgb(24, 42, 62));
                if (!Active) { return; }
                int segment = Math.Max(80, Width / 5);
                int x = (Phase % Math.Max(1, Width + segment)) - segment;
                Rectangle rectangle = new Rectangle(x, 0, segment, Height);
                using (var gradient = new LinearGradientBrush(rectangle, Color.Transparent, Color.FromArgb(50, 223, 241), LinearGradientMode.Horizontal)) { eventArgs.Graphics.FillRectangle(gradient, rectangle); }
            }
        }

        internal sealed class NetworkOperationsForm : Form
        {
            private static readonly Color Canvas = FieldTheme.Canvas;
            private static readonly Color SidebarColor = FieldTheme.Sidebar;
            private static readonly Color Surface = FieldTheme.Surface;
            private static readonly Color SurfaceRaised = FieldTheme.SurfaceRaised;
            private static readonly Color Stroke = FieldTheme.Stroke;
            private static readonly Color MainText = FieldTheme.MainText;
            private static readonly Color SoftText = FieldTheme.SoftText;
            private static readonly Color Cyan = FieldTheme.Cyan;
            private static readonly Color Blue = FieldTheme.Blue;
            private static readonly Color Purple = FieldTheme.Purple;
            private static readonly Color Green = FieldTheme.Green;
            private static readonly Color Amber = FieldTheme.Amber;
            private static readonly Color Red = FieldTheme.Red;

            private readonly bool _demoMode;
            private readonly List<NetworkRow> _rows = new List<NetworkRow>();
            private readonly DataGridView _grid;
            private readonly TextBox _search;
            private readonly Label _details;
            private readonly EventFeed _feed;
            private readonly Label _engineState;
            private readonly Label _engineHint;
            private readonly Label _networkLabel;
            private readonly Label _updatedLabel;
            private readonly Label _footer;
            private readonly FieldActionButton _scanButton;
            private readonly FieldActionButton _continuousButton;
            private readonly FieldActionButton _stopButton;
            private readonly ScanBar _scanBar;
            private readonly MetricPanel _totalMetric;
            private readonly MetricPanel _normalMetric;
            private readonly MetricPanel _attentionMetric;
            private readonly MetricPanel _conflictMetric;
            private readonly Timer _timer;
            private readonly TableLayoutPanel _shell;
            private readonly TableLayoutPanel _dashboardContent;
            private FieldActionButton _overviewNavigation;
            private FieldActionButton _reportsNavigation;
            private FieldActionButton _settingsNavigation;
            private FieldActionButton _helpNavigation;
            private Control _activeWorkspacePage;
            private SettingsExperiencePage _settingsPage;
            private bool _externalWorkspaceLocked;
            private const string StopEventName = "Local\\IPConflictMonitor.StrictEvidence.Stop";
            private Process _worker;
            private bool _continuous;
            private DateTime _lastSnapshot = DateTime.MinValue;
            private int _animationPhase;

            public NetworkOperationsForm(bool demoMode)
            {
                _demoMode = demoMode;
                Text = "IPConflictMonitor 3.4.0 — Strict Evidence";
                Icon = SystemIcons.Shield;
                BackColor = Canvas;
                ForeColor = MainText;
                Font = new Font("Segoe UI", 9F);
                AutoScaleMode = AutoScaleMode.Dpi;
                MinimumSize = new Size(1160, 740);
                Size = new Size(1440, 900);
                StartPosition = FormStartPosition.CenterScreen;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);

                _shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Canvas, Padding = new Padding(0), Margin = new Padding(0) };
                _shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 226));
                _shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                Controls.Add(_shell);
                _shell.Controls.Add(BuildSidebar(), 0, 0);

                _dashboardContent = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7, BackColor = Canvas, Padding = new Padding(20, 0, 20, 0), Margin = new Padding(0) };
                _dashboardContent.RowStyles.Add(new RowStyle(SizeType.Absolute, 108));
                _dashboardContent.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
                _dashboardContent.RowStyles.Add(new RowStyle(SizeType.Absolute, 116));
                _dashboardContent.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
                _dashboardContent.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                _dashboardContent.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
                _dashboardContent.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
                _shell.Controls.Add(_dashboardContent, 1, 0);
                _activeWorkspacePage = _dashboardContent;

                _dashboardContent.Controls.Add(BuildHero(out _scanButton, out _continuousButton), 0, 0);
                _dashboardContent.Controls.Add(BuildEngineCard(out _engineState, out _engineHint, out _networkLabel, out _updatedLabel, out _stopButton, out _scanBar), 0, 1);
                _dashboardContent.Controls.Add(BuildMetrics(out _totalMetric, out _normalMetric, out _attentionMetric, out _conflictMetric), 0, 2);
                _dashboardContent.Controls.Add(BuildSearch(out _search), 0, 3);
                _grid = BuildGrid();
                _dashboardContent.Controls.Add(_grid, 0, 4);
                _dashboardContent.Controls.Add(BuildLowerDeck(out _details, out _feed), 0, 5);
                _footer = new Label { Dock = DockStyle.Fill, Text = "IPCONFLICTMONITOR 3.4.0  /  PERFIL LOCAL  /  DIAGNÓSTICO CONSERVADOR", ForeColor = FieldTheme.MutedText, Font = new Font("Consolas", 7.2F), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(2, 0, 0, 0) };
                _dashboardContent.Controls.Add(_footer, 0, 6);

                _search.HandleCreated += delegate { SendMessage(_search.Handle, 0x1501, new IntPtr(1), "Buscar por IP, nome do dispositivo, MAC ou diagnóstico..."); };
                _search.TextChanged += delegate { ApplyFilter(); };
                _grid.SelectionChanged += delegate { ShowDetails(); };
                _scanButton.Click += delegate { StartWorker(true); };
                _continuousButton.Click += delegate { StartWorker(false); };
                _stopButton.Click += delegate { StopWorker(true); };
                Shown += delegate { if (_demoMode) { LoadDemo(); } else { RefreshData(true); } };
                FormClosing += HandleFormClosing;
                FormClosed += delegate { StopWorker(false); };
                _timer = new Timer { Interval = 90 };
                _timer.Tick += delegate { TickInterface(); };
                if (!_demoMode) { _timer.Start(); }
            }

            public void PreparePreview()
            {
                LoadDemo();
                PerformLayout();
                Refresh();
            }

            public void PrepareSettingsPreview()
            {
                string previewPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config", "config.json");
                if (!File.Exists(previewPath)) { previewPath = ConfigurationStore.ResolveActivePath(); }
                if (_settingsPage != null) { _settingsPage.Dispose(); }
                _settingsPage = new SettingsExperiencePage(false, previewPath);
                _settingsPage.BackRequested += RestoreDashboard;
                ShowWorkspacePage(_settingsPage, _settingsNavigation);
                PerformLayout();
                Refresh();
            }

            private Control BuildSidebar()
            {
                var panel = new Panel { Dock = DockStyle.Fill, BackColor = SidebarColor, Padding = new Padding(17, 0, 17, 18), Margin = new Padding(0) };
                panel.Paint += delegate(object sender, PaintEventArgs eventArgs)
                {
                    using (var separator = new Pen(Color.FromArgb(28, 48, 69))) { eventArgs.Graphics.DrawLine(separator, panel.Width - 1, 0, panel.Width - 1, panel.Height); }
                    using (var accent = new LinearGradientBrush(new Rectangle(0, 0, panel.Width, 3), Cyan, Blue, LinearGradientMode.Horizontal)) { eventArgs.Graphics.FillRectangle(accent, 0, 0, panel.Width, 3); }
                };
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 9, BackColor = SidebarColor, Padding = new Padding(0), Margin = new Padding(0) };
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 49));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 49));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 49));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 49));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 132));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));

                var brand = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, BackColor = SidebarColor, Padding = new Padding(0, 26, 0, 20), Margin = new Padding(0) };
                brand.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));
                brand.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                brand.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
                brand.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
                var logo = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(11, 33, 51), Margin = new Padding(0, 0, 9, 0) };
                logo.Paint += delegate(object sender, PaintEventArgs eventArgs)
                {
                    using (var border = new Pen(Color.FromArgb(108, Cyan), 1F))
                    using (var trace = new Pen(Cyan, 2F))
                    {
                        eventArgs.Graphics.DrawRectangle(border, 0, 0, Math.Max(0, logo.Width - 1), Math.Max(0, logo.Height - 1));
                        eventArgs.Graphics.DrawLine(trace, 9, 31, 18, 22);
                        eventArgs.Graphics.DrawLine(trace, 18, 22, 27, 30);
                        eventArgs.Graphics.DrawLine(trace, 27, 30, 37, 18);
                    }
                };
                brand.Controls.Add(logo, 0, 0); brand.SetRowSpan(logo, 2);
                brand.Controls.Add(new Label { Text = "IP CONFLICT", Dock = DockStyle.Fill, ForeColor = MainText, Font = new Font("Bahnschrift SemiCondensed", 13F, FontStyle.Bold), TextAlign = ContentAlignment.BottomLeft }, 1, 0);
                brand.Controls.Add(new Label { Text = "MONITOR DE REDE", Dock = DockStyle.Fill, ForeColor = Cyan, Font = new Font("Segoe UI Semibold", 7.1F), TextAlign = ContentAlignment.TopLeft }, 1, 1);
                layout.Controls.Add(brand, 0, 0);
                layout.Controls.Add(new Label { Text = "PAINEL OPERACIONAL", Dock = DockStyle.Fill, ForeColor = FieldTheme.MutedText, Font = new Font("Segoe UI Semibold", 7F), TextAlign = ContentAlignment.MiddleLeft }, 0, 1);

                _overviewNavigation = CreateNav("[OV]  VISÃO GERAL", true, delegate { RestoreDashboard(); if (_activeWorkspacePage == _dashboardContent) { _search.Clear(); } });
                _reportsNavigation = CreateNav("[RP]  RELATÓRIOS", false, delegate { if (CanLeaveWorkspace()) { ShowWorkspacePage(_dashboardContent, _overviewNavigation); OpenReports(); } });
                _settingsNavigation = CreateNav("[CF]  CONFIGURAÇÃO", false, delegate { OpenConfiguration(); });
                _helpNavigation = CreateNav("[?]   GUIA OPERACIONAL", false, delegate { ShowHelp(); });
                layout.Controls.Add(_overviewNavigation, 0, 2);
                layout.Controls.Add(_reportsNavigation, 0, 3);
                layout.Controls.Add(_settingsNavigation, 0, 4);
                layout.Controls.Add(_helpNavigation, 0, 5);

                var profile = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(10, 24, 41), Margin = new Padding(0, 5, 0, 7), Padding = new Padding(12, 10, 12, 8) };
                profile.Paint += PaintCardBorder;
                var profileLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5, ColumnCount = 1, BackColor = profile.BackColor, Margin = new Padding(0) };
                profileLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
                profileLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
                profileLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
                profileLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22));
                profileLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                profileLayout.Controls.Add(new Label { Text = "PERFIL OPERACIONAL", Dock = DockStyle.Fill, ForeColor = Cyan, Font = new Font("Segoe UI Semibold", 7.3F), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
                profileLayout.Controls.Add(new Label { Text = "DETECÇÃO  STRICT EVIDENCE", Dock = DockStyle.Fill, ForeColor = MainText, Font = new Font("Consolas", 7.2F), TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
                profileLayout.Controls.Add(new Label { Text = "CAPTURA   L2 + ARP ATIVO", Dock = DockStyle.Fill, ForeColor = MainText, Font = new Font("Consolas", 7.2F), TextAlign = ContentAlignment.MiddleLeft }, 0, 2);
                profileLayout.Controls.Add(new Label { Text = "POLÍTICA  FAIL-CLOSED", Dock = DockStyle.Fill, ForeColor = MainText, Font = new Font("Consolas", 7.2F), TextAlign = ContentAlignment.MiddleLeft }, 0, 3);
                profile.Controls.Add(profileLayout);
                layout.Controls.Add(profile, 0, 7);

                var safety = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(9, 33, 39), Margin = new Padding(0), Padding = new Padding(12, 8, 10, 7) };
                safety.Paint += PaintCardBorder;
                safety.Controls.Add(new Label { Text = "ALERTA SOMENTE COM PROVA\nARP CORRELACIONADA E REPETIDA", Dock = DockStyle.Fill, ForeColor = Green, Font = new Font("Segoe UI Semibold", 7.3F), TextAlign = ContentAlignment.MiddleLeft });
                layout.Controls.Add(safety, 0, 8);
                panel.Controls.Add(layout);
                return panel;
            }

            private FieldActionButton CreateNav(string text, bool selected, EventHandler action)
            {
                Color fill = selected ? Color.FromArgb(17, 57, 73) : SidebarColor;
                var button = new FieldActionButton
                {
                    Text = text, Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 4), StartColor = fill, EndColor = selected ? Color.FromArgb(14, 43, 64) : SidebarColor,
                    HoverStartColor = Color.FromArgb(21, 54, 73), HoverEndColor = Color.FromArgb(18, 42, 62), BorderColor = selected ? Color.FromArgb(45, 117, 134) : Color.Transparent,
                    ForeColor = selected ? Cyan : SoftText, Font = new Font("Segoe UI Semibold", 8F), Radius = 3, CaptionAlignment = ContentAlignment.MiddleLeft
                };
                button.Click += action;
                return button;
            }

            private Control BuildHero(out FieldActionButton scan, out FieldActionButton continuous)
            {
                var hero = new HeroPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 12, 0, 10), Padding = new Padding(18, 0, 16, 0) };
                hero.Paint += PaintCardBorder;
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Color.Transparent, Margin = new Padding(0) };
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 184));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
                var titleBox = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, BackColor = Color.Transparent, Margin = new Padding(0), Padding = new Padding(0, 8, 0, 7) };
                titleBox.RowStyles.Add(new RowStyle(SizeType.Absolute, 21));
                titleBox.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
                titleBox.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
                titleBox.Controls.Add(new Label { Text = "DIAGNÓSTICO DE REDE / PERFIL DE CAMPO 3.4.0", Dock = DockStyle.Fill, ForeColor = Cyan, Font = new Font("Segoe UI Semibold", 7.2F), TextAlign = ContentAlignment.BottomLeft, BackColor = Color.Transparent }, 0, 0);
                titleBox.Controls.Add(new Label { Text = "Conflitos IPv4", Dock = DockStyle.Fill, ForeColor = MainText, Font = new Font("Bahnschrift SemiCondensed", 22F, FontStyle.Bold), TextAlign = ContentAlignment.BottomLeft, BackColor = Color.Transparent }, 0, 1);
                titleBox.Controls.Add(new Label { Text = "Mapeie endereços e confirme duplicidade somente com evidência ARP atual.", Dock = DockStyle.Fill, ForeColor = SoftText, Font = new Font("Segoe UI", 8.7F), TextAlign = ContentAlignment.TopLeft, BackColor = Color.Transparent }, 0, 2);
                layout.Controls.Add(titleBox, 0, 0);
                continuous = new FieldActionButton { Text = "MONITORAR CONTÍNUO", Dock = DockStyle.Fill, Margin = new Padding(8, 22, 8, 22), StartColor = Color.FromArgb(23, 39, 63), EndColor = Color.FromArgb(17, 31, 52), HoverStartColor = Color.FromArgb(29, 51, 78), HoverEndColor = Color.FromArgb(22, 41, 64), BorderColor = Color.FromArgb(50, 77, 105), ForeColor = MainText, Font = new Font("Segoe UI Semibold", 8.1F), Radius = 4 };
                layout.Controls.Add(continuous, 1, 0);
                scan = new FieldActionButton { Text = "ANALISAR REDE", Dock = DockStyle.Fill, Margin = new Padding(8, 22, 0, 22), ForeColor = Color.FromArgb(4, 25, 38), Font = new Font("Segoe UI Semibold", 8.5F), Radius = 4 };
                layout.Controls.Add(scan, 2, 0);
                hero.Controls.Add(layout);
                return hero;
            }

            private Control BuildEngineCard(out Label state, out Label hint, out Label network, out Label updated, out FieldActionButton stop, out ScanBar scanBar)
            {
                var card = new Panel { Dock = DockStyle.Fill, BackColor = Surface, Margin = new Padding(0, 0, 0, 10), Padding = new Padding(16, 8, 12, 6) };
                card.Paint += PaintCardBorder;
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 2, BackColor = Surface, Margin = new Padding(0) };
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 250));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 170));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 185));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 3));
                var stateBox = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = Surface, Margin = new Padding(0), Padding = new Padding(0, 2, 0, 2) };
                state = new Label { Text = "[READY]  MECANISMO DISPONÍVEL", Dock = DockStyle.Fill, ForeColor = Green, Font = new Font("Segoe UI Semibold", 8.5F), TextAlign = ContentAlignment.BottomLeft };
                hint = new Label { Text = "Aguardando uma análise", Dock = DockStyle.Fill, ForeColor = SoftText, Font = new Font("Segoe UI", 8F), TextAlign = ContentAlignment.TopLeft };
                stateBox.Controls.Add(state, 0, 0); stateBox.Controls.Add(hint, 0, 1);
                layout.Controls.Add(stateBox, 0, 0);
                network = new Label { Text = "Interface e CIDR serão detectados automaticamente", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(162, 184, 209), Font = new Font("Consolas", 8.1F), TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, Padding = new Padding(10, 0, 0, 0) };
                layout.Controls.Add(network, 1, 0);
                layout.Controls.Add(CreateEngineBadge("ARP / STRICT", Cyan), 2, 0);
                updated = new Label { Text = "Nenhum diagnóstico", Dock = DockStyle.Fill, ForeColor = SoftText, Font = new Font("Segoe UI", 8F), TextAlign = ContentAlignment.MiddleRight };
                layout.Controls.Add(updated, 3, 0);
                stop = new FieldActionButton { Text = "[STOP]  PARAR", Dock = DockStyle.Fill, Margin = new Padding(12, 7, 0, 7), StartColor = Color.FromArgb(52, 28, 42), EndColor = Color.FromArgb(40, 25, 38), HoverStartColor = Color.FromArgb(75, 33, 49), HoverEndColor = Color.FromArgb(55, 28, 42), BorderColor = Color.FromArgb(94, 45, 60), ForeColor = Red, Font = new Font("Segoe UI Semibold", 7.7F), Radius = 4, Enabled = false };
                layout.Controls.Add(stop, 4, 0);
                scanBar = new ScanBar { Dock = DockStyle.Fill, Margin = new Padding(0) };
                layout.Controls.Add(scanBar, 0, 1); layout.SetColumnSpan(scanBar, 5);
                card.Controls.Add(layout);
                return card;
            }

            private Control CreateEngineBadge(string text, Color color)
            {
                var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(11, 29, 43), Margin = new Padding(13, 10, 13, 10) };
                panel.Paint += delegate(object sender, PaintEventArgs eventArgs) { using (var pen = new Pen(Color.FromArgb(44, color))) { eventArgs.Graphics.DrawRectangle(pen, 0, 0, panel.Width - 1, panel.Height - 1); } };
                panel.Controls.Add(new Label { Text = text, Dock = DockStyle.Fill, ForeColor = color, Font = new Font("Segoe UI Semibold", 7.3F), TextAlign = ContentAlignment.MiddleCenter });
                return panel;
            }

            private Control BuildMetrics(out MetricPanel total, out MetricPanel normal, out MetricPanel attention, out MetricPanel conflicts)
            {
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, BackColor = Canvas, Margin = new Padding(0), Padding = new Padding(0, 2, 0, 14) };
                for (int index = 0; index < 4; index++) { layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25)); }
                total = CreateMetric("ENDEREÇOS OBSERVADOS", "ALL", Blue, new Padding(0, 0, 7, 0));
                normal = CreateMetric("ASSOCIAÇÕES NORMAIS", "OK", Green, new Padding(3, 0, 4, 0));
                attention = CreateMetric("SEM CONFIRMAÇÃO", "CHK", Amber, new Padding(4, 0, 3, 0));
                conflicts = CreateMetric("CONFLITOS CONFIRMADOS", "DUP", Red, new Padding(7, 0, 0, 0));
                layout.Controls.Add(total, 0, 0); layout.Controls.Add(normal, 1, 0); layout.Controls.Add(attention, 2, 0); layout.Controls.Add(conflicts, 3, 0);
                return layout;
            }

            private MetricPanel CreateMetric(string title, string symbol, Color color, Padding margin)
            {
                return new MetricPanel { Dock = DockStyle.Fill, Margin = margin, MetricTitle = title, Symbol = symbol, AccentColor = color };
            }

            private Control BuildSearch(out TextBox search)
            {
                var card = new Panel { Dock = DockStyle.Fill, BackColor = Surface, Margin = new Padding(0, 0, 0, 8), Padding = new Padding(15, 9, 12, 8) };
                card.Paint += PaintCardBorder;
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, BackColor = Surface, Margin = new Padding(0) };
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 28));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 174));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94));
                layout.Controls.Add(new Label { Text = "[Q]", Dock = DockStyle.Fill, ForeColor = Cyan, Font = new Font("Segoe UI Symbol", 14F), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
                search = new TextBox { Dock = DockStyle.Fill, BackColor = Surface, ForeColor = MainText, BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 9.3F), Margin = new Padding(0, 4, 10, 0) };
                layout.Controls.Add(search, 1, 0);
                layout.Controls.Add(new Label { Text = "IP  /  HOST  /  MAC  /  STATUS", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(88, 118, 148), Font = new Font("Segoe UI Semibold", 7.1F), TextAlign = ContentAlignment.MiddleRight }, 2, 0);
                var clear = new FieldActionButton { Text = "LIMPAR", Dock = DockStyle.Fill, Margin = new Padding(12, 0, 0, 0), StartColor = SurfaceRaised, EndColor = Color.FromArgb(15, 29, 49), HoverStartColor = Color.FromArgb(29, 48, 72), HoverEndColor = Color.FromArgb(22, 39, 61), BorderColor = Stroke, ForeColor = SoftText, Font = new Font("Segoe UI Semibold", 7.2F), Radius = 4 };
                clear.Click += delegate { _search.Clear(); };
                layout.Controls.Add(clear, 3, 0);
                card.Controls.Add(layout);
                return card;
            }

            private DataGridView BuildGrid()
            {
                var grid = new DataGridView
                {
                    Dock = DockStyle.Fill, BackgroundColor = Color.FromArgb(9, 18, 32), BorderStyle = BorderStyle.None, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
                    AllowUserToResizeRows = false, ReadOnly = true, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, RowHeadersVisible = false,
                    AutoGenerateColumns = false, EnableHeadersVisualStyles = false, GridColor = Color.FromArgb(28, 44, 63), ColumnHeadersHeight = 40, RowTemplate = { Height = 36 }, Margin = new Padding(0)
                };
                typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(grid, true, null);
                grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(17, 31, 51);
                grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(120, 148, 180);
                grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI Semibold", 7.2F);
                grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(7, 0, 0, 0);
                grid.DefaultCellStyle.BackColor = Color.FromArgb(12, 23, 40);
                grid.DefaultCellStyle.ForeColor = Color.FromArgb(207, 220, 235);
                grid.DefaultCellStyle.Font = new Font("Segoe UI", 8.5F);
                grid.DefaultCellStyle.Padding = new Padding(7, 0, 0, 0);
                grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(24, 59, 78);
                grid.DefaultCellStyle.SelectionForeColor = MainText;
                grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(9, 20, 36);
                grid.Columns.Add(Column("Status", "DIAGNÓSTICO", 122));
                grid.Columns.Add(Column("IP", "ENDEREÇO IPv4", 130));
                grid.Columns.Add(Column("Hostname", "DISPOSITIVO / HOST", 188));
                grid.Columns.Add(Column("MACs", "IDENTIDADE(S) MAC", 236));
                grid.Columns.Add(Column("Observations", "EVIDÊNCIAS", 92));
                grid.Columns.Add(Column("LastSeen", "ÚLTIMA VISÃO", 136));
                DataGridViewTextBoxColumn result = Column("Reason", "RESULTADO DA ANÁLISE", 280); result.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill; grid.Columns.Add(result);
                grid.CellPainting += PaintStatus;
                grid.RowPrePaint += PaintRowAccent;
                return grid;
            }

            private DataGridViewTextBoxColumn Column(string name, string title, int width)
            {
                return new DataGridViewTextBoxColumn { Name = name, HeaderText = title, Width = width, SortMode = DataGridViewColumnSortMode.Automatic };
            }

            private Control BuildLowerDeck(out Label details, out EventFeed feed)
            {
                var deck = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Canvas, Padding = new Padding(0, 10, 0, 0), Margin = new Padding(0) };
                deck.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
                deck.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
                var detailCard = new Panel { Dock = DockStyle.Fill, BackColor = Surface, Margin = new Padding(0, 0, 5, 0), Padding = new Padding(13, 9, 13, 11) };
                detailCard.Paint += PaintCardBorder;
                var detailLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = Surface, Margin = new Padding(0) };
                detailLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));
                detailLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                detailLayout.Controls.Add(SectionTitle("EVIDÊNCIAS DO ENDEREÇO SELECIONADO", "CORRELACIONAMENTO IP ↔ MAC"), 0, 0);
                details = new Label { Text = "Selecione um endereço na tabela para abrir o laudo técnico.", Dock = DockStyle.Fill, BackColor = Color.FromArgb(8, 18, 33), ForeColor = Color.FromArgb(187, 205, 225), Font = new Font("Consolas", 8.25F), TextAlign = ContentAlignment.TopLeft, Padding = new Padding(12, 9, 12, 8), AutoEllipsis = true };
                detailLayout.Controls.Add(details, 0, 1); detailCard.Controls.Add(detailLayout); deck.Controls.Add(detailCard, 0, 0);

                var feedCard = new Panel { Dock = DockStyle.Fill, BackColor = Surface, Margin = new Padding(5, 0, 0, 0), Padding = new Padding(13, 9, 13, 11) };
                feedCard.Paint += PaintCardBorder;
                var feedLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1, BackColor = Surface, Margin = new Padding(0) };
                feedLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 29)); feedLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                feedLayout.Controls.Add(SectionTitle("TELEMETRIA EM TEMPO REAL", "ÚLTIMOS EVENTOS"), 0, 0);
                feed = new EventFeed { Dock = DockStyle.Fill }; feedLayout.Controls.Add(feed, 0, 1); feedCard.Controls.Add(feedLayout); deck.Controls.Add(feedCard, 1, 0);
                return deck;
            }

            private Control SectionTitle(string title, string badge)
            {
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Surface, Margin = new Padding(0) };
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 165));
                layout.Controls.Add(new Label { Text = title, Dock = DockStyle.Fill, ForeColor = Color.FromArgb(148, 174, 202), Font = new Font("Segoe UI Semibold", 7.2F), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
                layout.Controls.Add(new Label { Text = badge, Dock = DockStyle.Fill, ForeColor = Color.FromArgb(74, 109, 142), Font = new Font("Segoe UI Semibold", 6.8F), TextAlign = ContentAlignment.MiddleRight }, 1, 0);
                return layout;
            }

            private void PaintCardBorder(object sender, PaintEventArgs eventArgs)
            {
                Control control = (Control)sender;
                using (var pen = new Pen(Stroke)) { eventArgs.Graphics.DrawRectangle(pen, 0, 0, Math.Max(0, control.Width - 1), Math.Max(0, control.Height - 1)); }
            }

            private void PaintStatus(object sender, DataGridViewCellPaintingEventArgs eventArgs)
            {
                if (eventArgs.RowIndex < 0 || eventArgs.ColumnIndex != 0) { return; }
                eventArgs.PaintBackground(eventArgs.CellBounds, true);
                string raw = Convert.ToString(eventArgs.FormattedValue);
                Color color = raw == "CONFIRMED" ? Red : raw == "MONITORING_LIMITED" ? Amber : raw == "UNVERIFIED" ? Blue : Green;
                string text = raw == "CONFIRMED" ? "CONFLITO" : raw == "MONITORING_LIMITED" ? "LIMITADO" : raw == "UNVERIFIED" ? "NÃO VERIFIC." : "NORMAL";
                Rectangle badge = new Rectangle(eventArgs.CellBounds.X + 13, eventArgs.CellBounds.Y + 8, eventArgs.CellBounds.Width - 26, eventArgs.CellBounds.Height - 16);
                using (var fill = new SolidBrush(Color.FromArgb(38, color)))
                using (var border = new Pen(Color.FromArgb(82, color)))
                using (var font = new Font("Segoe UI Semibold", 7.1F))
                {
                    eventArgs.Graphics.FillRectangle(fill, badge);
                    eventArgs.Graphics.DrawRectangle(border, badge);
                    TextRenderer.DrawText(eventArgs.Graphics, text, font, badge, color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                eventArgs.Handled = true;
            }

            private void PaintRowAccent(object sender, DataGridViewRowPrePaintEventArgs eventArgs)
            {
                object value = _grid.Rows[eventArgs.RowIndex].Cells[0].Value;
                string status = Convert.ToString(value);
                Color color = status == "CONFIRMED" ? Red : status == "MONITORING_LIMITED" ? Amber : status == "UNVERIFIED" ? Blue : Color.Transparent;
                if (color.A == 0) { return; }
                using (var brush = new SolidBrush(color)) { eventArgs.Graphics.FillRectangle(brush, eventArgs.RowBounds.X, eventArgs.RowBounds.Y, 3, eventArgs.RowBounds.Height); }
            }

            private void LoadDemo()
            {
                _rows.Clear();
                _rows.Add(new NetworkRow { IP = "192.168.15.35", Hostname = "IMPRESSORA-RECEPCAO", Status = "CONFIRMED", MACs = "00:1A:2B:3C:4D:5E, 70:8A:09:11:22:32", MACDetails = "Dois MACs com prova estrita", MacCount = "2", Observations = "8", DirectArpMacCount = "2", ActiveProbeMacCount = "2", FirstSeen = "2026-08-22T10:02:11", LastSeen = "2026-08-22T10:04:29", Interface = "Ethernet (#12)", MonitorIp = "192.168.15.3", RequestObserved = "True", PositiveRounds = "2", RequiredRounds = "2", ConfirmedCycles = "2", RequiredCycles = "2", CorrelatedArpReplies = "6", CaptureHealthy = "True", EvidenceId = "EVD-20260822-00152", EvidenceQuality = "STRICT_PROOF", Reason = "Conflito confirmado após respostas ARP correlacionadas e repetidas de dois MACs distintos para o mesmo IPv4." });
                _rows.Add(new NetworkRow { IP = "192.168.15.21", Hostname = "NOTEBOOK-CAMPO", Status = "UNVERIFIED", MACs = "34:AA:90:12:CC:44, 98:76:54:32:10:FC", MACDetails = "Mudança histórica sem prova atual", MacCount = "2", Observations = "4", DirectArpMacCount = "1", ActiveProbeMacCount = "1", FirstSeen = "2026-08-22T10:01:03", LastSeen = "2026-08-22T10:04:22", Interface = "Ethernet (#12)", MonitorIp = "192.168.15.3", RequestObserved = "True", PositiveRounds = "0", RequiredRounds = "2", ConfirmedCycles = "0", RequiredCycles = "2", CorrelatedArpReplies = "1", CaptureHealthy = "True", EvidenceId = "EVD-20260822-00153", EvidenceQuality = "INCONCLUSIVE", Reason = "Mudança de associação observada — conflito não confirmado." });
                _rows.Add(new NetworkRow { IP = "192.168.15.80", Hostname = "CAMERA-ISOLADA", Status = "MONITORING_LIMITED", MACs = "00:25:96:AB:CD:10", MACDetails = "Captura indisponível", MacCount = "1", Observations = "2", FirstSeen = "2026-08-22T10:01:03", LastSeen = "2026-08-22T10:04:22", Interface = "Ethernet (#12)", MonitorIp = "192.168.15.3", PositiveRounds = "0", RequiredRounds = "2", ConfirmedCycles = "0", RequiredCycles = "2", CaptureHealthy = "False", EvidenceId = "EVD-20260822-00154", EvidenceQuality = "CAPTURE_UNAVAILABLE", Reason = "Não foi possível confirmar porque a camada de captura ARP não está disponível." });
                _rows.Add(new NetworkRow { IP = "192.168.15.1", Hostname = "GATEWAY-FILIAL", Status = "NORMAL", MACs = "E8:44:8A:2C:2E:10", MACDetails = "Associação atual sem prova de conflito", MacCount = "1", Observations = "9", FirstSeen = "2026-08-22T09:59:01", LastSeen = "2026-08-22T10:04:31", Interface = "Ethernet (#12)", MonitorIp = "192.168.15.3", CaptureHealthy = "True", Reason = "Nenhuma evidência contemporânea de conflito foi encontrada." });
                ApplyFilter();
                _engineState.Text = "  VERIFICAÇÃO ESTRITA PRONTA"; _engineState.ForeColor = Green;
                _engineHint.Text = "1 conflito confirmado  /  1 não verificado  /  1 limitado";
                _networkLabel.Text = "Ethernet  /  192.168.15.3  /  Strict Evidence";
                _updatedLabel.Text = "Atualizado às 10:04:31";
                _footer.Text = "MODO DE DEMONSTRAÇÃO  /  DADOS FICTÍCIOS  /  STRICT EVIDENCE 3.4.0";
                _feed.SetItems(new[]
                {
                    new FeedItem { Time = "10:04:23", Text = "Verificação estrita pronta", Color = Cyan },
                    new FeedItem { Time = "10:04:24", Text = "Requisição ARP correlacionada para 192.168.15.35", Color = Color.FromArgb(173, 196, 220) },
                    new FeedItem { Time = "10:04:29", Text = "2/3 rodadas positivas / ciclo 2/2", Color = Red },
                    new FeedItem { Time = "10:04:31", Text = "Conflito confirmado / Evidence EVD-20260822-00152", Color = Red }
                });
            }

            private void StartWorker(bool once)
            {
                if (_worker != null && !_worker.HasExited) { return; }
                try
                {
                    _continuous = !once;
                    var info = new ProcessStartInfo { FileName = Assembly.GetExecutingAssembly().Location, Arguments = "-Worker" + (once ? " -Once" : String.Empty), WorkingDirectory = Environment.CurrentDirectory, UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden };
                    _worker = Process.Start(info);
                    if (_worker == null) { throw new InvalidOperationException("Não foi possível iniciar o motor nativo."); }
                    UpdateWorkerState();
                }
                catch (Exception exception)
                {
                    _engineState.Text = "[FALHA]  NÃO FOI POSSÍVEL INICIAR";
                    _engineState.ForeColor = Red;
                    _engineHint.Text = exception.Message;
                    _footer.Text = "FALHA AO INICIAR / REVISE A CONFIGURAÇÃO E AS PERMISSÕES DE CAPTURA";
                }
            }

            private void StopWorker(bool ask)
            {
                if (_worker == null) { return; }
                try
                {
                    if (!_worker.HasExited)
                    {
                        try { using (System.Threading.EventWaitHandle stop = System.Threading.EventWaitHandle.OpenExisting(StopEventName)) { stop.Set(); } } catch { }
                        if (!_worker.WaitForExit(5000)) { _worker.Kill(); _worker.WaitForExit(2500); }
                    }
                }
                catch { }
                _worker.Dispose(); _worker = null; _continuous = false; UpdateWorkerState();
            }

            private void TickInterface()
            {
                _animationPhase += 12;
                _scanBar.Phase = _animationPhase;
                if (_scanBar.Active) { _scanBar.Invalidate(); }
                if (_animationPhase % 180 != 0) { return; }
                if (_worker != null && _worker.HasExited)
                {
                    int code = _worker.ExitCode; _worker.Dispose(); _worker = null; _continuous = false;
                    _footer.Text = code == 0 ? "ANÁLISE CONCLUÍDA  /  RELATÓRIOS ATUALIZADOS" : "A ANÁLISE TERMINOU COM CÓDIGO " + code + "  /  CONSULTE A TELEMETRIA";
                }
                UpdateWorkerState();
                RefreshData(false);
            }

            private void UpdateWorkerState()
            {
                bool running = _worker != null && !_worker.HasExited;
                _scanButton.Enabled = !running; _continuousButton.Enabled = !running; _stopButton.Enabled = running; _scanBar.Active = running;
                if (running)
                {
                    _engineState.Text = _continuous ? "[RUN]  MONITORAMENTO CONTÍNUO" : "[RUN]  ANÁLISE EM ANDAMENTO";
                    _engineState.ForeColor = Cyan;
                    _engineHint.Text = _continuous ? "Novos ciclos serão executados enquanto o painel estiver aberto" : "Mapeando IPs e correlacionando identidades MAC";
                    _scanButton.Text = "ANALISANDO...";
                }
                else
                {
                    _engineState.Text = "[READY]  MECANISMO DISPONÍVEL"; _engineState.ForeColor = Green;
                    _engineHint.Text = "Aguardando uma análise"; _scanButton.Text = "ANALISAR REDE";
                }
                _scanBar.Invalidate();
            }

            private void RefreshData(bool force)
            {
                try
                {
                    string root = ConfigurationStore.ResolveOutputDirectory();
                    string snapshot = Path.Combine(root, "reports", "snapshot.csv");
                    if (File.Exists(snapshot))
                    {
                        DateTime write = File.GetLastWriteTime(snapshot);
                        if (force || write != _lastSnapshot) { LoadSnapshot(snapshot); _lastSnapshot = write; _updatedLabel.Text = "Atualizado às " + write.ToString("HH:mm:ss"); }
                    }
                    else if (force) { _rows.Clear(); ApplyFilter(); _updatedLabel.Text = "Nenhum diagnóstico"; }
                    LoadEvents(Path.Combine(root, "logs", "monitor.log"));
                }
                catch (Exception exception) { _footer.Text = "FALHA AO ATUALIZAR PAINEL  /  " + exception.Message; }
            }

            private void LoadSnapshot(string path)
            {
                var loaded = new List<NetworkRow>();
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                {
                    string headerLine = reader.ReadLine(); if (String.IsNullOrWhiteSpace(headerLine)) { return; }
                    List<string> headers = ParseCsv(headerLine);
                    var indexes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    for (int index = 0; index < headers.Count; index++) { indexes[headers[index].TrimStart('\uFEFF')] = index; }
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (String.IsNullOrWhiteSpace(line)) { continue; }
                        List<string> values = ParseCsv(line);
                        loaded.Add(new NetworkRow
                        {
                            Timestamp = Value(indexes, values, "Timestamp"), IP = Value(indexes, values, "IP"), Hostname = Value(indexes, values, "Hostname"), Status = Value(indexes, values, "Status"), MACs = Value(indexes, values, "MACs"), MACDetails = Value(indexes, values, "MACDetails"),
                            MacCount = Value(indexes, values, "MacCount"), Observations = Value(indexes, values, "Observations"), Transitions = Value(indexes, values, "Transitions"), DirectArpMacCount = Value(indexes, values, "DirectArpMacCount"), ActiveProbeMacCount = Value(indexes, values, "ActiveProbeMacCount"),
                            MappingMismatch = Value(indexes, values, "MappingMismatch"), FirstSeen = Value(indexes, values, "FirstSeen"), LastSeen = Value(indexes, values, "LastSeen"), Reason = Value(indexes, values, "Reason"),
                            Interface = Value(indexes, values, "Interface"), MonitorIp = Value(indexes, values, "MonitorIp"), RequestObserved = Value(indexes, values, "RequestObserved"), PositiveRounds = Value(indexes, values, "PositiveRounds"), RequiredRounds = Value(indexes, values, "RequiredRounds"), ConfirmedCycles = Value(indexes, values, "ConfirmedCycles"), RequiredCycles = Value(indexes, values, "RequiredCycles"), CorrelatedArpReplies = Value(indexes, values, "CorrelatedArpReplies"), ProxyArpRisk = Value(indexes, values, "ProxyArpRisk"), GatewayMac = Value(indexes, values, "GatewayMac"), TrustedPair = Value(indexes, values, "TrustedPair"), CaptureHealthy = Value(indexes, values, "CaptureHealthy"), ConfidenceScore = Value(indexes, values, "ConfidenceScore"), EvidenceId = Value(indexes, values, "EvidenceId"), EvidenceHash = Value(indexes, values, "EvidenceHash"), EvidenceQuality = Value(indexes, values, "EvidenceQuality")
                        });
                    }
                }
                _rows.Clear(); _rows.AddRange(loaded.OrderBy(StatusRank).ThenBy(delegate(NetworkRow row) { return IpKey(row.IP); })); ApplyFilter();
            }

            private void ApplyFilter()
            {
                string filter = (_search.Text ?? String.Empty).Trim();
                IEnumerable<NetworkRow> visible = _rows;
                if (filter.Length > 0) { visible = visible.Where(delegate(NetworkRow row) { return Has(row.IP, filter) || Has(row.Hostname, filter) || Has(row.MACs, filter) || Has(row.Status, filter) || Has(row.Reason, filter); }); }
                _grid.Rows.Clear();
                foreach (NetworkRow item in visible)
                {
                    int index = _grid.Rows.Add(item.Status, item.IP, Empty(item.Hostname), item.MACs, item.Observations, Time(item.LastSeen), item.Reason);
                    _grid.Rows[index].Tag = item;
                }
                _totalMetric.MetricValue = _rows.Count.ToString();
                _normalMetric.MetricValue = _rows.Count(delegate(NetworkRow row) { return row.Status == "NORMAL"; }).ToString();
                _attentionMetric.MetricValue = _rows.Count(delegate(NetworkRow row) { return row.Status == "UNVERIFIED" || row.Status == "MONITORING_LIMITED"; }).ToString();
                _conflictMetric.MetricValue = _rows.Count(delegate(NetworkRow row) { return row.Status == "CONFIRMED"; }).ToString();
                _totalMetric.Invalidate(); _normalMetric.Invalidate(); _attentionMetric.Invalidate(); _conflictMetric.Invalidate();
                if (_grid.Rows.Count > 0) { _grid.CurrentCell = _grid.Rows[0].Cells[0]; _grid.Rows[0].Selected = true; ShowDetails(); }
                else { _details.Text = filter.Length > 0 ? "Nenhum endereço corresponde ao filtro informado." : "Execute uma análise para mapear os dispositivos desta rede."; }
            }

            private void ShowDetails()
            {
                if (_grid.SelectedRows.Count == 0 || !(_grid.SelectedRows[0].Tag is NetworkRow)) { return; }
                var row = (NetworkRow)_grid.SelectedRows[0].Tag;
                _details.Text = "IPv4       " + row.IP + "      STATUS  " + StatusText(row.Status) + Environment.NewLine +
                    "DISPOSITIVO " + Empty(row.Hostname) + "      INTERFACE  " + Empty(row.Interface) + Environment.NewLine +
                    "MAC(S)      " + row.MACs + Environment.NewLine +
                    "PROVA       requisição=" + Empty(row.RequestObserved) + "  /  rodadas=" + Empty(row.PositiveRounds) + "/" + Empty(row.RequiredRounds) + "  /  ciclos=" + Empty(row.ConfirmedCycles) + "/" + Empty(row.RequiredCycles) + "  /  respostas=" + Empty(row.CorrelatedArpReplies) + Environment.NewLine +
                    "EVIDENCE    " + Empty(row.EvidenceId) + "  /  qualidade=" + Empty(row.EvidenceQuality) + "  /  captura=" + Empty(row.CaptureHealthy) + Environment.NewLine +
                    "JANELA      " + Time(row.FirstSeen) + "  →  " + Time(row.LastSeen) + Environment.NewLine +
                    "CONCLUSÃO   " + row.Reason;
            }

            private void LoadEvents(string path)
            {
                if (!File.Exists(path)) { _feed.SetItems(new FeedItem[0]); return; }
                var queue = new Queue<string>();
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream, Encoding.UTF8, true))
                {
                    string line; while ((line = reader.ReadLine()) != null) { queue.Enqueue(line); if (queue.Count > 30) { queue.Dequeue(); } }
                }
                var items = new List<FeedItem>();
                foreach (string line in queue)
                {
                    Color color = Color.FromArgb(170, 192, 216);
                    if (line.IndexOf("[CONFLICT]", StringComparison.OrdinalIgnoreCase) >= 0 || line.IndexOf("[ERROR]", StringComparison.OrdinalIgnoreCase) >= 0) { color = Red; }
                    else if (line.IndexOf("[LIMITED]", StringComparison.OrdinalIgnoreCase) >= 0 || line.IndexOf("[WARN]", StringComparison.OrdinalIgnoreCase) >= 0) { color = Amber; }
                    else if (line.IndexOf("[VERIFY]", StringComparison.OrdinalIgnoreCase) >= 0 || line.IndexOf("[STATE]", StringComparison.OrdinalIgnoreCase) >= 0) { color = Blue; }
                    else if (line.IndexOf("Ciclo concluido", StringComparison.OrdinalIgnoreCase) >= 0) { color = Green; }
                    int interfaceIndex = line.IndexOf("Interface=", StringComparison.OrdinalIgnoreCase);
                    if (interfaceIndex >= 0) { _networkLabel.Text = line.Substring(interfaceIndex).TrimEnd('.').Replace(";", "  / "); color = Cyan; }
                    string time = line.Length >= 19 && line[4] == '-' ? line.Substring(11, 8) : "--:--:--";
                    int messageIndex = line.IndexOf("] ", 20, StringComparison.Ordinal);
                    string text = messageIndex >= 0 ? line.Substring(messageIndex + 2).Trim() : (line.Length >= 24 && line[4] == '-' ? line.Substring(24).Trim() : line);
                    items.Add(new FeedItem { Time = time, Text = text, Color = color });
                }
                _feed.SetItems(items);
            }

            private void OpenReports()
            {
                string directory = Path.Combine(ConfigurationStore.ResolveOutputDirectory(), "reports");
                Directory.CreateDirectory(directory);
                Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = "\"" + directory + "\"", UseShellExecute = false });
            }

            private void OpenConfiguration()
            {
                if (_activeWorkspacePage == _settingsPage) { return; }
                if (!CanLeaveWorkspace()) { return; }
                if (_settingsPage != null) { _settingsPage.Dispose(); }
                bool running = _worker != null && !_worker.HasExited;
                _settingsPage = new SettingsExperiencePage(running);
                _settingsPage.BackRequested += RestoreDashboard;
                ShowWorkspacePage(_settingsPage, _settingsNavigation);
            }

            private void ShowHelp()
            {
                if (!CanLeaveWorkspace()) { return; }
                var guide = new OperatorGuidePage();
                guide.BackRequested += RestoreDashboard;
                ShowWorkspacePage(guide, _helpNavigation);
            }

            internal bool TryEnterUpdatePage()
            {
                if (_externalWorkspaceLocked || !CanLeaveWorkspace()) { return false; }
                _externalWorkspaceLocked = true;
                SetWorkspaceNavigationEnabled(false);
                return true;
            }

            internal void LeaveUpdatePage()
            {
                if (!_externalWorkspaceLocked) { return; }
                _externalWorkspaceLocked = false;
                SetWorkspaceNavigationEnabled(true);
            }

            private void SetWorkspaceNavigationEnabled(bool enabled)
            {
                FieldActionButton[] buttons = { _overviewNavigation, _reportsNavigation, _settingsNavigation, _helpNavigation };
                foreach (FieldActionButton button in buttons)
                {
                    if (button != null) { button.Enabled = enabled; }
                }
            }

            private bool CanLeaveWorkspace()
            {
                if (_externalWorkspaceLocked) { return false; }
                if (_activeWorkspacePage == _settingsPage && _settingsPage != null && _settingsPage.HasUnsavedChanges)
                {
                    _settingsPage.ShowUnsavedWarning();
                    SetActiveNavigation(_settingsNavigation);
                    return false;
                }
                return true;
            }

            private void RestoreDashboard()
            {
                if (!CanLeaveWorkspace()) { return; }
                ShowWorkspacePage(_dashboardContent, _overviewNavigation);
            }

            private void ShowWorkspacePage(Control page, FieldActionButton navigation)
            {
                if (page == null) { return; }
                if (_activeWorkspacePage != page)
                {
                    _shell.SuspendLayout();
                    if (_activeWorkspacePage != null) { _shell.Controls.Remove(_activeWorkspacePage); }
                    page.Dock = DockStyle.Fill;
                    _shell.Controls.Add(page, 1, 0);
                    _activeWorkspacePage = page;
                    page.BringToFront();
                    _shell.ResumeLayout(true);
                }
                SetActiveNavigation(navigation);
            }

            private void SetActiveNavigation(FieldActionButton selected)
            {
                FieldActionButton[] buttons = { _overviewNavigation, _reportsNavigation, _settingsNavigation, _helpNavigation };
                foreach (FieldActionButton button in buttons)
                {
                    if (button == null) { continue; }
                    bool active = Object.ReferenceEquals(button, selected);
                    button.StartColor = active ? Color.FromArgb(17, 57, 73) : SidebarColor;
                    button.EndColor = active ? Color.FromArgb(14, 43, 64) : SidebarColor;
                    button.BorderColor = active ? Color.FromArgb(45, 117, 134) : Color.Transparent;
                    button.ForeColor = active ? Cyan : SoftText;
                    button.Invalidate();
                }
            }

            private void HandleFormClosing(object sender, FormClosingEventArgs eventArgs)
            {
                if (_settingsPage != null && _settingsPage.HasUnsavedChanges)
                {
                    eventArgs.Cancel = true;
                    ShowWorkspacePage(_settingsPage, _settingsNavigation);
                    _settingsPage.ShowUnsavedWarning();
                    return;
                }
            }

            private static List<string> ParseCsv(string line)
            {
                var values = new List<string>(); var field = new StringBuilder(); bool quoted = false;
                for (int index = 0; index < line.Length; index++)
                {
                    char character = line[index];
                    if (character == '"') { if (quoted && index + 1 < line.Length && line[index + 1] == '"') { field.Append('"'); index++; } else { quoted = !quoted; } }
                    else if (character == ',' && !quoted) { values.Add(field.ToString()); field.Clear(); }
                    else { field.Append(character); }
                }
                values.Add(field.ToString()); return values;
            }

            private static string Value(Dictionary<string, int> indexes, List<string> values, string name) { int index; return indexes.TryGetValue(name, out index) && index < values.Count ? values[index] : String.Empty; }
            private static int StatusRank(NetworkRow row) { return row.Status == "CONFIRMED" ? 0 : row.Status == "MONITORING_LIMITED" ? 1 : row.Status == "UNVERIFIED" ? 2 : 3; }
            private static long IpKey(string ip) { long value = 0; foreach (string part in (ip ?? String.Empty).Split('.')) { int number; value = (value << 8) + (Int32.TryParse(part, out number) ? number : 0); } return value; }
            private static bool Has(string value, string filter) { return (value ?? String.Empty).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0; }
            private static string Empty(string value) { return String.IsNullOrWhiteSpace(value) ? "—" : value; }
            private static string Time(string value) { DateTime parsed; return DateTime.TryParse(value, out parsed) ? parsed.ToString("dd/MM HH:mm:ss") : Empty(value); }
            private static string StatusText(string value) { return value == "CONFIRMED" ? "CONFLITO CONFIRMADO" : value == "MONITORING_LIMITED" ? "MONITORAMENTO LIMITADO" : value == "UNVERIFIED" ? "NÃO VERIFICADO" : "NORMAL"; }
        }

    }

    internal static class FeedExtensions
    {
        public static IEnumerable<T> TakeLastCompat<T>(this IEnumerable<T> source, int count)
        {
            var queue = new Queue<T>();
            foreach (T item in source) { queue.Enqueue(item); if (queue.Count > count) { queue.Dequeue(); } }
            return queue;
        }
    }
}


