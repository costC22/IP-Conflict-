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
            public string DhcpHostname;
            public string DhcpClientId;
            public string Reason;
        }

        private sealed class FeedItem
        {
            public string Time;
            public string Text;
            public Color Color;
        }

        private sealed class GradientButton : Button
        {
            public Color StartColor { get; set; }
            public Color EndColor { get; set; }
            public Color HoverStartColor { get; set; }
            public Color HoverEndColor { get; set; }
            public Color BorderColor { get; set; }
            public int Radius { get; set; }
            public ContentAlignment CaptionAlignment { get; set; }
            private bool _hover;

            public GradientButton()
            {
                StartColor = Color.FromArgb(35, 210, 238);
                EndColor = Color.FromArgb(72, 121, 255);
                HoverStartColor = Color.FromArgb(74, 226, 245);
                HoverEndColor = Color.FromArgb(98, 145, 255);
                BorderColor = Color.Transparent;
                Radius = 9;
                CaptionAlignment = ContentAlignment.MiddleCenter;
                FlatStyle = FlatStyle.Flat;
                FlatAppearance.BorderSize = 0;
                Cursor = Cursors.Hand;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            }

            protected override void OnMouseEnter(EventArgs eventArgs) { _hover = true; Invalidate(); base.OnMouseEnter(eventArgs); }
            protected override void OnMouseLeave(EventArgs eventArgs) { _hover = false; Invalidate(); base.OnMouseLeave(eventArgs); }

            protected override void OnPaint(PaintEventArgs eventArgs)
            {
                eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle bounds = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
                Color first = Enabled ? (_hover ? HoverStartColor : StartColor) : Color.FromArgb(45, 57, 75);
                Color second = Enabled ? (_hover ? HoverEndColor : EndColor) : Color.FromArgb(38, 49, 66);
                using (GraphicsPath path = CreateRoundPath(bounds, Radius))
                using (var gradient = new LinearGradientBrush(bounds, first, second, LinearGradientMode.Horizontal))
                using (var border = new Pen(BorderColor, 1F))
                {
                    eventArgs.Graphics.FillPath(gradient, path);
                    if (BorderColor.A > 0) { eventArgs.Graphics.DrawPath(border, path); }
                }
                Rectangle textBounds = CaptionAlignment == ContentAlignment.MiddleLeft ? new Rectangle(16, 0, Width - 24, Height) : bounds;
                TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
                flags |= CaptionAlignment == ContentAlignment.MiddleLeft ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter;
                TextRenderer.DrawText(eventArgs.Graphics, Text, Font, textBounds, Enabled ? ForeColor : Color.FromArgb(119, 134, 153), flags);
            }
        }

        private sealed class HeroPanel : Panel
        {
            public Color AccentColor { get; set; }
            public HeroPanel()
            {
                AccentColor = Color.FromArgb(43, 211, 239);
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            }

            protected override void OnPaintBackground(PaintEventArgs eventArgs)
            {
                Rectangle bounds = ClientRectangle;
                if (bounds.Width <= 0 || bounds.Height <= 0) { return; }
                using (var gradient = new LinearGradientBrush(bounds, Color.FromArgb(14, 29, 51), Color.FromArgb(8, 19, 36), LinearGradientMode.Horizontal)) { eventArgs.Graphics.FillRectangle(gradient, bounds); }
                eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var glow = new SolidBrush(Color.FromArgb(22, AccentColor))) { eventArgs.Graphics.FillEllipse(glow, bounds.Width - 360, -170, 430, 300); }
                using (var line = new Pen(Color.FromArgb(32, 73, 112), 1F))
                {
                    Point[] points = { new Point(bounds.Width - 420, 82), new Point(bounds.Width - 335, 34), new Point(bounds.Width - 244, 64), new Point(bounds.Width - 145, 24), new Point(bounds.Width - 45, 59) };
                    eventArgs.Graphics.DrawLines(line, points);
                    foreach (Point point in points) { eventArgs.Graphics.FillEllipse(new SolidBrush(Color.FromArgb(115, AccentColor)), point.X - 3, point.Y - 3, 6, 6); }
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
                AccentColor = Color.FromArgb(43, 211, 239);
                MetricTitle = String.Empty;
                MetricValue = "0";
                Symbol = "•";
                Padding = new Padding(0);
            }

            protected override void OnPaint(PaintEventArgs eventArgs)
            {
                Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
                eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = CreateRoundPath(bounds, 10))
                using (var fill = new LinearGradientBrush(bounds, Color.FromArgb(17, 31, 52), Color.FromArgb(12, 24, 43), LinearGradientMode.Vertical))
                using (var border = new Pen(Color.FromArgb(38, 58, 82)))
                {
                    eventArgs.Graphics.FillPath(fill, path);
                    eventArgs.Graphics.DrawPath(border, path);
                }
                using (var accent = new SolidBrush(AccentColor)) { eventArgs.Graphics.FillRectangle(accent, 0, 0, Width, 3); }
                using (var halo = new SolidBrush(Color.FromArgb(22, AccentColor))) { eventArgs.Graphics.FillEllipse(halo, Width - 68, 18, 42, 42); }
                TextRenderer.DrawText(eventArgs.Graphics, Symbol, new Font("Segoe UI Symbol", 14F, FontStyle.Bold), new Rectangle(Width - 67, 18, 40, 42), AccentColor, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                TextRenderer.DrawText(eventArgs.Graphics, MetricValue, new Font("Segoe UI Semibold", 24F), new Rectangle(18, 15, Width - 88, 44), Color.FromArgb(239, 246, 255), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                TextRenderer.DrawText(eventArgs.Graphics, MetricTitle, new Font("Segoe UI Semibold", 7.5F), new Rectangle(18, 63, Width - 32, 27), Color.FromArgb(133, 159, 189), TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.EndEllipsis);
            }
        }

        private sealed class EventFeed : Control
        {
            private readonly List<FeedItem> _items = new List<FeedItem>();
            public EventFeed() { DoubleBuffered = true; BackColor = Color.FromArgb(8, 18, 33); }
            public void SetItems(IEnumerable<FeedItem> items) { _items.Clear(); _items.AddRange(items.TakeLastCompat(7)); Invalidate(); }

            protected override void OnPaint(PaintEventArgs eventArgs)
            {
                eventArgs.Graphics.Clear(BackColor);
                eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                int y = 8;
                foreach (FeedItem item in _items)
                {
                    using (var halo = new SolidBrush(Color.FromArgb(32, item.Color))) { eventArgs.Graphics.FillEllipse(halo, 8, y + 3, 14, 14); }
                    using (var dot = new SolidBrush(item.Color)) { eventArgs.Graphics.FillEllipse(dot, 13, y + 8, 4, 4); }
                    TextRenderer.DrawText(eventArgs.Graphics, item.Time, new Font("Consolas", 7.5F), new Rectangle(28, y, 58, 20), Color.FromArgb(92, 118, 146), TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
                    TextRenderer.DrawText(eventArgs.Graphics, item.Text, new Font("Segoe UI", 8F), new Rectangle(88, y, Width - 98, 20), item.Color, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    y += 22;
                    if (y > Height - 19) { break; }
                }
                if (_items.Count == 0) { TextRenderer.DrawText(eventArgs.Graphics, "A atividade aparecerá após a primeira análise.", new Font("Segoe UI", 8.5F), ClientRectangle, Color.FromArgb(100, 127, 155), TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter); }
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

        private sealed class NetworkOperationsForm : Form
        {
            private static readonly Color Canvas = Color.FromArgb(6, 12, 23);
            private static readonly Color SidebarColor = Color.FromArgb(8, 17, 31);
            private static readonly Color Surface = Color.FromArgb(13, 25, 44);
            private static readonly Color SurfaceRaised = Color.FromArgb(17, 32, 54);
            private static readonly Color Stroke = Color.FromArgb(37, 58, 82);
            private static readonly Color MainText = Color.FromArgb(237, 245, 253);
            private static readonly Color SoftText = Color.FromArgb(142, 166, 195);
            private static readonly Color Cyan = Color.FromArgb(43, 213, 237);
            private static readonly Color Blue = Color.FromArgb(75, 125, 255);
            private static readonly Color Purple = Color.FromArgb(158, 102, 255);
            private static readonly Color Green = Color.FromArgb(55, 222, 151);
            private static readonly Color Amber = Color.FromArgb(249, 184, 68);
            private static readonly Color Red = Color.FromArgb(255, 88, 113);

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
            private readonly GradientButton _scanButton;
            private readonly GradientButton _continuousButton;
            private readonly GradientButton _stopButton;
            private readonly ScanBar _scanBar;
            private readonly MetricPanel _totalMetric;
            private readonly MetricPanel _normalMetric;
            private readonly MetricPanel _attentionMetric;
            private readonly MetricPanel _conflictMetric;
            private readonly Timer _timer;
            private Process _worker;
            private bool _continuous;
            private DateTime _lastSnapshot = DateTime.MinValue;
            private int _animationPhase;

            public NetworkOperationsForm(bool demoMode)
            {
                _demoMode = demoMode;
                Text = "IPConflictMonitor 3 — Network Operations";
                Icon = SystemIcons.Shield;
                BackColor = Canvas;
                ForeColor = MainText;
                Font = new Font("Segoe UI", 9F);
                AutoScaleMode = AutoScaleMode.Dpi;
                MinimumSize = new Size(1160, 740);
                Size = new Size(1440, 900);
                StartPosition = FormStartPosition.CenterScreen;
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);

                var shell = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = Canvas, Padding = new Padding(0), Margin = new Padding(0) };
                shell.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 226));
                shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                Controls.Add(shell);
                shell.Controls.Add(BuildSidebar(), 0, 0);

                var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7, BackColor = Canvas, Padding = new Padding(20, 0, 20, 0), Margin = new Padding(0) };
                content.RowStyles.Add(new RowStyle(SizeType.Absolute, 108));
                content.RowStyles.Add(new RowStyle(SizeType.Absolute, 78));
                content.RowStyles.Add(new RowStyle(SizeType.Absolute, 116));
                content.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
                content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                content.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
                content.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
                shell.Controls.Add(content, 1, 0);

                content.Controls.Add(BuildHero(out _scanButton, out _continuousButton), 0, 0);
                content.Controls.Add(BuildEngineCard(out _engineState, out _engineHint, out _networkLabel, out _updatedLabel, out _stopButton, out _scanBar), 0, 1);
                content.Controls.Add(BuildMetrics(out _totalMetric, out _normalMetric, out _attentionMetric, out _conflictMetric), 0, 2);
                content.Controls.Add(BuildSearch(out _search), 0, 3);
                _grid = BuildGrid();
                content.Controls.Add(_grid, 0, 4);
                content.Controls.Add(BuildLowerDeck(out _details, out _feed), 0, 5);
                _footer = new Label { Dock = DockStyle.Fill, Text = "FIELD EDITION 3.1  •  MOTOR C# NATIVO  •  SEM POWERSHELL  •  SEM INSTALAÇÃO", ForeColor = Color.FromArgb(87, 114, 143), Font = new Font("Segoe UI Semibold", 7.3F), TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(2, 0, 0, 0) };
                content.Controls.Add(_footer, 0, 6);

                _search.HandleCreated += delegate { SendMessage(_search.Handle, 0x1501, new IntPtr(1), "Buscar por IP, nome do dispositivo, MAC ou diagnóstico..."); };
                _search.TextChanged += delegate { ApplyFilter(); };
                _grid.SelectionChanged += delegate { ShowDetails(); };
                _scanButton.Click += delegate { StartWorker(true); };
                _continuousButton.Click += delegate { StartWorker(false); };
                _stopButton.Click += delegate { StopWorker(true); };
                Shown += delegate { if (_demoMode) { LoadDemo(); } else { RefreshData(true); } };
                FormClosing += delegate { StopWorker(false); };
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

            private Control BuildSidebar()
            {
                var panel = new Panel { Dock = DockStyle.Fill, BackColor = SidebarColor, Padding = new Padding(17, 0, 17, 18), Margin = new Padding(0) };
                panel.Paint += delegate(object sender, PaintEventArgs eventArgs)
                {
                    using (var separator = new Pen(Color.FromArgb(28, 48, 69))) { eventArgs.Graphics.DrawLine(separator, panel.Width - 1, 0, panel.Width - 1, panel.Height); }
                    using (var accent = new LinearGradientBrush(new Rectangle(0, 0, panel.Width, 3), Cyan, Purple, LinearGradientMode.Horizontal)) { eventArgs.Graphics.FillRectangle(accent, 0, 0, panel.Width, 3); }
                };
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 9, BackColor = SidebarColor, Padding = new Padding(0), Margin = new Padding(0) };
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 112));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 49));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 49));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 49));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 49));
                layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 116));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 76));

                var brand = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, BackColor = SidebarColor, Padding = new Padding(0, 26, 0, 20), Margin = new Padding(0) };
                brand.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52));
                brand.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                brand.RowStyles.Add(new RowStyle(SizeType.Percent, 60));
                brand.RowStyles.Add(new RowStyle(SizeType.Percent, 40));
                var logo = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(16, 47, 67), Margin = new Padding(0, 0, 9, 0) };
                logo.Paint += delegate(object sender, PaintEventArgs eventArgs)
                {
                    eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    using (var gradient = new LinearGradientBrush(logo.ClientRectangle, Cyan, Blue, LinearGradientMode.ForwardDiagonal)) { eventArgs.Graphics.FillRectangle(gradient, logo.ClientRectangle); }
                    using (var pen = new Pen(Color.FromArgb(5, 28, 40), 2.5F)) { eventArgs.Graphics.DrawLines(pen, new[] { new Point(11, 27), new Point(19, 20), new Point(26, 29), new Point(35, 18) }); }
                };
                brand.Controls.Add(logo, 0, 0); brand.SetRowSpan(logo, 2);
                brand.Controls.Add(new Label { Text = "IP CONFLICT", Dock = DockStyle.Fill, ForeColor = MainText, Font = new Font("Segoe UI Semibold", 11.5F), TextAlign = ContentAlignment.BottomLeft }, 1, 0);
                brand.Controls.Add(new Label { Text = "NETWORK OPS", Dock = DockStyle.Fill, ForeColor = Cyan, Font = new Font("Segoe UI Semibold", 7.3F), TextAlign = ContentAlignment.TopLeft }, 1, 1);
                layout.Controls.Add(brand, 0, 0);
                layout.Controls.Add(new Label { Text = "CENTRAL DE OPERAÇÕES", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(81, 110, 142), Font = new Font("Segoe UI Semibold", 7F), TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
                layout.Controls.Add(CreateNav("●   VISÃO GERAL", true, delegate { _search.Clear(); }), 0, 2);
                layout.Controls.Add(CreateNav("▤   RELATÓRIOS", false, delegate { OpenReports(); }), 0, 3);
                layout.Controls.Add(CreateNav("⚙   CONFIGURAÇÃO", false, delegate { OpenConfiguration(); }), 0, 4);
                layout.Controls.Add(CreateNav("?   AJUDA RÁPIDA", false, delegate { ShowHelp(); }), 0, 5);

                var steps = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(10, 24, 41), Margin = new Padding(0, 5, 0, 7), Padding = new Padding(12, 10, 12, 8) };
                steps.Paint += PaintCardBorder;
                var stepLayout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1, BackColor = steps.BackColor, Margin = new Padding(0) };
                stepLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
                stepLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
                stepLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 24));
                stepLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                stepLayout.Controls.Add(new Label { Text = "FLUXO DO TÉCNICO", Dock = DockStyle.Fill, ForeColor = Cyan, Font = new Font("Segoe UI Semibold", 7.3F), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
                stepLayout.Controls.Add(new Label { Text = "01  Analisar a rede", Dock = DockStyle.Fill, ForeColor = MainText, Font = new Font("Segoe UI", 8F), TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
                stepLayout.Controls.Add(new Label { Text = "02  Ver conflitos", Dock = DockStyle.Fill, ForeColor = MainText, Font = new Font("Segoe UI", 8F), TextAlign = ContentAlignment.MiddleLeft }, 0, 2);
                stepLayout.Controls.Add(new Label { Text = "03  Conferir evidências", Dock = DockStyle.Fill, ForeColor = MainText, Font = new Font("Segoe UI", 8F), TextAlign = ContentAlignment.MiddleLeft }, 0, 3);
                steps.Controls.Add(stepLayout);
                layout.Controls.Add(steps, 0, 7);

                var safety = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(9, 33, 39), Margin = new Padding(0), Padding = new Padding(12, 8, 10, 7) };
                safety.Paint += PaintCardBorder;
                safety.Controls.Add(new Label { Text = "✓  EXECUÇÃO NATIVA E PORTÁTIL\n     Sem scripts ou instalação automática", Dock = DockStyle.Fill, ForeColor = Green, Font = new Font("Segoe UI Semibold", 7.7F), TextAlign = ContentAlignment.MiddleLeft });
                layout.Controls.Add(safety, 0, 8);
                panel.Controls.Add(layout);
                return panel;
            }

            private GradientButton CreateNav(string text, bool selected, EventHandler action)
            {
                Color fill = selected ? Color.FromArgb(17, 57, 73) : SidebarColor;
                var button = new GradientButton
                {
                    Text = text, Dock = DockStyle.Fill, Margin = new Padding(0, 4, 0, 4), StartColor = fill, EndColor = selected ? Color.FromArgb(17, 45, 67) : SidebarColor,
                    HoverStartColor = Color.FromArgb(21, 54, 73), HoverEndColor = Color.FromArgb(18, 42, 62), BorderColor = selected ? Color.FromArgb(38, 108, 123) : Color.Transparent,
                    ForeColor = selected ? Cyan : SoftText, Font = new Font("Segoe UI Semibold", 8F), Radius = 8, CaptionAlignment = ContentAlignment.MiddleLeft
                };
                button.Click += action;
                return button;
            }

            private Control BuildHero(out GradientButton scan, out GradientButton continuous)
            {
                var hero = new HeroPanel { Dock = DockStyle.Fill, Margin = new Padding(0, 12, 0, 10), Padding = new Padding(18, 0, 16, 0) };
                hero.Paint += PaintCardBorder;
                var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = Color.Transparent, Margin = new Padding(0) };
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 165));
                layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
                var titleBox = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1, BackColor = Color.Transparent, Margin = new Padding(0), Padding = new Padding(0, 8, 0, 7) };
                titleBox.RowStyles.Add(new RowStyle(SizeType.Absolute, 21));
                titleBox.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
                titleBox.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
                titleBox.Controls.Add(new Label { Text = "NETWORK INTELLIGENCE  /  FIELD EDITION 3.1", Dock = DockStyle.Fill, ForeColor = Cyan, Font = new Font("Segoe UI Semibold", 7.2F), TextAlign = ContentAlignment.BottomLeft, BackColor = Color.Transparent }, 0, 0);
                titleBox.Controls.Add(new Label { Text = "Radar de conflitos IPv4", Dock = DockStyle.Fill, ForeColor = MainText, Font = new Font("Segoe UI Semibold", 20F), TextAlign = ContentAlignment.BottomLeft, BackColor = Color.Transparent }, 0, 1);
                titleBox.Controls.Add(new Label { Text = "Descubra quando dois dispositivos disputam o mesmo endereço na rede local", Dock = DockStyle.Fill, ForeColor = SoftText, Font = new Font("Segoe UI", 8.7F), TextAlign = ContentAlignment.TopLeft, BackColor = Color.Transparent }, 0, 2);
                layout.Controls.Add(titleBox, 0, 0);
                continuous = new GradientButton { Text = "◉  MONITORAR", Dock = DockStyle.Fill, Margin = new Padding(8, 22, 8, 22), StartColor = Color.FromArgb(23, 39, 63), EndColor = Color.FromArgb(17, 31, 52), HoverStartColor = Color.FromArgb(29, 51, 78), HoverEndColor = Color.FromArgb(22, 41, 64), BorderColor = Color.FromArgb(50, 77, 105), ForeColor = MainText, Font = new Font("Segoe UI Semibold", 8.3F), Radius = 9 };
                layout.Controls.Add(continuous, 1, 0);
                scan = new GradientButton { Text = "ANALISAR REDE  →", Dock = DockStyle.Fill, Margin = new Padding(8, 22, 0, 22), ForeColor = Color.FromArgb(4, 25, 38), Font = new Font("Segoe UI Semibold", 8.5F), Radius = 9 };
                layout.Controls.Add(scan, 2, 0);
                hero.Controls.Add(layout);
                return hero;
            }

            private Control BuildEngineCard(out Label state, out Label hint, out Label network, out Label updated, out GradientButton stop, out ScanBar scanBar)
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
                state = new Label { Text = "●  MOTOR NATIVO PRONTO", Dock = DockStyle.Fill, ForeColor = Green, Font = new Font("Segoe UI Semibold", 8.5F), TextAlign = ContentAlignment.BottomLeft };
                hint = new Label { Text = "Aguardando uma análise", Dock = DockStyle.Fill, ForeColor = SoftText, Font = new Font("Segoe UI", 8F), TextAlign = ContentAlignment.TopLeft };
                stateBox.Controls.Add(state, 0, 0); stateBox.Controls.Add(hint, 0, 1);
                layout.Controls.Add(stateBox, 0, 0);
                network = new Label { Text = "Interface e CIDR serão detectados automaticamente", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(162, 184, 209), Font = new Font("Consolas", 8.1F), TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, Padding = new Padding(10, 0, 0, 0) };
                layout.Controls.Add(network, 1, 0);
                layout.Controls.Add(CreateEngineBadge("ARP + ICMP", Cyan), 2, 0);
                updated = new Label { Text = "Nenhum diagnóstico", Dock = DockStyle.Fill, ForeColor = SoftText, Font = new Font("Segoe UI", 8F), TextAlign = ContentAlignment.MiddleRight };
                layout.Controls.Add(updated, 3, 0);
                stop = new GradientButton { Text = "■  PARAR", Dock = DockStyle.Fill, Margin = new Padding(12, 7, 0, 7), StartColor = Color.FromArgb(52, 28, 42), EndColor = Color.FromArgb(40, 25, 38), HoverStartColor = Color.FromArgb(75, 33, 49), HoverEndColor = Color.FromArgb(55, 28, 42), BorderColor = Color.FromArgb(94, 45, 60), ForeColor = Red, Font = new Font("Segoe UI Semibold", 7.7F), Radius = 7, Enabled = false };
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
                total = CreateMetric("ENDEREÇOS OBSERVADOS", "◎", Blue, new Padding(0, 0, 7, 0));
                normal = CreateMetric("ASSOCIAÇÕES NORMAIS", "✓", Green, new Padding(3, 0, 4, 0));
                attention = CreateMetric("REQUEREM ATENÇÃO", "!", Amber, new Padding(4, 0, 3, 0));
                conflicts = CreateMetric("CONFLITOS CONFIRMADOS", "×", Red, new Padding(7, 0, 0, 0));
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
                layout.Controls.Add(new Label { Text = "⌕", Dock = DockStyle.Fill, ForeColor = Cyan, Font = new Font("Segoe UI Symbol", 14F), TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
                search = new TextBox { Dock = DockStyle.Fill, BackColor = Surface, ForeColor = MainText, BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 9.3F), Margin = new Padding(0, 4, 10, 0) };
                layout.Controls.Add(search, 1, 0);
                layout.Controls.Add(new Label { Text = "IP  •  HOST  •  MAC  •  STATUS", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(88, 118, 148), Font = new Font("Segoe UI Semibold", 7.1F), TextAlign = ContentAlignment.MiddleRight }, 2, 0);
                var clear = new GradientButton { Text = "LIMPAR", Dock = DockStyle.Fill, Margin = new Padding(12, 0, 0, 0), StartColor = SurfaceRaised, EndColor = Color.FromArgb(15, 29, 49), HoverStartColor = Color.FromArgb(29, 48, 72), HoverEndColor = Color.FromArgb(22, 39, 61), BorderColor = Stroke, ForeColor = SoftText, Font = new Font("Segoe UI Semibold", 7.2F), Radius = 6 };
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
                Color color = raw == "CONFIRMED" ? Red : raw == "SUSPECT" ? Amber : Green;
                string text = raw == "CONFIRMED" ? "CONFLITO" : raw == "SUSPECT" ? "ATENÇÃO" : "NORMAL";
                Rectangle badge = new Rectangle(eventArgs.CellBounds.X + 13, eventArgs.CellBounds.Y + 8, eventArgs.CellBounds.Width - 26, eventArgs.CellBounds.Height - 16);
                eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (GraphicsPath path = CreateRoundPath(badge, 8))
                using (var fill = new SolidBrush(Color.FromArgb(38, color)))
                using (var border = new Pen(Color.FromArgb(82, color))) { eventArgs.Graphics.FillPath(fill, path); eventArgs.Graphics.DrawPath(border, path); }
                TextRenderer.DrawText(eventArgs.Graphics, text, new Font("Segoe UI Semibold", 7.1F), badge, color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                eventArgs.Handled = true;
            }

            private void PaintRowAccent(object sender, DataGridViewRowPrePaintEventArgs eventArgs)
            {
                object value = _grid.Rows[eventArgs.RowIndex].Cells[0].Value;
                string status = Convert.ToString(value);
                Color color = status == "CONFIRMED" ? Red : status == "SUSPECT" ? Amber : Color.Transparent;
                if (color.A == 0) { return; }
                using (var brush = new SolidBrush(color)) { eventArgs.Graphics.FillRectangle(brush, eventArgs.RowBounds.X, eventArgs.RowBounds.Y, 3, eventArgs.RowBounds.Height); }
            }

            private void LoadDemo()
            {
                _rows.Clear();
                _rows.Add(new NetworkRow { IP = "192.168.15.35", Hostname = "IMPRESSORA-RECEPCAO", Status = "CONFIRMED", MACs = "00:1A:2B:3C:4D:5E, 70:8A:09:11:22:33", MACDetails = "Múltiplas identidades de camada 2", MacCount = "2", Observations = "8", Transitions = "3", DirectArpMacCount = "2", ActiveProbeMacCount = "2", FirstSeen = "2026-07-29T10:02:11", LastSeen = "2026-07-29T10:04:29", Reason = "Dois MACs responderam durante a mesma captura ARP." });
                _rows.Add(new NetworkRow { IP = "192.168.15.21", Hostname = "NOTEBOOK-CAMPO", Status = "SUSPECT", MACs = "34:AB:90:12:CD:44, 98:76:54:32:10:FE", MACDetails = "Alternância recente", MacCount = "2", Observations = "4", Transitions = "1", DirectArpMacCount = "1", ActiveProbeMacCount = "1", FirstSeen = "2026-07-29T10:01:03", LastSeen = "2026-07-29T10:04:22", Reason = "Mais de um MAC observado na janela de evidências." });
                _rows.Add(new NetworkRow { IP = "192.168.15.1", Hostname = "GATEWAY-FILIAL", Status = "NORMAL", MACs = "E8:45:8B:2D:2F:10", MACDetails = "Associação estável", MacCount = "1", Observations = "9", Transitions = "0", DirectArpMacCount = "1", ActiveProbeMacCount = "1", FirstSeen = "2026-07-29T09:59:01", LastSeen = "2026-07-29T10:04:31", Reason = "Um único MAC observado." });
                _rows.Add(new NetworkRow { IP = "192.168.15.10", Hostname = "PDV-CAIXA-01", Status = "NORMAL", MACs = "98:2F:F8:A9:27:19", MACDetails = "Associação estável", MacCount = "1", Observations = "9", Transitions = "0", DirectArpMacCount = "1", ActiveProbeMacCount = "1", FirstSeen = "2026-07-29T09:59:01", LastSeen = "2026-07-29T10:04:31", Reason = "Um único MAC observado." });
                _rows.Add(new NetworkRow { IP = "192.168.15.12", Hostname = "CAMERA-ESTOQUE", Status = "NORMAL", MACs = "6E:3F:FA:04:5A:3B", MACDetails = "Associação estável", MacCount = "1", Observations = "7", Transitions = "0", DirectArpMacCount = "1", ActiveProbeMacCount = "1", FirstSeen = "2026-07-29T10:00:18", LastSeen = "2026-07-29T10:04:28", Reason = "Um único MAC observado." });
                ApplyFilter();
                _engineState.Text = "●  ANÁLISE CONCLUÍDA"; _engineState.ForeColor = Green;
                _engineHint.Text = "1 conflito confirmado  •  1 endereço requer atenção";
                _networkLabel.Text = "Ethernet  /  192.168.15.3  /  192.168.15.0/24";
                _updatedLabel.Text = "Atualizado às 10:04:31";
                _footer.Text = "MODO DE DEMONSTRAÇÃO  •  DADOS FICTÍCIOS  •  NENHUMA ATIVIDADE DE REDE EXECUTADA";
                _feed.SetItems(new[]
                {
                    new FeedItem { Time = "10:04:23", Text = "Motor C# nativo iniciado", Color = Cyan },
                    new FeedItem { Time = "10:04:24", Text = "Rede 192.168.15.0/24 • 254 alvos", Color = Color.FromArgb(173, 196, 220) },
                    new FeedItem { Time = "10:04:29", Text = "Dois MACs responderam para 192.168.15.35", Color = Red },
                    new FeedItem { Time = "10:04:31", Text = "Ciclo concluído • 5 dispositivos observados", Color = Green }
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
                catch (Exception exception) { MessageBox.Show(this, exception.Message, "Falha ao iniciar", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            }

            private void StopWorker(bool ask)
            {
                if (_worker == null) { return; }
                try
                {
                    if (!_worker.HasExited)
                    {
                        if (ask && MessageBox.Show(this, "Encerrar a análise de rede em andamento?", "IPConflictMonitor", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) { return; }
                        _worker.Kill(); _worker.WaitForExit(2500);
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
                    _footer.Text = code == 0 ? "ANÁLISE CONCLUÍDA  •  RELATÓRIOS ATUALIZADOS" : "A ANÁLISE TERMINOU COM CÓDIGO " + code + "  •  CONSULTE A TELEMETRIA";
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
                    _engineState.Text = _continuous ? "●  MONITORAMENTO CONTÍNUO" : "●  VARREDURA EM ANDAMENTO";
                    _engineState.ForeColor = Cyan;
                    _engineHint.Text = _continuous ? "Novos ciclos serão executados enquanto o painel estiver aberto" : "Mapeando IPs e correlacionando identidades MAC";
                    _scanButton.Text = "ANALISANDO...";
                }
                else
                {
                    _engineState.Text = "●  MOTOR NATIVO PRONTO"; _engineState.ForeColor = Green;
                    _engineHint.Text = "Aguardando uma análise"; _scanButton.Text = "ANALISAR REDE  →";
                }
                _scanBar.Invalidate();
            }

            private void RefreshData(bool force)
            {
                try
                {
                    string root = GetUserDataDirectory();
                    string snapshot = Path.Combine(root, "reports", "snapshot.csv");
                    if (File.Exists(snapshot))
                    {
                        DateTime write = File.GetLastWriteTime(snapshot);
                        if (force || write != _lastSnapshot) { LoadSnapshot(snapshot); _lastSnapshot = write; _updatedLabel.Text = "Atualizado às " + write.ToString("HH:mm:ss"); }
                    }
                    else if (force) { _rows.Clear(); ApplyFilter(); _updatedLabel.Text = "Nenhum diagnóstico"; }
                    LoadEvents(Path.Combine(root, "logs", "monitor.log"));
                }
                catch (Exception exception) { _footer.Text = "FALHA AO ATUALIZAR PAINEL  •  " + exception.Message; }
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
                            MappingMismatch = Value(indexes, values, "MappingMismatch"), FirstSeen = Value(indexes, values, "FirstSeen"), LastSeen = Value(indexes, values, "LastSeen"), DhcpHostname = Value(indexes, values, "DhcpHostname"), DhcpClientId = Value(indexes, values, "DhcpClientId"), Reason = Value(indexes, values, "Reason")
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
                _attentionMetric.MetricValue = _rows.Count(delegate(NetworkRow row) { return row.Status == "SUSPECT"; }).ToString();
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
                    "DISPOSITIVO " + Empty(row.Hostname) + Environment.NewLine +
                    "MAC(S)      " + row.MACs + Environment.NewLine +
                    "EVIDÊNCIAS  " + row.Observations + " observações  •  " + row.Transitions + " transições  •  ARP " + row.DirectArpMacCount + "  •  sondagem " + row.ActiveProbeMacCount + Environment.NewLine +
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
                    if (line.IndexOf("[CONFIRMED]", StringComparison.OrdinalIgnoreCase) >= 0 || line.IndexOf("[ERROR]", StringComparison.OrdinalIgnoreCase) >= 0) { color = Red; }
                    else if (line.IndexOf("[SUSPECT]", StringComparison.OrdinalIgnoreCase) >= 0 || line.IndexOf("[WARN]", StringComparison.OrdinalIgnoreCase) >= 0) { color = Amber; }
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
                string directory = Path.Combine(GetUserDataDirectory(), "reports"); Directory.CreateDirectory(directory);
                Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = "\"" + directory + "\"", UseShellExecute = false });
            }

            private void OpenConfiguration()
            {
                string sidecar = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "config", "config.json");
                string local = Path.Combine(GetUserDataDirectory(), "config", "config.json");
                string path = File.Exists(sidecar) ? sidecar : local;
                if (!File.Exists(path)) { MessageBox.Show(this, "A configuração será criada na primeira análise.", "Configuração", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
                Process.Start(new ProcessStartInfo { FileName = "notepad.exe", Arguments = "\"" + path + "\"", UseShellExecute = false });
            }

            private void ShowHelp()
            {
                MessageBox.Show(this, "1. Clique em ANALISAR REDE para uma verificação única.\n\n2. Use MONITORAR para repetir as análises durante o atendimento.\n\n3. CONFLITO indica dois MACs confirmados no mesmo IPv4. ATENÇÃO indica evidência que ainda precisa de confirmação.\n\nPara maior precisão, execute manualmente como Administrador e instale TShark/Npcap.", "Ajuda rápida", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
            private static int StatusRank(NetworkRow row) { return row.Status == "CONFIRMED" ? 0 : row.Status == "SUSPECT" ? 1 : 2; }
            private static long IpKey(string ip) { long value = 0; foreach (string part in (ip ?? String.Empty).Split('.')) { int number; value = (value << 8) + (Int32.TryParse(part, out number) ? number : 0); } return value; }
            private static bool Has(string value, string filter) { return (value ?? String.Empty).IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0; }
            private static string Empty(string value) { return String.IsNullOrWhiteSpace(value) ? "—" : value; }
            private static string Time(string value) { DateTime parsed; return DateTime.TryParse(value, out parsed) ? parsed.ToString("dd/MM HH:mm:ss") : Empty(value); }
            private static string StatusText(string value) { return value == "CONFIRMED" ? "CONFLITO CONFIRMADO" : value == "SUSPECT" ? "REQUER ATENÇÃO" : "NORMAL"; }
        }

        private static GraphicsPath CreateRoundPath(Rectangle rectangle, int radius)
        {
            int diameter = Math.Max(2, radius * 2);
            var path = new GraphicsPath();
            path.AddArc(rectangle.Left, rectangle.Top, diameter, diameter, 180, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Top, diameter, diameter, 270, 90);
            path.AddArc(rectangle.Right - diameter, rectangle.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(rectangle.Left, rectangle.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
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



