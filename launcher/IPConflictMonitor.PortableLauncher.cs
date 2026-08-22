using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

[assembly: AssemblyTitle("IPConflictMonitor")]
[assembly: AssemblyDescription("Strict Evidence Detection para conflitos IPv4/MAC")]
[assembly: AssemblyCompany("IPConflictMonitor")]
[assembly: AssemblyProduct("IPConflictMonitor Field Edition")]
[assembly: AssemblyCopyright("Copyright (c) 2026")]
[assembly: AssemblyVersion("3.2.2.0")]
[assembly: AssemblyFileVersion("3.2.2.0")]

namespace IPConflictMonitor.Launcher
{
    internal static partial class Program
    {
        private const string ConfigResource = "IPConflictMonitor.DefaultConfig.json";
        private const string ProductDirectoryName = "IPConflictMonitor";

        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                if (HasSwitch(args, "-SelfTestDetection", "--self-test-detection")) { return StrictEvidenceSelfTests.Run(Console.Out); }
                if (HasSwitch(args, "-ApplyUpdate")) { return UpdateCoordinator.ApplyUpdate(args); }
                if (HasSwitch(args, "-CheckUpdate")) { return UpdateCoordinator.CheckFromCommandLine(); }
                if (args.Length == 0 || HasSwitch(args, "-Gui", "--gui", "/Gui"))
                {
                    HideConsoleWindow(); UpdateCoordinator.RegisterInterfaceHook(); return RunGraphicalInterface();
                }
                if (HasSwitch(args, "-GuiScreenshot", "--gui-screenshot"))
                {
                    HideConsoleWindow(); UpdateCoordinator.RegisterInterfaceHook(); return CaptureGraphicalInterface(GetOptionValue(args, "-GuiScreenshot", "--gui-screenshot"));
                }
                if (HasSwitch(args, "-Help", "--help", "/?", "-?")) { PrintHelp(); return 0; }
                if (HasSwitch(args, "-Status", "--status", "/Status")) { return ShowStatus(); }
                if (HasSwitch(args, "-Install", "--install", "/Install")) { Console.Error.WriteLine("A instalacao persistente nao faz parte da edicao portatil."); return 2; }
                return RunMonitor(RemoveSwitches(args, "-Worker", "--worker"));
            }
            catch (Exception exception) { Console.Error.WriteLine("IPConflictMonitor: " + exception.Message); return 1; }
        }

        private static int RunMonitor(string[] args)
        {
            string applicationDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string configPath = FindConfigArgument(args);
            if (configPath != null && String.IsNullOrWhiteSpace(configPath)) { throw new ArgumentException("-ConfigPath requer o caminho de um arquivo JSON."); }
            if (configPath == null) { configPath = EnsureDefaultConfiguration(applicationDirectory); }
            return NativeMonitor.Run(args, configPath);
        }

        private static int ShowStatus()
        {
            string data = GetUserDataDirectory(); string snapshot = Path.Combine(data, "reports", "snapshot.csv");
            Console.WriteLine("IPConflictMonitor 3.2.2 - Strict Evidence Detection");
            Console.WriteLine("Motor: C# nativo; confirmacao somente por ARP ativo correlacionado e repetido");
            Console.WriteLine("Politica: fail-closed sem TShark/Npcap/captura saudavel");
            Console.WriteLine("Atualizador: GitHub Releases com validacao SHA-256");
            Console.WriteLine("Ultimo diagnostico: " + (File.Exists(snapshot) ? File.GetLastWriteTime(snapshot).ToString("dd/MM/yyyy HH:mm:ss") : "ainda nao executado"));
            Console.WriteLine("Dados: " + data); return 0;
        }

        private static string GetUserDataDirectory() { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProductDirectoryName); }

        private static string EnsureDefaultConfiguration(string applicationDirectory)
        {
            string sidecar = Path.Combine(applicationDirectory, "config", "config.json"); if (File.Exists(sidecar)) { return sidecar; }
            string directory = Path.Combine(GetUserDataDirectory(), "config"); Directory.CreateDirectory(directory); string path = Path.Combine(directory, "config.json");
            if (!File.Exists(path)) { File.WriteAllText(path, ReadResourceText(ConfigResource), new UTF8Encoding(false)); } return path;
        }

        private static string ReadResourceText(string resourceName)
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)) { if (stream == null) { throw new InvalidOperationException("Recurso interno ausente: " + resourceName); } using (var reader = new StreamReader(stream, Encoding.UTF8, true)) { return reader.ReadToEnd(); } }
        }

        private static string FindConfigArgument(string[] args)
        {
            for (int index = 0; index < args.Length; index++) { if (String.Equals(args[index], "-ConfigPath", StringComparison.OrdinalIgnoreCase) || String.Equals(args[index], "--ConfigPath", StringComparison.OrdinalIgnoreCase) || String.Equals(args[index], "/ConfigPath", StringComparison.OrdinalIgnoreCase)) { return index + 1 < args.Length ? args[index + 1] : String.Empty; } } return null;
        }

        private static string[] RemoveSwitches(string[] args, params string[] names)
        {
            var output = new List<string>(); foreach (string argument in args) { bool remove = false; foreach (string name in names) { if (String.Equals(argument, name, StringComparison.OrdinalIgnoreCase)) { remove = true; break; } } if (!remove) { output.Add(argument); } } return output.ToArray();
        }

        private static string GetOptionValue(string[] args, params string[] names)
        {
            for (int index = 0; index < args.Length; index++) { foreach (string name in names) { if (String.Equals(args[index], name, StringComparison.OrdinalIgnoreCase)) { return index + 1 < args.Length ? args[index + 1] : null; } } } return null;
        }

        private static bool HasSwitch(string[] args, params string[] names)
        {
            foreach (string argument in args) { foreach (string name in names) { if (String.Equals(argument, name, StringComparison.OrdinalIgnoreCase)) { return true; } } } return false;
        }

        private static void PrintHelp()
        {
            Console.WriteLine("IPConflictMonitor 3.2.2 - Strict Evidence Detection");
            Console.WriteLine("  IPConflictMonitor.exe                         Abre o painel grafico.");
            Console.WriteLine("  IPConflictMonitor.exe -Worker -Once           Executa uma varredura portatil.");
            Console.WriteLine("  IPConflictMonitor.exe -Worker                 Monitora enquanto o processo estiver aberto.");
            Console.WriteLine("  IPConflictMonitor.exe -SelfTestDetection      Executa 24 cenarios sinteticos.");
            Console.WriteLine("  IPConflictMonitor.exe -CheckUpdate            Consulta a release mais recente.");
            Console.WriteLine("  IPConflictMonitor.exe -Status                 Exibe o ultimo diagnostico.");
            Console.WriteLine("  IPConflictMonitor.exe -ValidateConfiguration  Valida config/config.json.");
            Console.WriteLine();
            Console.WriteLine("CONFIRMED exige ARP ativo, correlacionado, dois MACs, rodadas repetidas e ciclos consecutivos.");
        }
    }
}
