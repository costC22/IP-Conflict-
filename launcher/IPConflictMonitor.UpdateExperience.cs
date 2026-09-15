using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace IPConflictMonitor.Launcher
{
    internal static class FieldTheme
    {
        public static readonly Color Canvas = Color.FromArgb(6, 12, 23);
        public static readonly Color Sidebar = Color.FromArgb(8, 17, 31);
        public static readonly Color Surface = Color.FromArgb(13, 25, 44);
        public static readonly Color SurfaceRaised = Color.FromArgb(17, 32, 54);
        public static readonly Color Stroke = Color.FromArgb(37, 58, 82);
        public static readonly Color MainText = Color.FromArgb(237, 245, 253);
        public static readonly Color SoftText = Color.FromArgb(142, 166, 195);
        public static readonly Color MutedText = Color.FromArgb(86, 116, 146);
        public static readonly Color Cyan = Color.FromArgb(43, 213, 237);
        public static readonly Color Blue = Color.FromArgb(75, 125, 255);
        public static readonly Color Purple = Color.FromArgb(158, 102, 255);
        public static readonly Color Green = Color.FromArgb(55, 222, 151);
        public static readonly Color Amber = Color.FromArgb(249, 184, 68);
        public static readonly Color Red = Color.FromArgb(255, 88, 113);
    }

    internal enum UpdateStage
    {
        Check = 0,
        Download = 1,
        Integrity = 2,
        Install = 3,
        Restart = 4
    }

    internal sealed class UpdateProgressInfo
    {
        public UpdateStage Stage;
        public string Title;
        public string Description;
        public int Percent = -1;
    }

    internal sealed class FieldActionButton : Button
    {
        private bool _hover;
        public Color StartColor { get; set; }
        public Color EndColor { get; set; }
        public Color HoverStartColor { get; set; }
        public Color HoverEndColor { get; set; }
        public Color BorderColor { get; set; }
        public int Radius { get; set; }
        public ContentAlignment CaptionAlignment { get; set; }

        public FieldActionButton()
        {
            StartColor = FieldTheme.Cyan;
            EndColor = FieldTheme.Blue;
            HoverStartColor = Color.FromArgb(74, 226, 245);
            HoverEndColor = Color.FromArgb(98, 145, 255);
            BorderColor = Color.Transparent;
            Radius = 6;
            CaptionAlignment = ContentAlignment.MiddleCenter;
            ForeColor = Color.FromArgb(4, 25, 38);
            Font = new Font("Segoe UI Semibold", 9F);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor = Cursors.Hand;
            Height = 46;
            MinimumSize = Size.Empty;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnMouseEnter(EventArgs eventArgs) { _hover = true; Invalidate(); base.OnMouseEnter(eventArgs); }
        protected override void OnMouseLeave(EventArgs eventArgs) { _hover = false; Invalidate(); base.OnMouseLeave(eventArgs); }
        protected override void OnGotFocus(EventArgs eventArgs) { base.OnGotFocus(eventArgs); Invalidate(); }
        protected override void OnLostFocus(EventArgs eventArgs) { base.OnLostFocus(eventArgs); Invalidate(); }
        protected override void OnEnabledChanged(EventArgs eventArgs) { base.OnEnabledChanged(eventArgs); Invalidate(); }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            Color first = Enabled ? (_hover ? HoverStartColor : StartColor) : Color.FromArgb(45, 57, 75);
            Color second = Enabled ? (_hover ? HoverEndColor : EndColor) : Color.FromArgb(38, 49, 66);
            using (GraphicsPath path = Rounded(bounds, Radius))
            using (var gradient = new LinearGradientBrush(bounds, first, second, LinearGradientMode.Horizontal))
            using (var border = new Pen(BorderColor, 1F))
            {
                eventArgs.Graphics.FillPath(gradient, path);
                if (BorderColor.A > 0) { eventArgs.Graphics.DrawPath(border, path); }
            }
            Rectangle textBounds = CaptionAlignment == ContentAlignment.MiddleLeft ? new Rectangle(16, 0, Math.Max(1, Width - 24), Height) : bounds;
            TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis;
            flags |= CaptionAlignment == ContentAlignment.MiddleLeft ? TextFormatFlags.Left : TextFormatFlags.HorizontalCenter;
            TextRenderer.DrawText(eventArgs.Graphics, Text, Font, textBounds, Enabled ? ForeColor : Color.FromArgb(119, 134, 153), flags);
            if (Focused)
            {
                Rectangle focusBounds = new Rectangle(2, 2, Math.Max(1, Width - 5), Math.Max(1, Height - 5));
                using (GraphicsPath focusPath = Rounded(focusBounds, Math.Max(2, Radius - 2)))
                using (var focusPen = new Pen(FieldTheme.MainText, 2F)) { eventArgs.Graphics.DrawPath(focusPen, focusPath); }
            }
        }

        private static GraphicsPath Rounded(Rectangle rectangle, int radius)
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

    internal sealed class FieldSurfacePanel : Panel
    {
        public Color AccentColor { get; set; }
        public bool HeaderTreatment { get; set; }

        public FieldSurfacePanel()
        {
            DoubleBuffered = true;
            AccentColor = FieldTheme.Cyan;
            BackColor = FieldTheme.Surface;
        }

        protected override void OnPaintBackground(PaintEventArgs eventArgs)
        {
            Rectangle bounds = ClientRectangle;
            if (bounds.Width <= 0 || bounds.Height <= 0) { return; }
            if (HeaderTreatment)
            {
                using (var gradient = new LinearGradientBrush(bounds, Color.FromArgb(14, 29, 51), Color.FromArgb(8, 19, 36), LinearGradientMode.Horizontal)) { eventArgs.Graphics.FillRectangle(gradient, bounds); }
                eventArgs.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                int traceLeft = Math.Max(0, bounds.Width - 410);
                int traceRight = Math.Max(traceLeft + 24, bounds.Width - 24);
                int traceWidth = traceRight - traceLeft;
                using (var zone = new LinearGradientBrush(new Rectangle(traceLeft, 0, Math.Max(1, bounds.Width - traceLeft), bounds.Height), Color.FromArgb(8, AccentColor), Color.FromArgb(28, AccentColor), LinearGradientMode.Horizontal))
                {
                    eventArgs.Graphics.FillRectangle(zone, traceLeft, 0, bounds.Width - traceLeft, bounds.Height);
                }
                using (var line = new Pen(Color.FromArgb(48, 99, 143), 1F))
                {
                    Point[] trace =
                    {
                        new Point(traceLeft + 12, 76), new Point(traceLeft + (traceWidth / 4), 76),
                        new Point(traceLeft + (traceWidth / 4), 34), new Point(traceLeft + (traceWidth / 2), 34),
                        new Point(traceLeft + (traceWidth / 2), 64), new Point(traceLeft + ((traceWidth * 3) / 4), 64),
                        new Point(traceLeft + ((traceWidth * 3) / 4), 27), new Point(traceRight, 27)
                    };
                    eventArgs.Graphics.DrawLines(line, trace);
                    using (var node = new SolidBrush(Color.FromArgb(165, AccentColor)))
                    {
                        foreach (Point point in trace) { eventArgs.Graphics.FillRectangle(node, point.X - 2, point.Y - 2, 5, 5); }
                    }
                }
            }
            else
            {
                eventArgs.Graphics.Clear(BackColor);
            }
            using (var border = new Pen(FieldTheme.Stroke)) { eventArgs.Graphics.DrawRectangle(border, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1)); }
        }
    }

    internal sealed class UpdateStageRail : Control
    {
        private static readonly string[] StageNames = { "CONSULTA", "DOWNLOAD", "INTEGRIDADE", "INSTALAÇÃO", "REINÍCIO" };
        public int ActiveStage { get; set; }
        public int CompletedThrough { get; set; }
        public int ErrorStage { get; set; }

        public UpdateStageRail()
        {
            ActiveStage = 0;
            CompletedThrough = -1;
            ErrorStage = -1;
            Height = 96;
            BackColor = FieldTheme.Canvas;
            AccessibleRole = AccessibleRole.ProgressBar;
            AccessibleName = "Etapas da atualização";
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            eventArgs.Graphics.Clear(BackColor);
            int count = StageNames.Length;
            int left = 4;
            int gap = 10;
            int available = Math.Max(count, Width - (left * 2) - (gap * (count - 1)));
            int moduleWidth = Math.Max(1, available / count);
            int boxY = 18;
            int boxHeight = 54;
            int connectorY = boxY + (boxHeight / 2);
            using (var stageFont = new Font("Segoe UI Semibold", 7.4F))
            using (var stateFont = new Font("Consolas", 6.8F))
            using (var symbolFont = new Font("Consolas", 7.4F, FontStyle.Bold))
            {
                for (int index = 0; index < count; index++)
                {
                    int x = left + (index * (moduleWidth + gap));
                    Rectangle module = new Rectangle(x, boxY, moduleWidth, boxHeight);
                    bool completed = index <= CompletedThrough;
                    bool active = index == ActiveStage && !completed;
                    bool failed = index == ErrorStage;
                    Color color = failed ? FieldTheme.Red : completed ? FieldTheme.Green : active ? FieldTheme.Cyan : FieldTheme.MutedText;
                    Color fill = failed ? Color.FromArgb(43, 23, 34) : completed ? Color.FromArgb(10, 39, 41) : active ? FieldTheme.SurfaceRaised : Color.FromArgb(9, 18, 32);

                    if (index > 0)
                    {
                        int previousRight = x - gap;
                        Color connectorColor = completed ? FieldTheme.Green : Color.FromArgb(39, 58, 80);
                        using (var connector = new Pen(connectorColor, 3F)) { eventArgs.Graphics.DrawLine(connector, previousRight, connectorY, x, connectorY); }
                    }

                    using (var moduleFill = new SolidBrush(fill)) { eventArgs.Graphics.FillRectangle(moduleFill, module); }
                    using (var moduleBorder = new Pen(color, active || failed ? 2F : 1F)) { eventArgs.Graphics.DrawRectangle(moduleBorder, module); }
                    using (var accent = new SolidBrush(color)) { eventArgs.Graphics.FillRectangle(accent, module.X, module.Y, 4, module.Height + 1); }

                    Rectangle symbolBox = new Rectangle(module.X + 12, module.Y + 12, 30, 27);
                    using (var symbolFill = new SolidBrush(Color.FromArgb(24, color))) { eventArgs.Graphics.FillRectangle(symbolFill, symbolBox); }
                    using (var symbolBorder = new Pen(Color.FromArgb(120, color))) { eventArgs.Graphics.DrawRectangle(symbolBorder, symbolBox); }
                    string symbol = completed ? "OK" : (index + 1).ToString("00");
                    TextRenderer.DrawText(eventArgs.Graphics, symbol, symbolFont, symbolBox, color, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

                    Rectangle titleBox = new Rectangle(module.X + 51, module.Y + 8, Math.Max(1, module.Width - 58), 22);
                    Rectangle stateBox = new Rectangle(module.X + 51, module.Y + 29, Math.Max(1, module.Width - 58), 17);
                    TextRenderer.DrawText(eventArgs.Graphics, StageNames[index], stageFont, titleBox, color, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                    string state = failed ? "FALHA" : completed ? "CONCLUÍDO" : active ? "EM CURSO" : "AGUARDANDO";
                    TextRenderer.DrawText(eventArgs.Graphics, state, stateFont, stateBox, color, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                }
            }
            int activeIndex = Math.Max(0, Math.Min(count - 1, ActiveStage));
            AccessibleDescription = "Etapa atual: " + StageNames[activeIndex];
        }
    }

    internal sealed class UpdateProgressBar : Control
    {
        public bool Determinate { get; set; }
        public int ProgressValue { get; set; }
        public bool Active { get; set; }
        public int Phase { get; set; }
        public Color AccentColor { get; set; }

        public UpdateProgressBar()
        {
            Height = 8;
            AccentColor = FieldTheme.Cyan;
            AccessibleRole = AccessibleRole.ProgressBar;
            AccessibleName = "Progresso da atualização";
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs eventArgs)
        {
            eventArgs.Graphics.Clear(Color.FromArgb(25, 42, 62));
            if (!Active) { return; }
            Rectangle fill;
            if (Determinate)
            {
                int width = Math.Max(0, Math.Min(Width, Width * Math.Max(0, Math.Min(100, ProgressValue)) / 100));
                fill = new Rectangle(0, 0, width, Height);
                AccessibleDescription = ProgressValue + "% concluído";
            }
            else
            {
                int segment = Math.Max(90, Width / 4);
                int x = (Phase % Math.Max(1, Width + segment)) - segment;
                fill = new Rectangle(x, 0, segment, Height);
                AccessibleDescription = "Operação em andamento";
            }
            if (fill.Width <= 0) { return; }
            using (var gradient = new LinearGradientBrush(fill, AccentColor, FieldTheme.Blue, LinearGradientMode.Horizontal)) { eventArgs.Graphics.FillRectangle(gradient, fill); }
        }
    }

    internal sealed class UpdateExperiencePage : Panel
    {
        private readonly Label _eyebrow;
        private readonly Label _headline;
        private readonly Label _versionBadge;
        private readonly Label _statusTitle;
        private readonly Label _statusDescription;
        private readonly Label _progressLabel;
        private readonly Label _versionLine;
        private readonly Label _notes;
        private readonly Label _securityLine;
        private readonly UpdateStageRail _stageRail;
        private readonly UpdateProgressBar _progress;
        private readonly FieldActionButton _primaryButton;
        private readonly FieldActionButton _secondaryButton;
        private readonly Timer _animation;
        private Action _primaryAction;
        private Action _secondaryAction;
        private int _phase;

        public bool Busy { get; private set; }

        public UpdateExperiencePage()
        {
            Dock = DockStyle.Fill;
            BackColor = FieldTheme.Canvas;
            ForeColor = FieldTheme.MainText;
            Padding = new Padding(34, 26, 34, 24);
            Font = new Font("Segoe UI", 9F);
            AccessibleName = "Atualização do sistema";

            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, BackColor = FieldTheme.Canvas, Margin = new Padding(0), Padding = new Padding(0) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 116));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 108));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 176));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
            Controls.Add(layout);

            var header = new FieldSurfacePanel { Dock = DockStyle.Fill, HeaderTreatment = true, AccentColor = FieldTheme.Cyan, Margin = new Padding(0, 0, 0, 12), Padding = new Padding(24, 13, 20, 12) };
            var headerLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, BackColor = Color.Transparent, Margin = new Padding(0) };
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            headerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));
            headerLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            headerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _eyebrow = new Label { Text = "CANAL SEGURO  /  ATUALIZAÇÃO ASSISTIDA", Dock = DockStyle.Fill, ForeColor = FieldTheme.Cyan, Font = new Font("Segoe UI Semibold", 7.4F), TextAlign = ContentAlignment.BottomLeft };
            _headline = new Label { Text = "Atualização do sistema", Dock = DockStyle.Fill, ForeColor = FieldTheme.MainText, Font = new Font("Bahnschrift SemiCondensed", 24F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
            _versionBadge = new Label { Text = "HTTPS  +  SHA-256", Dock = DockStyle.Fill, ForeColor = FieldTheme.Green, BackColor = Color.FromArgb(9, 33, 39), Font = new Font("Segoe UI Semibold", 8F), TextAlign = ContentAlignment.MiddleCenter, Margin = new Padding(16, 14, 0, 14) };
            headerLayout.Controls.Add(_eyebrow, 0, 0);
            headerLayout.Controls.Add(_headline, 0, 1);
            headerLayout.Controls.Add(_versionBadge, 1, 0);
            headerLayout.SetRowSpan(_versionBadge, 2);
            header.Controls.Add(headerLayout);
            layout.Controls.Add(header, 0, 0);

            _stageRail = new UpdateStageRail { Dock = DockStyle.Fill, Margin = new Padding(0, 5, 0, 6) };
            layout.Controls.Add(_stageRail, 0, 1);

            var status = new FieldSurfacePanel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 12), Padding = new Padding(24, 18, 24, 14), AccentColor = FieldTheme.Cyan };
            var statusLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 4, BackColor = FieldTheme.Surface, Margin = new Padding(0) };
            statusLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            statusLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
            statusLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
            statusLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            statusLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            statusLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            _statusTitle = new Label { Text = "Preparando atualização", Dock = DockStyle.Fill, ForeColor = FieldTheme.MainText, Font = new Font("Segoe UI Semibold", 15F), TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
            _statusDescription = new Label { Text = "Aguarde enquanto o sistema prepara o canal seguro.", Dock = DockStyle.Fill, ForeColor = FieldTheme.SoftText, Font = new Font("Segoe UI", 9F), TextAlign = ContentAlignment.TopLeft, AutoEllipsis = true };
            _progressLabel = new Label { Text = "EM ANDAMENTO", Dock = DockStyle.Fill, ForeColor = FieldTheme.Cyan, Font = new Font("Consolas", 8F, FontStyle.Bold), TextAlign = ContentAlignment.MiddleRight };
            _versionLine = new Label { Text = "VERSÃO INSTALADA", Dock = DockStyle.Fill, ForeColor = FieldTheme.MutedText, Font = new Font("Consolas", 8F), TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
            _progress = new UpdateProgressBar { Dock = DockStyle.Fill, Margin = new Padding(0, 8, 0, 0), Active = true };
            statusLayout.Controls.Add(_statusTitle, 0, 0);
            statusLayout.Controls.Add(_progressLabel, 1, 0);
            statusLayout.Controls.Add(_statusDescription, 0, 1);
            statusLayout.SetColumnSpan(_statusDescription, 2);
            statusLayout.Controls.Add(_versionLine, 0, 2);
            statusLayout.SetColumnSpan(_versionLine, 2);
            statusLayout.Controls.Add(_progress, 0, 3);
            statusLayout.SetColumnSpan(_progress, 2);
            status.Controls.Add(statusLayout);
            layout.Controls.Add(status, 0, 2);

            var details = new FieldSurfacePanel { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 12), Padding = new Padding(24, 16, 24, 14), AccentColor = FieldTheme.Blue };
            var detailLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, BackColor = FieldTheme.Surface, Margin = new Padding(0) };
            detailLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66));
            detailLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34));
            detailLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            detailLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            detailLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            var detailTitle = new Label { Text = "DETALHES DA ATUALIZAÇÃO", Dock = DockStyle.Fill, ForeColor = FieldTheme.Blue, Font = new Font("Segoe UI Semibold", 7.5F), TextAlign = ContentAlignment.MiddleLeft };
            detailLayout.Controls.Add(detailTitle, 0, 0);
            detailLayout.SetColumnSpan(detailTitle, 2);
            _notes = new Label { Text = "As notas da versão aparecerão aqui.", Dock = DockStyle.Fill, ForeColor = Color.FromArgb(190, 207, 226), Font = new Font("Segoe UI", 8.7F), TextAlign = ContentAlignment.TopLeft, AutoEllipsis = true, Padding = new Padding(0, 4, 18, 0) };
            var protections = new Label { Text = "PROTEÇÕES ATIVAS\n\n[01] Origem HTTPS obrigatória\n[02] Tamanho do arquivo conferido\n[03] SHA-256 antes e depois\n[BK] Backup para recuperação", Dock = DockStyle.Fill, BackColor = FieldTheme.SurfaceRaised, ForeColor = FieldTheme.Cyan, Font = new Font("Consolas", 8.2F), TextAlign = ContentAlignment.TopLeft, Padding = new Padding(16, 13, 12, 8), Margin = new Padding(8, 0, 0, 8) };
            _securityLine = new Label { Text = "[SAFE] O pacote é validado antes de substituir o executável. A versão anterior fica disponível para recuperação.", Dock = DockStyle.Fill, ForeColor = FieldTheme.Green, Font = new Font("Segoe UI Semibold", 8F), TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
            detailLayout.Controls.Add(_notes, 0, 1);
            detailLayout.Controls.Add(protections, 1, 1);
            detailLayout.Controls.Add(_securityLine, 0, 2);
            detailLayout.SetColumnSpan(_securityLine, 2);
            details.Controls.Add(detailLayout);
            layout.Controls.Add(details, 0, 3);

            var actions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = FieldTheme.Canvas, Margin = new Padding(0), Padding = new Padding(0, 10, 0, 4) };
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
            _secondaryButton = new FieldActionButton { Text = "VOLTAR AO PAINEL", Dock = DockStyle.Fill, Margin = new Padding(0, 0, 12, 0), StartColor = FieldTheme.SurfaceRaised, EndColor = Color.FromArgb(14, 27, 46), HoverStartColor = Color.FromArgb(27, 48, 72), HoverEndColor = Color.FromArgb(20, 38, 61), BorderColor = FieldTheme.Stroke, ForeColor = FieldTheme.SoftText };
            _primaryButton = new FieldActionButton { Text = "ATUALIZAR AGORA", Dock = DockStyle.Fill, Margin = new Padding(0) };
            _secondaryButton.Click += delegate { if (_secondaryAction != null && _secondaryButton.Enabled) { _secondaryAction(); } };
            _primaryButton.Click += delegate { if (_primaryAction != null && _primaryButton.Enabled) { _primaryAction(); } };
            actions.Controls.Add(_secondaryButton, 1, 0);
            actions.Controls.Add(_primaryButton, 2, 0);
            layout.Controls.Add(actions, 0, 4);

            _animation = new Timer { Interval = 55, Enabled = true };
            _animation.Tick += delegate
            {
                if (!_progress.Active || _progress.Determinate || !SystemInformation.IsMenuAnimationEnabled) { return; }
                _phase += 13;
                _progress.Phase = _phase;
                _progress.Invalidate();
            };
            ShowChecking(new Version(0, 0, 0));
        }

        public void ShowChecking(Version current)
        {
            Busy = true;
            SetRail(0, -1, -1);
            SetProgress(false, 0, FieldTheme.Cyan, "CONECTANDO");
            _statusTitle.Text = "Buscando uma nova versão";
            _statusDescription.Text = "Consultando o canal oficial de atualizações. Nenhum arquivo será alterado nesta etapa.";
            _versionLine.Text = "VERSÃO INSTALADA  " + current.ToString(3);
            _notes.Text = "Conexão HTTPS em andamento. A disponibilidade da versão e os arquivos obrigatórios serão verificados antes do download.";
            _securityLine.ForeColor = FieldTheme.Green;
            _securityLine.Text = "[SAFE] Consulta somente leitura / sem credenciais armazenadas no aplicativo";
            HideActions();
        }

        public void ShowAvailable(Version current, Version latest, string notes, Action install, Action close)
        {
            Busy = false;
            SetRail(1, 0, -1);
            SetProgress(true, 0, FieldTheme.Cyan, "PRONTA PARA BAIXAR");
            _statusTitle.Text = "Versão " + latest.ToString(3) + " disponível";
            _statusDescription.Text = "Revise os detalhes e inicie a atualização quando estiver pronto.";
            _versionLine.Text = "VERSÃO INSTALADA  " + current.ToString(3) + "     →     NOVA VERSÃO  " + latest.ToString(3);
            _notes.Text = String.IsNullOrWhiteSpace(notes) ? "Esta versão contém melhorias de estabilidade e experiência operacional." : notes;
            _securityLine.ForeColor = FieldTheme.Green;
            _securityLine.Text = "[SAFE] Download HTTPS / validação SHA-256 / backup automático antes da instalação";
            SetActions("AGORA NÃO", close, "ATUALIZAR PARA " + latest.ToString(3), install);
        }

        public void ShowProgress(UpdateProgressInfo info, Version current, Version latest)
        {
            Busy = true;
            int stage = Math.Max(0, Math.Min(4, (int)info.Stage));
            SetRail(stage, stage - 1, -1);
            bool determinate = info.Percent >= 0;
            string progressText = determinate ? Math.Max(0, Math.Min(100, info.Percent)) + "%" : "EM ANDAMENTO";
            SetProgress(determinate, info.Percent, stage >= 3 ? FieldTheme.Purple : FieldTheme.Cyan, progressText);
            _statusTitle.Text = info.Title;
            _statusDescription.Text = info.Description;
            _versionLine.Text = "VERSÃO INSTALADA  " + current.ToString(3) + "     →     DESTINO  " + latest.ToString(3);
            _notes.Text = StageDetail((UpdateStage)stage);
            _securityLine.ForeColor = FieldTheme.Green;
            _securityLine.Text = "[SAFE] Não desligue o computador. O aplicativo será reiniciado automaticamente quando estiver seguro.";
            HideActions();
        }

        public void ShowUpToDate(Version current, Action close)
        {
            Busy = false;
            SetRail(4, 4, -1);
            SetProgress(true, 100, FieldTheme.Green, "CONCLUÍDO");
            _statusTitle.Text = "Sistema atualizado";
            _statusDescription.Text = "Você já está usando a versão mais recente disponível.";
            _versionLine.Text = "VERSÃO INSTALADA  " + current.ToString(3);
            _notes.Text = "Nenhum download é necessário. Você pode voltar ao painel e continuar a análise da rede.";
            _securityLine.ForeColor = FieldTheme.Green;
            _securityLine.Text = "[OK] Consulta concluída com sucesso";
            SetActions(null, null, "VOLTAR AO PAINEL", close);
        }

        public void ShowFailure(UpdateStage stage, string message, Action retry, Action close)
        {
            Busy = false;
            int index = Math.Max(0, Math.Min(4, (int)stage));
            SetRail(index, index - 1, index);
            SetProgress(true, 0, FieldTheme.Red, "AÇÃO NECESSÁRIA");
            _statusTitle.Text = "Atualização não concluída";
            _statusDescription.Text = message;
            _notes.Text = "A instalação atual permanece preservada. Verifique a conexão e tente novamente. Se o problema continuar, consulte o arquivo updater.log.";
            _securityLine.ForeColor = FieldTheme.Amber;
            _securityLine.Text = "[ALERTA] Nenhuma versão não validada será instalada";
            SetActions("VOLTAR AO PAINEL", close, "TENTAR NOVAMENTE", retry);
        }

        public void ShowRestarting(Version latest)
        {
            Busy = true;
            SetRail(4, 3, -1);
            SetProgress(false, 0, FieldTheme.Green, "REINICIANDO");
            _statusTitle.Text = "Atualização instalada";
            _statusDescription.Text = "A versão " + latest.ToString(3) + " foi validada. O painel será aberto novamente em instantes.";
            _versionLine.Text = "NOVA VERSÃO  " + latest.ToString(3);
            _notes.Text = "A substituição terminou com sucesso e o backup da versão anterior foi preservado para recuperação.";
            _securityLine.ForeColor = FieldTheme.Green;
            _securityLine.Text = "[OK] Integridade final confirmada";
            HideActions();
        }

        public void ShowPreview()
        {
            ShowProgress(new UpdateProgressInfo { Stage = UpdateStage.Integrity, Title = "Validando integridade do pacote", Description = "Comparando o arquivo baixado com o SHA-256 publicado.", Percent = -1 }, new Version(3, 3, 1), new Version(3, 4, 0));
            _notes.Text = "Download concluído. O sistema está verificando o hash, a versão interna e os caminhos do pacote antes de permitir a instalação.";
        }

        public void ShowCloseBlocked()
        {
            if (!Busy) { return; }
            _statusDescription.Text = "A etapa atual precisa terminar para manter a instalação consistente. Aguarde alguns instantes.";
            _securityLine.ForeColor = FieldTheme.Amber;
            _securityLine.Text = "[ALERTA] Fechamento temporariamente bloqueado durante uma etapa crítica";
        }

        public void FocusPrimary()
        {
            if (_primaryButton.Visible && _primaryButton.Enabled) { _primaryButton.Select(); }
            else if (_secondaryButton.Visible && _secondaryButton.Enabled) { _secondaryButton.Select(); }
            else { Select(); }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _animation != null) { _animation.Stop(); _animation.Dispose(); }
            base.Dispose(disposing);
        }

        private void SetActions(string secondaryText, Action secondary, string primaryText, Action primary)
        {
            _secondaryAction = secondary;
            _primaryAction = primary;
            _secondaryButton.Visible = !String.IsNullOrWhiteSpace(secondaryText);
            _secondaryButton.Enabled = _secondaryButton.Visible;
            _secondaryButton.Text = secondaryText ?? String.Empty;
            _primaryButton.Visible = !String.IsNullOrWhiteSpace(primaryText);
            _primaryButton.Enabled = _primaryButton.Visible;
            _primaryButton.Text = primaryText ?? String.Empty;
            FocusPrimary();
        }

        private void HideActions() { SetActions(null, null, null, null); }

        private void SetRail(int active, int completed, int error)
        {
            _stageRail.ActiveStage = active;
            _stageRail.CompletedThrough = completed;
            _stageRail.ErrorStage = error;
            _stageRail.AccessibleDescription = "Etapa " + (active + 1) + " de 5: " + active;
            _stageRail.Invalidate();
        }

        private void SetProgress(bool determinate, int value, Color accent, string label)
        {
            _progress.Determinate = determinate;
            _progress.ProgressValue = Math.Max(0, Math.Min(100, value));
            _progress.AccentColor = accent;
            _progress.Active = true;
            _progressLabel.Text = label;
            _progressLabel.ForeColor = accent;
            _progress.Invalidate();
        }

        private static string StageDetail(UpdateStage stage)
        {
            if (stage == UpdateStage.Download) { return "O pacote oficial está sendo transferido. O percentual representa apenas os bytes do download."; }
            if (stage == UpdateStage.Integrity) { return "O hash SHA-256, a versão interna e os caminhos do pacote estão sendo conferidos antes da instalação."; }
            if (stage == UpdateStage.Install) { return "O instalador aguarda o painel encerrar, cria um backup e substitui somente o executável validado."; }
            if (stage == UpdateStage.Restart) { return "A instalação terminou e a versão atualizada será aberta automaticamente."; }
            return "A consulta verifica a release pública mais recente sem alterar arquivos locais.";
        }
    }
}

