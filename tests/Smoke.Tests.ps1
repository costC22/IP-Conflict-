$projectRoot = Split-Path $PSScriptRoot -Parent

Describe 'IPConflictMonitor 3.1 source package' {
    It 'contains the complete native build inputs' {
        Test-Path (Join-Path $projectRoot 'launcher\IPConflictMonitor.PortableLauncher.3_1.cs') | Should Be $true
        Test-Path (Join-Path $projectRoot 'launcher\IPConflictMonitor.NativeMonitor.cs') | Should Be $true
        Test-Path (Join-Path $projectRoot 'launcher\IPConflictMonitor.NeonGui.cs') | Should Be $true
        Test-Path (Join-Path $projectRoot 'launcher\IPConflictMonitor.Updater.cs') | Should Be $true
    }

    It 'contains valid JSON configuration' {
        { Get-Content (Join-Path $projectRoot 'config\config.json') -Raw | ConvertFrom-Json } | Should Not Throw
    }

    It 'compiles only the active source files' {
        $build = Get-Content (Join-Path $projectRoot 'Build-Executable.ps1') -Raw
        $build | Should Match 'IPConflictMonitor.PortableLauncher.3_1.cs'
        $build | Should Match 'IPConflictMonitor.NativeMonitor.cs'
        $build | Should Match 'IPConflictMonitor.NeonGui.cs'
        $build | Should Match 'IPConflictMonitor.Updater.cs'
        $build | Should Match 'System.IO.Compression.FileSystem.dll'
    }

    It 'does not add elevation or persistence' {
        $source = Get-Content (Join-Path $projectRoot 'launcher\IPConflictMonitor.PortableLauncher.3_1.cs') -Raw
        $source += Get-Content (Join-Path $projectRoot 'launcher\IPConflictMonitor.Updater.cs') -Raw
        $source | Should Not Match 'Verb\s*=\s*"runas"'
        $source | Should Not Match 'schtasks'
        $source | Should Not Match 'CurrentVersion\\Run'
        $source | Should Match '3.1.0.0'
    }

    It 'uses native Windows ARP APIs' {
        $engine = Get-Content (Join-Path $projectRoot 'launcher\IPConflictMonitor.NativeMonitor.cs') -Raw
        $engine | Should Match 'GetIpNetTable'
        $engine | Should Match 'DeleteIpNetEntry'
        $engine | Should Match 'ARP-Packet'
        $engine | Should Match 'ActiveProbe'
    }

    It 'validates release downloads before applying them' {
        $updater = Get-Content (Join-Path $projectRoot 'launcher\IPConflictMonitor.Updater.cs') -Raw
        $updater | Should Match 'releases/latest'
        $updater | Should Match 'IPConflictMonitor.exe.sha256'
        $updater | Should Match 'ComputeSha256'
        $updater | Should Match 'ExtractZipSafely'
        $updater | Should Match 'File.Copy\(backup, target'
        $updater | Should Match 'uri\.Host'
        $updater | Should Not Match 'Authorization'
        $updater | Should Not Match 'Bearer '
    }
}

Describe 'Built portable executable' {
    $exe = Join-Path $projectRoot 'dist\IPConflictMonitor.exe'

    It 'exists and reports version 3.1.0.0' {
        Test-Path $exe | Should Be $true
        (Get-Item $exe).VersionInfo.FileVersion | Should Be '3.1.0.0'
    }

    It 'embeds only the default configuration' {
        $resources = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes($exe)).GetManifestResourceNames()
        @($resources).Count | Should Be 1
        $resources[0] | Should Be 'IPConflictMonitor.DefaultConfig.json'
    }

    It 'contains the updater and field interface markers' {
        $bytes = [IO.File]::ReadAllBytes($exe)
        $ascii = [Text.Encoding]::ASCII.GetString($bytes)
        $unicode = [Text.Encoding]::Unicode.GetString($bytes)
        foreach ($marker in @('-CheckUpdate', '-ApplyUpdate', 'IPConflictMonitor-Windows.zip', 'ANALISAR REDE', 'TELEMETRIA EM TEMPO REAL')) {
            ($ascii.IndexOf($marker, [StringComparison]::Ordinal) -ge 0 -or $unicode.IndexOf($marker, [StringComparison]::Ordinal) -ge 0) | Should Be $true
        }
    }

    It 'ships the release artifacts' {
        Test-Path (Join-Path $projectRoot 'dist\IPConflictMonitor-Windows.zip') | Should Be $true
        Test-Path (Join-Path $projectRoot 'dist\IPConflictMonitor.exe.sha256') | Should Be $true
        Test-Path (Join-Path $projectRoot 'dist\IPConflictMonitor-dashboard.png') | Should Be $true
    }
}


