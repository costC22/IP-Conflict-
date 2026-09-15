using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace IPConflictMonitor.Launcher
{
    internal sealed class GitHubReleaseAsset
    {
        public string name { get; set; }
        public string browser_download_url { get; set; }
        public long size { get; set; }
    }

    internal sealed class GitHubRelease
    {
        public string tag_name { get; set; }
        public string name { get; set; }
        public string body { get; set; }
        public string html_url { get; set; }
        public bool draft { get; set; }
        public bool prerelease { get; set; }
        public List<GitHubReleaseAsset> assets { get; set; }
    }

    internal sealed class UpdateCheckResult
    {
        public Version CurrentVersion;
        public Version LatestVersion;
        public GitHubRelease Release;
        public bool UpdateAvailable;
    }

    internal sealed class PreparedUpdate
    {
        public string ExecutablePath;
        public string ExpectedHash;
        public Version Version;
    }

    internal static class UpdateCoordinator
    {
        private const string RepositoryOwner = "costC22";
        private const string RepositoryName = "IP-Conflict-";
        private const string ReleaseApi = "https://api.github.com/repos/costC22/IP-Conflict-/releases/latest";
        private const string PackageAssetName = "IPConflictMonitor-Windows.zip";
        private const string HashAssetName = "IPConflictMonitor.exe.sha256";
        private static readonly JavaScriptSerializer Json = new JavaScriptSerializer { MaxJsonLength = 1024 * 1024 };
        private static bool _hookRegistered;
        private static bool _interfaceAttached;

        public static void RegisterInterfaceHook()
        {
            if (_hookRegistered) { return; }
            _hookRegistered = true;
            Application.Idle += AttachUpdateInterface;
        }

        private static void AttachUpdateInterface(object sender, EventArgs eventArgs)
        {
            if (_interfaceAttached || Application.OpenForms.Count == 0) { return; }
            AttachToForm(Application.OpenForms[0]);
        }

        internal static void AttachToForm(Form form)
        {
            if (_interfaceAttached || form == null) { return; }
            TableLayoutPanel sidebar = FindSidebar(form);
            if (sidebar == null) { return; }
            _interfaceAttached = true;
            Application.Idle -= AttachUpdateInterface;

            var host = new Panel { Dock = DockStyle.Fill, BackColor = FieldTheme.Sidebar, Padding = new Padding(0, 8, 0, 8), Margin = new Padding(0) };
            var layout = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 74, ColumnCount = 1, RowCount = 2, BackColor = host.BackColor, Margin = new Padding(0) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            var button = new Button
            {
                Text = "↻  VERIFICAR ATUALIZAÇÃO",
                Dock = DockStyle.Fill,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(16, 48, 66),
                ForeColor = Color.FromArgb(43, 213, 237),
                Font = new Font("Segoe UI Semibold", 7.7F),
                Cursor = Cursors.Hand,
                Margin = new Padding(0, 2, 0, 4)
            };
            button.FlatAppearance.BorderColor = Color.FromArgb(38, 108, 123);
            var version = new Label
            {
                Text = "VERSÃO INSTALADA  " + GetCurrentVersion().ToString(3),
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(86, 116, 146),
                Font = new Font("Segoe UI Semibold", 6.8F),
                TextAlign = ContentAlignment.MiddleCenter
            };
            layout.Controls.Add(button, 0, 0);
            layout.Controls.Add(version, 0, 1);
            host.Controls.Add(layout);
            sidebar.Controls.Add(host, 0, 6);
            button.Click += delegate { BeginUpdateCheck(form, button); };
        }

        private static TableLayoutPanel FindSidebar(Control root)
        {
            foreach (Control control in root.Controls)
            {
                var table = control as TableLayoutPanel;
                if (table != null && table.ColumnCount == 1 && table.RowCount == 9) { return table; }
                TableLayoutPanel nested = FindSidebar(control);
                if (nested != null) { return nested; }
            }
            return null;
        }

        private sealed class UpdatePageSession
        {
            public Form Form;
            public Button Trigger;
            public TableLayoutPanel Sidebar;
            public TableLayoutPanel Shell;
            public Control PreviousContent;
            public UpdateExperiencePage Page;
            public FormClosingEventHandler CloseGuard;
            public bool AllowFormClose;
            public UpdateStage CurrentStage;
        }

        private static void BeginUpdateCheck(Form form, Button button)
        {
            UpdatePageSession session;
            try
            {
                session = OpenUpdatePage(form, button);
                StartUpdateCheck(session);
            }
            catch (Exception exception)
            {
                button.Enabled = true;
                button.Text = "↻  VERIFICAR ATUALIZAÇÃO";
                WriteUpdateLog("Falha ao abrir a experiência de atualização: " + exception);
            }
        }

        private static TableLayoutPanel FindMainShell(Control sidebar)
        {
            Control current = sidebar;
            while (current != null)
            {
                TableLayoutPanel table = current as TableLayoutPanel;
                if (table != null && table.ColumnCount >= 2 && table.GetControlFromPosition(1, 0) != null) { return table; }
                current = current.Parent;
            }
            return null;
        }
        private static UpdatePageSession OpenUpdatePage(Form form, Button button)
        {
            TableLayoutPanel sidebar = FindSidebar(form);
            TableLayoutPanel shell = FindMainShell(sidebar);
            Control previous = shell == null ? null : shell.GetControlFromPosition(1, 0);
            if (sidebar == null || shell == null || previous == null) { throw new InvalidOperationException("A área principal do painel não foi encontrada."); }

            var session = new UpdatePageSession
            {
                Form = form,
                Trigger = button,
                Sidebar = sidebar,
                Shell = shell,
                PreviousContent = previous,
                Page = new UpdateExperiencePage(),
                CurrentStage = UpdateStage.Check,
            };
            session.CloseGuard = delegate(object sender, FormClosingEventArgs eventArgs)
            {
                if (!session.AllowFormClose && session.Page != null && session.Page.Busy && eventArgs.CloseReason == CloseReason.UserClosing)
                {
                    eventArgs.Cancel = true;
                    session.Page.ShowCloseBlocked();
                }
            };

            if (button != null)
            {
                button.Enabled = false;
                button.Text = "ATUALIZAÇÃO ABERTA";
            }
            previous.Visible = false;
            shell.Controls.Add(session.Page, 1, 0);
            session.Page.BringToFront();
            form.FormClosing += session.CloseGuard;
            session.Page.Select();
            return session;
        }

        private static void CloseUpdatePage(UpdatePageSession session)
        {
            if (session == null || session.Form == null || session.Form.IsDisposed) { return; }
            session.Form.FormClosing -= session.CloseGuard;
            if (session.Page != null)
            {
                session.Shell.Controls.Remove(session.Page);
                session.Page.Dispose();
                session.Page = null;
            }
            session.PreviousContent.Visible = true;
            if (session.Trigger != null)
            {
                session.Trigger.Enabled = true;
                session.Trigger.Text = "↻  VERIFICAR ATUALIZAÇÃO";
                session.Trigger.Select();
            }
        }

        private static void StartUpdateCheck(UpdatePageSession session)
        {
            if (session == null || session.Page == null) { return; }
            session.CurrentStage = UpdateStage.Check;
            session.Page.ShowChecking(GetCurrentVersion());
            Task.Factory.StartNew<UpdateCheckResult>(delegate { return CheckLatestRelease(); }).ContinueWith(delegate(Task<UpdateCheckResult> task)
            {
                if (session.Form.IsDisposed || session.Page == null || session.Page.IsDisposed) { return; }
                session.Form.BeginInvoke(new Action(delegate
                {
                    if (task.IsFaulted)
                    {
                        session.Page.ShowFailure(UpdateStage.Check, FriendlyError(task.Exception), delegate { StartUpdateCheck(session); }, delegate { CloseUpdatePage(session); });
                        return;
                    }
                    UpdateCheckResult result = task.Result;
                    if (!result.UpdateAvailable)
                    {
                        if (session.Trigger != null) { session.Trigger.Text = "✓  SISTEMA ATUALIZADO"; }
                        session.Page.ShowUpToDate(result.CurrentVersion, delegate { CloseUpdatePage(session); });
                        return;
                    }
                    string notes = CleanReleaseNotes(result.Release.body);
                    session.Page.ShowAvailable(result.CurrentVersion, result.LatestVersion, notes, delegate { BeginDownload(session, result); }, delegate { CloseUpdatePage(session); });
                }));
            });
        }

        private static void BeginDownload(UpdatePageSession session, UpdateCheckResult result)
        {
            session.CurrentStage = UpdateStage.Download;
            session.Page.ShowProgress(new UpdateProgressInfo { Stage = UpdateStage.Download, Title = "Baixando pacote oficial", Description = "Transferindo os arquivos da versão " + result.LatestVersion.ToString(3) + ".", Percent = 0 }, result.CurrentVersion, result.LatestVersion);
            Action<UpdateProgressInfo> report = delegate(UpdateProgressInfo info)
            {
                session.CurrentStage = info.Stage;
                if (session.Form.IsDisposed || session.Page == null || session.Page.IsDisposed) { return; }
                try
                {
                    session.Form.BeginInvoke(new Action(delegate
                    {
                        if (session.Page != null && !session.Page.IsDisposed) { session.Page.ShowProgress(info, result.CurrentVersion, result.LatestVersion); }
                    }));
                }
                catch (InvalidOperationException) { }
            };

            Task.Factory.StartNew(delegate { return PrepareUpdate(result, report); }).ContinueWith(delegate(Task<PreparedUpdate> task)
            {
                if (session.Form.IsDisposed || session.Page == null || session.Page.IsDisposed) { return; }
                session.Form.BeginInvoke(new Action(delegate
                {
                    if (task.IsFaulted)
                    {
                        session.Page.ShowFailure(session.CurrentStage, FriendlyError(task.Exception), delegate { BeginDownload(session, result); }, delegate { CloseUpdatePage(session); });
                        return;
                    }
                    PreparedUpdate prepared = task.Result;
                    try
                    {
                        session.CurrentStage = UpdateStage.Install;
                        session.Page.ShowProgress(new UpdateProgressInfo { Stage = UpdateStage.Install, Title = "Transferindo para o instalador seguro", Description = "O painel será fechado somente depois que o instalador estiver visível.", Percent = -1 }, result.CurrentVersion, result.LatestVersion);
                        string current = Assembly.GetExecutingAssembly().Location;
                        var info = new ProcessStartInfo
                        {
                            FileName = prepared.ExecutablePath,
                            Arguments = "-ApplyUpdate -Target " + Quote(current) + " -ParentPid " + Process.GetCurrentProcess().Id + " -ExpectedHash " + prepared.ExpectedHash + " -Restart",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            WorkingDirectory = Path.GetDirectoryName(prepared.ExecutablePath)
                        };
                        Process process = Process.Start(info);
                        if (process == null) { throw new InvalidOperationException("Não foi possível iniciar o instalador da atualização."); }
                        session.AllowFormClose = true;
                        session.Form.FormClosing -= session.CloseGuard;
                        if (session.Trigger != null) { session.Trigger.Text = "REINICIANDO..."; }
                        Application.Exit();
                    }
                    catch (Exception exception)
                    {
                        WriteUpdateLog("Falha ao iniciar o instalador: " + exception);
                        session.Page.ShowFailure(UpdateStage.Install, FriendlyError(exception), delegate { BeginDownload(session, result); }, delegate { CloseUpdatePage(session); });
                    }
                }));
            });
        }
        public static int CaptureUpdateInterface(string outputPath)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (String.IsNullOrWhiteSpace(outputPath)) { outputPath = Path.Combine(Path.GetTempPath(), "IPConflictMonitor-update.png"); }
            outputPath = Path.GetFullPath(outputPath);
            string directory = Path.GetDirectoryName(outputPath);
            if (!String.IsNullOrWhiteSpace(directory)) { Directory.CreateDirectory(directory); }
            using (var form = new Program.NetworkOperationsForm(true))
            {
                form.StartPosition = FormStartPosition.Manual;
                form.Location = new Point(20, 20);
                form.Show();
                Application.DoEvents();
                AttachToForm(form);
                form.PreparePreview();
                Application.DoEvents();
                UpdatePageSession session = OpenUpdatePage(form, null);
                session.Page.ShowPreview();
                Application.DoEvents();
                using (var bitmap = new Bitmap(form.Width, form.Height))
                {
                    form.DrawToBitmap(bitmap, new Rectangle(0, 0, bitmap.Width, bitmap.Height));
                    bitmap.Save(outputPath, System.Drawing.Imaging.ImageFormat.Png);
                }
                session.AllowFormClose = true;
                form.FormClosing -= session.CloseGuard;
                form.Close();
            }
            return 0;
        }
        public static UpdateCheckResult CheckLatestRelease()
        {
            EnableModernTls();
            string response = DownloadText(ReleaseApi);
            GitHubRelease release = Json.Deserialize<GitHubRelease>(response);
            if (release == null || release.draft || String.IsNullOrWhiteSpace(release.tag_name)) { throw new InvalidDataException("O GitHub não retornou uma release publicada válida."); }
            Version latest = ParseVersion(release.tag_name);
            Version current = GetCurrentVersion();
            return new UpdateCheckResult { CurrentVersion = current, LatestVersion = latest, Release = release, UpdateAvailable = latest > current };
        }

        public static int CheckFromCommandLine()
        {
            UpdateCheckResult result = CheckLatestRelease();
            Console.WriteLine("Versão instalada: " + result.CurrentVersion.ToString(3));
            Console.WriteLine("Versão disponível: " + result.LatestVersion.ToString(3));
            Console.WriteLine(result.UpdateAvailable ? "Atualização disponível." : "Sistema atualizado.");
            return result.UpdateAvailable ? 10 : 0;
        }

        private static PreparedUpdate PrepareUpdate(UpdateCheckResult result)
        {
            return PrepareUpdate(result, null);
        }

        private static PreparedUpdate PrepareUpdate(UpdateCheckResult result, Action<UpdateProgressInfo> report)
        {
            GitHubReleaseAsset package = FindAsset(result.Release, PackageAssetName);
            GitHubReleaseAsset hashAsset = FindAsset(result.Release, HashAssetName);
            if (package == null || hashAsset == null) { throw new InvalidDataException("A release não contém o pacote e o arquivo SHA-256 obrigatórios."); }
            ValidateGitHubDownloadUrl(package.browser_download_url);
            ValidateGitHubDownloadUrl(hashAsset.browser_download_url);

            string versionFolder = "v" + result.LatestVersion.ToString(3);
            string updateRoot = Path.Combine(Path.GetTempPath(), "IPConflictMonitor", "updates", versionFolder);
            if (Directory.Exists(updateRoot)) { Directory.Delete(updateRoot, true); }
            Directory.CreateDirectory(updateRoot);
            string zipPath = Path.Combine(updateRoot, PackageAssetName);
            string hashPath = Path.Combine(updateRoot, HashAssetName);

            Report(report, UpdateStage.Download, "Baixando pacote oficial", "Transferindo os arquivos da versão " + result.LatestVersion.ToString(3) + ".", 0);
            DownloadFile(package.browser_download_url, zipPath, package.size, delegate(int percent)
            {
                Report(report, UpdateStage.Download, "Baixando pacote oficial", "Transferindo os arquivos da versão " + result.LatestVersion.ToString(3) + ".", percent);
            });
            DownloadFile(hashAsset.browser_download_url, hashPath, hashAsset.size, null);

            Report(report, UpdateStage.Integrity, "Validando integridade do pacote", "Comparando o executável baixado com o SHA-256 publicado.", -1);
            string expectedHash = ReadExpectedHash(hashPath);
            string extractPath = Path.Combine(updateRoot, "package");
            Directory.CreateDirectory(extractPath);
            ExtractZipSafely(zipPath, extractPath);
            string executable = Path.Combine(extractPath, "IPConflictMonitor.exe");
            if (!File.Exists(executable)) { throw new InvalidDataException("O pacote baixado não contém IPConflictMonitor.exe."); }
            string actualHash = ComputeSha256(executable);
            if (!String.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase)) { throw new InvalidDataException("A validação SHA-256 falhou. A atualização foi cancelada."); }
            Version downloadedVersion = ParseVersion(FileVersionInfo.GetVersionInfo(executable).FileVersion);
            if (downloadedVersion != result.LatestVersion) { throw new InvalidDataException("A versão do EXE não corresponde à release publicada."); }
            Report(report, UpdateStage.Install, "Pacote validado", "A instalação segura está pronta para começar.", 100);
            return new PreparedUpdate { ExecutablePath = executable, ExpectedHash = expectedHash, Version = downloadedVersion };
        }
        public static int ApplyUpdate(string[] args)
        {
            bool restart = HasArgument(args, "-Restart");
            bool graphical = restart && !HasArgument(args, "-SilentUpdate") && Environment.UserInteractive;
            if (graphical) { return ApplyUpdateWithInterface(args); }
            try
            {
                string target = ApplyUpdateFiles(args, null);
                if (restart) { RestartTarget(target); }
                return 0;
            }
            catch (Exception exception)
            {
                WriteUpdateLog("Falha: " + exception);
                Console.Error.WriteLine("Não foi possível aplicar a atualização: " + exception.Message);
                return 1;
            }
        }

        private static int ApplyUpdateWithInterface(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Version targetVersion = ParseVersion(FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion);
            int resultCode = 1;
            bool allowClose = false;
            using (var form = new Form
            {
                Text = "IPConflictMonitor — Atualização do sistema",
                Icon = SystemIcons.Shield,
                BackColor = FieldTheme.Canvas,
                ForeColor = FieldTheme.MainText,
                Font = new Font("Segoe UI", 9F),
                AutoScaleMode = AutoScaleMode.Dpi,
                MinimumSize = new Size(860, 600),
                Size = new Size(960, 680),
                StartPosition = FormStartPosition.CenterScreen,
                FormBorderStyle = FormBorderStyle.FixedSingle,
                MaximizeBox = false,
            })
            {
                var page = new UpdateExperiencePage();
                form.Controls.Add(page);
                Action beginInstall = null;
                Action restartTarget = null;

                restartTarget = delegate
                {
                    try
                    {
                        string target = GetOption(args, "-Target");
                        RestartTarget(Path.GetFullPath(target));
                        resultCode = 0;
                        allowClose = true;
                        form.Close();
                    }
                    catch (Exception exception)
                    {
                        WriteUpdateLog("Falha ao reiniciar: " + exception);
                        page.ShowFailure(UpdateStage.Restart, FriendlyError(exception), restartTarget, delegate { allowClose = true; form.Close(); });
                    }
                };

                beginInstall = delegate
                {
                    page.ShowProgress(new UpdateProgressInfo { Stage = UpdateStage.Install, Title = "Aguardando o painel encerrar", Description = "O instalador está assumindo o controle com segurança.", Percent = -1 }, GetInstalledTargetVersion(args), targetVersion);
                    Action<UpdateProgressInfo> report = delegate(UpdateProgressInfo info)
                    {
                        if (form.IsDisposed) { return; }
                        try { form.BeginInvoke(new Action(delegate { if (!page.IsDisposed) { page.ShowProgress(info, GetInstalledTargetVersion(args), targetVersion); } })); }
                        catch (InvalidOperationException) { }
                    };
                    Task.Factory.StartNew(delegate { return ApplyUpdateFiles(args, report); }).ContinueWith(delegate(Task<string> task)
                    {
                        if (form.IsDisposed) { return; }
                        form.BeginInvoke(new Action(delegate
                        {
                            if (task.IsFaulted)
                            {
                                page.ShowFailure(UpdateStage.Install, FriendlyError(task.Exception), beginInstall, delegate { allowClose = true; form.Close(); });
                                return;
                            }
                            page.ShowRestarting(targetVersion);
                            var timer = new System.Windows.Forms.Timer { Interval = 1100 };
                            timer.Tick += delegate
                            {
                                timer.Stop();
                                timer.Dispose();
                                restartTarget();
                            };
                            timer.Start();
                        }));
                    });
                };

                form.FormClosing += delegate(object sender, FormClosingEventArgs eventArgs)
                {
                    if (!allowClose && page.Busy && eventArgs.CloseReason == CloseReason.UserClosing)
                    {
                        eventArgs.Cancel = true;
                        page.ShowCloseBlocked();
                    }
                };
                form.Shown += delegate { beginInstall(); };
                Application.Run(form);
            }
            return resultCode;
        }

        private static string ApplyUpdateFiles(string[] args, Action<UpdateProgressInfo> report)
        {
            string target = GetOption(args, "-Target");
            string expectedHash = GetOption(args, "-ExpectedHash");
            int parentPid;
            Int32.TryParse(GetOption(args, "-ParentPid"), out parentPid);
            if (String.IsNullOrWhiteSpace(target) || !String.Equals(Path.GetFileName(target), "IPConflictMonitor.exe", StringComparison.OrdinalIgnoreCase)) { throw new InvalidOperationException("Destino de atualização inválido."); }
            target = Path.GetFullPath(target);
            string source = Path.GetFullPath(Assembly.GetExecutingAssembly().Location);
            if (String.Equals(source, target, StringComparison.OrdinalIgnoreCase)) { throw new InvalidOperationException("Origem e destino da atualização são iguais."); }
            if (String.IsNullOrWhiteSpace(expectedHash) || !String.Equals(ComputeSha256(source), expectedHash, StringComparison.OrdinalIgnoreCase)) { throw new InvalidDataException("O instalador não corresponde ao hash esperado."); }

            Report(report, UpdateStage.Install, "Aguardando o painel encerrar", "A instalação começará assim que o arquivo em uso for liberado.", -1);
            WaitForProcess(parentPid, 45000);
            if (File.Exists(target) && String.Equals(ComputeSha256(target), expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                WriteUpdateLog("A versão validada já estava instalada em " + target + ".");
                return target;
            }

            string backup = target + ".previous";
            Report(report, UpdateStage.Install, "Criando ponto de recuperação", "Preservando a versão anterior antes da substituição.", -1);
            if (File.Exists(target)) { File.Copy(target, backup, true); }
            try
            {
                Report(report, UpdateStage.Install, "Instalando a nova versão", "Substituindo somente o executável pelo arquivo já validado.", -1);
                File.Copy(source, target, true);
                Report(report, UpdateStage.Install, "Validando a instalação", "Confirmando novamente a integridade do executável instalado.", -1);
                if (!String.Equals(ComputeSha256(target), expectedHash, StringComparison.OrdinalIgnoreCase)) { throw new InvalidDataException("O arquivo atualizado não passou na validação final."); }
            }
            catch
            {
                if (File.Exists(backup)) { File.Copy(backup, target, true); }
                throw;
            }
            WriteUpdateLog("Atualização aplicada com sucesso em " + target + ".");
            return target;
        }

        private static Version GetInstalledTargetVersion(string[] args)
        {
            try
            {
                string target = GetOption(args, "-Target");
                if (!String.IsNullOrWhiteSpace(target) && File.Exists(target)) { return ParseVersion(FileVersionInfo.GetVersionInfo(target).FileVersion); }
            }
            catch { }
            return new Version(0, 0, 0);
        }

        private static void RestartTarget(string target)
        {
            Process process = Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(target) });
            if (process == null) { throw new InvalidOperationException("A versão atualizada foi instalada, mas não pôde ser aberta automaticamente."); }
        }
        private static void ExtractZipSafely(string zipPath, string destination)
        {
            string root = Path.GetFullPath(destination).TrimEnd('\\') + "\\";
            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    string output = Path.GetFullPath(Path.Combine(destination, entry.FullName));
                    if (!output.StartsWith(root, StringComparison.OrdinalIgnoreCase)) { throw new InvalidDataException("O pacote contém um caminho inválido."); }
                    if (String.IsNullOrEmpty(entry.Name)) { Directory.CreateDirectory(output); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(output));
                    entry.ExtractToFile(output, true);
                }
            }
        }

        private static GitHubReleaseAsset FindAsset(GitHubRelease release, string name)
        {
            return (release.assets ?? new List<GitHubReleaseAsset>()).FirstOrDefault(delegate(GitHubReleaseAsset asset) { return String.Equals(asset.name, name, StringComparison.OrdinalIgnoreCase); });
        }

        private static void ValidateGitHubDownloadUrl(string value)
        {
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri) || uri.Scheme != Uri.UriSchemeHttps || !String.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)) { throw new InvalidDataException("A release retornou um endereço de download não confiável."); }
        }

        private static string DownloadText(string url)
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Method = "GET";
            request.UserAgent = "IPConflictMonitor-Updater/3.3.1";
            request.Accept = "application/vnd.github+json";
            request.Timeout = 12000;
            request.ReadWriteTimeout = 12000;
            using (WebResponse response = request.GetResponse())
            using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8)) { return reader.ReadToEnd(); }
        }

        private static void DownloadFile(string url, string destination, long expectedSize, Action<int> progress)
        {
            string partial = destination + ".part";
            if (File.Exists(partial)) { File.Delete(partial); }
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(url);
                request.Method = "GET";
                request.UserAgent = "IPConflictMonitor-Updater/3.3.1";
                request.Timeout = 30000;
                request.ReadWriteTimeout = 30000;
                using (WebResponse response = request.GetResponse())
                using (Stream input = response.GetResponseStream())
                using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    if (input == null) { throw new IOException("A resposta de download não contém dados."); }
                    long total = expectedSize > 0 ? expectedSize : response.ContentLength;
                    long received = 0;
                    int lastPercent = -1;
                    var buffer = new byte[81920];
                    int read;
                    while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        output.Write(buffer, 0, read);
                        received += read;
                        if (progress != null && total > 0)
                        {
                            int percent = (int)Math.Max(0, Math.Min(100, received * 100L / total));
                            if (percent != lastPercent) { lastPercent = percent; progress(percent); }
                        }
                    }
                    output.Flush(true);
                    if (expectedSize > 0 && received != expectedSize) { throw new InvalidDataException("O tamanho do arquivo baixado não corresponde à release publicada."); }
                }
                File.Move(partial, destination);
                if (progress != null) { progress(100); }
            }
            catch
            {
                if (File.Exists(partial)) { File.Delete(partial); }
                throw;
            }
        }

        private static void Report(Action<UpdateProgressInfo> report, UpdateStage stage, string title, string description, int percent)
        {
            if (report != null) { report(new UpdateProgressInfo { Stage = stage, Title = title, Description = description, Percent = percent }); }
        }

        private static Version GetCurrentVersion() { return Assembly.GetExecutingAssembly().GetName().Version; }

        private static Version ParseVersion(string value)
        {
            string clean = (value ?? String.Empty).Trim();
            if (clean.StartsWith("v", StringComparison.OrdinalIgnoreCase)) { clean = clean.Substring(1); }
            int dash = clean.IndexOf('-'); if (dash >= 0) { clean = clean.Substring(0, dash); }
            Version parsed;
            if (!Version.TryParse(clean, out parsed)) { throw new InvalidDataException("Versão inválida: " + value); }
            if (parsed.Build < 0) { parsed = new Version(parsed.Major, parsed.Minor, 0); }
            return new Version(parsed.Major, parsed.Minor, parsed.Build);
        }

        private static string ReadExpectedHash(string path)
        {
            string text = File.ReadAllText(path, Encoding.ASCII).Trim();
            string token = text.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (token == null || token.Length != 64 || token.Any(delegate(char character) { return !Uri.IsHexDigit(character); })) { throw new InvalidDataException("O arquivo SHA-256 da release é inválido."); }
            return token.ToUpperInvariant();
        }

        private static string ComputeSha256(string path)
        {
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path)) { return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", String.Empty); }
        }

        private static void WaitForProcess(int processId, int timeout)
        {
            if (processId <= 0) { return; }
            try { using (Process process = Process.GetProcessById(processId)) { process.WaitForExit(timeout); } }
            catch (ArgumentException) { }
        }

        private static string GetOption(string[] args, string name)
        {
            for (int index = 0; index < args.Length; index++) { if (String.Equals(args[index], name, StringComparison.OrdinalIgnoreCase)) { return index + 1 < args.Length ? args[index + 1] : null; } }
            return null;
        }

        private static bool HasArgument(string[] args, string name) { return args.Any(delegate(string value) { return String.Equals(value, name, StringComparison.OrdinalIgnoreCase); }); }
        private static string Quote(string value) { return "\"" + (value ?? String.Empty).Replace("\"", "\\\"") + "\""; }

        private static string CleanReleaseNotes(string value)
        {
            string text = String.IsNullOrWhiteSpace(value) ? "A release contém melhorias e correções." : value.Replace("#", String.Empty).Replace("*", String.Empty).Trim();
            if (text.Length > 500) { text = text.Substring(0, 500) + "..."; }
            return text;
        }

        private static string FriendlyError(Exception exception)
        {
            Exception error = exception;
            AggregateException aggregate = exception as AggregateException;
            if (aggregate != null) { error = aggregate.Flatten().InnerExceptions.FirstOrDefault(); }
            while (error is TargetInvocationException && error.InnerException != null) { error = error.InnerException; }
            if (error is WebException) { return "Não foi possível acessar o canal de atualizações. Verifique a internet e tente novamente."; }
            string message = error == null ? "Falha desconhecida ao verificar a atualização." : error.Message;
            return String.IsNullOrWhiteSpace(message) ? "A atualização não pôde ser concluída." : message;
        }
        private static void EnableModernTls()
        {
            try { ServicePointManager.SecurityProtocol |= (SecurityProtocolType)3072; } catch { }
        }

        private static void WriteUpdateLog(string message)
        {
            try
            {
                string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IPConflictMonitor", "logs");
                Directory.CreateDirectory(directory);
                File.AppendAllText(Path.Combine(directory, "updater.log"), DateTime.Now.ToString("s") + " " + message + Environment.NewLine, new UTF8Encoding(false));
            }
            catch { }
        }
    }
}







