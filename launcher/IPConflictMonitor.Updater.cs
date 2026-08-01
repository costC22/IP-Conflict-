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

            var host = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(8, 17, 31), Padding = new Padding(0, 8, 0, 8), Margin = new Padding(0) };
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

        private static void BeginUpdateCheck(Form form, Button button)
        {
            button.Enabled = false;
            button.Text = "CONSULTANDO GITHUB...";
            Task.Factory.StartNew<UpdateCheckResult>(delegate { return CheckLatestRelease(); }).ContinueWith(delegate(Task<UpdateCheckResult> task)
            {
                if (form.IsDisposed) { return; }
                form.BeginInvoke(new Action(delegate
                {
                    if (task.IsFaulted)
                    {
                        button.Enabled = true;
                        button.Text = "↻  VERIFICAR ATUALIZAÇÃO";
                        MessageBox.Show(form, FriendlyError(task.Exception), "Atualização", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    UpdateCheckResult result = task.Result;
                    if (!result.UpdateAvailable)
                    {
                        button.Enabled = true;
                        button.Text = "✓  SISTEMA ATUALIZADO";
                        MessageBox.Show(form, "Você já está usando a versão mais recente (" + result.CurrentVersion.ToString(3) + ").", "Atualização", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    string notes = CleanReleaseNotes(result.Release.body);
                    string message = "Nova versão disponível: " + result.LatestVersion.ToString(3) + "\nVersão instalada: " + result.CurrentVersion.ToString(3) + "\n\n" + notes + "\n\nBaixar e instalar agora?";
                    if (MessageBox.Show(form, message, "Atualização disponível", MessageBoxButtons.YesNo, MessageBoxIcon.Information) != DialogResult.Yes)
                    {
                        button.Enabled = true;
                        button.Text = "↻  VERIFICAR ATUALIZAÇÃO";
                        return;
                    }
                    BeginDownload(form, button, result);
                }));
            });
        }

        private static void BeginDownload(Form form, Button button, UpdateCheckResult result)
        {
            button.Text = "BAIXANDO E VALIDANDO...";
            Task.Factory.StartNew(delegate { return PrepareUpdate(result); }).ContinueWith(delegate(Task<PreparedUpdate> task)
            {
                if (form.IsDisposed) { return; }
                form.BeginInvoke(new Action(delegate
                {
                    if (task.IsFaulted)
                    {
                        button.Enabled = true;
                        button.Text = "↻  VERIFICAR ATUALIZAÇÃO";
                        MessageBox.Show(form, FriendlyError(task.Exception), "Falha na atualização", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }
                    PreparedUpdate prepared = task.Result;
                    try
                    {
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
                        if (process == null) { throw new InvalidOperationException("Não foi possível iniciar o aplicador da atualização."); }
                        button.Text = "REINICIANDO...";
                        Application.Exit();
                    }
                    catch (Exception exception)
                    {
                        button.Enabled = true;
                        button.Text = "↻  VERIFICAR ATUALIZAÇÃO";
                        MessageBox.Show(form, exception.Message, "Falha na atualização", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }));
            });
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
            DownloadFile(package.browser_download_url, zipPath);
            DownloadFile(hashAsset.browser_download_url, hashPath);
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
            return new PreparedUpdate { ExecutablePath = executable, ExpectedHash = expectedHash, Version = downloadedVersion };
        }

        public static int ApplyUpdate(string[] args)
        {
            string target = GetOption(args, "-Target");
            string expectedHash = GetOption(args, "-ExpectedHash");
            int parentPid;
            Int32.TryParse(GetOption(args, "-ParentPid"), out parentPid);
            bool restart = HasArgument(args, "-Restart");
            try
            {
                if (String.IsNullOrWhiteSpace(target) || !String.Equals(Path.GetFileName(target), "IPConflictMonitor.exe", StringComparison.OrdinalIgnoreCase)) { throw new InvalidOperationException("Destino de atualização inválido."); }
                target = Path.GetFullPath(target);
                string source = Path.GetFullPath(Assembly.GetExecutingAssembly().Location);
                if (String.Equals(source, target, StringComparison.OrdinalIgnoreCase)) { throw new InvalidOperationException("Origem e destino da atualização são iguais."); }
                if (!String.Equals(ComputeSha256(source), expectedHash, StringComparison.OrdinalIgnoreCase)) { throw new InvalidDataException("O aplicador não corresponde ao hash esperado."); }
                WaitForProcess(parentPid, 45000);

                string backup = target + ".previous";
                if (File.Exists(target)) { File.Copy(target, backup, true); }
                try
                {
                    File.Copy(source, target, true);
                    if (!String.Equals(ComputeSha256(target), expectedHash, StringComparison.OrdinalIgnoreCase)) { throw new InvalidDataException("O arquivo atualizado não passou na validação final."); }
                }
                catch
                {
                    if (File.Exists(backup)) { File.Copy(backup, target, true); }
                    throw;
                }
                WriteUpdateLog("Atualização aplicada com sucesso em " + target + ".");
                if (restart) { Process.Start(new ProcessStartInfo { FileName = target, UseShellExecute = true, WorkingDirectory = Path.GetDirectoryName(target) }); }
                return 0;
            }
            catch (Exception exception)
            {
                WriteUpdateLog("Falha: " + exception);
                MessageBox.Show("Não foi possível aplicar a atualização.\n\n" + exception.Message, "IPConflictMonitor", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
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
            request.UserAgent = "IPConflictMonitor-Updater/3.1";
            request.Accept = "application/vnd.github+json";
            request.Timeout = 12000;
            request.ReadWriteTimeout = 12000;
            using (WebResponse response = request.GetResponse())
            using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8)) { return reader.ReadToEnd(); }
        }

        private static void DownloadFile(string url, string destination)
        {
            using (var client = new WebClient())
            {
                client.Headers[HttpRequestHeader.UserAgent] = "IPConflictMonitor-Updater/3.1";
                client.DownloadFile(url, destination);
            }
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

        private static string FriendlyError(AggregateException exception)
        {
            Exception error = exception == null ? null : exception.Flatten().InnerExceptions.FirstOrDefault();
            if (error is WebException) { return "Não foi possível consultar as releases no GitHub. Verifique a internet e tente novamente.\n\n" + error.Message; }
            return error == null ? "Falha desconhecida ao verificar atualização." : error.Message;
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


