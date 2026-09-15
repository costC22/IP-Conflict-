Describe 'Strict Evidence policy' {
    BeforeAll {
        function global:Assert-StrictTrue {
            param($Condition, [string]$Message)
            if (-not [bool]$Condition) { throw "Assertion failed: $Message" }
        }
        function global:Assert-StrictEqual {
            param($Actual, $Expected, [string]$Message)
            if ($Actual -ne $Expected) { throw "Assertion failed: $Message. Expected '$Expected', received '$Actual'." }
        }
        function global:Assert-StrictMatch {
            param([string]$Actual, [string]$Pattern, [string]$Message)
            if ($Actual -notmatch $Pattern) { throw "Assertion failed: $Message. Pattern '$Pattern' was not found." }
        }
        function global:Assert-StrictNoMatch {
            param([string]$Actual, [string]$Pattern, [string]$Message)
            if ($Actual -match $Pattern) { throw "Assertion failed: $Message. Forbidden pattern '$Pattern' was found." }
        }

        $script:projectRoot = Split-Path $PSScriptRoot -Parent
        $script:strictPath = Join-Path $script:projectRoot 'launcher\IPConflictMonitor.StrictEvidence.cs'
        $script:nativePath = Join-Path $script:projectRoot 'launcher\IPConflictMonitor.NativeMonitor.cs'
        $script:guiPath = Join-Path $script:projectRoot 'launcher\IPConflictMonitor.NeonGui.cs'
        $script:buildPath = Join-Path $script:projectRoot 'Build-Executable.ps1'
        $script:configPath = Join-Path $script:projectRoot 'config\config.json'
        $script:exe = Join-Path $script:projectRoot 'dist\IPConflictMonitor.exe'
    }

    It 'uses one centralized decision engine' {
        $source = Get-Content -LiteralPath $script:strictPath -Raw
        Assert-StrictMatch $source 'EvaluateConflict\(' 'central decision engine is present'
        Assert-StrictEqual @([regex]::Matches($source, 'decision\.State\s*=\s*StrictDetectionState\.CONFIRMED')).Count 1 'exactly one CONFIRMED assignment exists'
    }

    It 'cannot confirm from history cache score or flapping' {
        $native = Get-Content -LiteralPath $script:nativePath -Raw
        $strict = Get-Content -LiteralPath $script:strictPath -Raw
        Assert-StrictNoMatch $native 'MinMacTransitions|MinObservationsPerMac' 'legacy history thresholds are absent'
        Assert-StrictNoMatch $native 'ConfidenceScore\s*[><=].*CONFIRMED' 'confidence score cannot confirm'
        Assert-StrictNoMatch $native 'directConflict|activeConflict|enough\s*&&\s*transitions' 'legacy unsafe confirmation paths are absent'
        Assert-StrictMatch $strict 'OnlyHistoricalChange' 'historical change case is explicit'
        Assert-StrictMatch $strict 'CacheOnlyAmbiguity' 'cache ambiguity case is explicit'
    }

    It 'has no SUSPECT state or incident path' {
        Assert-StrictNoMatch (Get-Content -LiteralPath $script:strictPath -Raw) '\bSUSPECT\b' 'strict engine contains no SUSPECT state'
        Assert-StrictNoMatch (Get-Content -LiteralPath $script:nativePath -Raw) '\bSUSPECT\b' 'native monitor contains no SUSPECT state'
        Assert-StrictNoMatch (Get-Content -LiteralPath $script:guiPath -Raw) '\bSUSPECT\b' 'GUI contains no SUSPECT state'
    }

    It 'requires capture correlation repeated rounds and cycles' {
        $source = Get-Content -LiteralPath $script:strictPath -Raw
        foreach ($marker in @('StrictVerificationReady','RequestObserved','RequestCorrelationValid','PositiveRounds','SameMacPairAcrossRounds','PairCandidates','ConsecutivePositiveCycles','PossibleProxyArp','TrustedPair','InterfaceValidation')) {
            Assert-StrictMatch $source $marker "required evidence marker $marker is present"
        }
    }

    It 'validates invalid broadcast and multicast MAC addresses centrally' {
        $source = Get-Content -LiteralPath $script:strictPath -Raw
        Assert-StrictMatch $source 'NormalizeAndValidateMac' 'central MAC validator is present'
        Assert-StrictMatch $source 'FFFFFFFFFFFF' 'broadcast MAC is rejected'
        Assert-StrictMatch $source '\(first & 1\)' 'multicast MAC bit is rejected'
    }

    It 'persists state and snapshot atomically' {
        $source = Get-Content -LiteralPath $script:nativePath -Raw
        Assert-StrictMatch $source 'WriteAtomicText' 'atomic writer is present'
        Assert-StrictMatch $source 'stream\.Flush\(true\)' 'writer flushes to disk'
        Assert-StrictMatch $source 'File\.Replace\(' 'writer replaces atomically'
    }

    It 'correlates an observed request with replies inside a time window' {
        $source = Get-Content -LiteralPath $script:nativePath -Raw
        Assert-StrictMatch $source 'packet\.Opcode == 1' 'ARP requests are recognized'
        Assert-StrictMatch $source 'packet\.Opcode == 2' 'ARP replies are recognized'
        Assert-StrictMatch $source 'ResponseWindowStartUtc' 'response window start is tracked'
        Assert-StrictMatch $source 'ResponseWindowEndUtc' 'response window end is tracked'
        Assert-StrictMatch $source 'String\.Equals\(packet\.TargetIp, selected\.Address' 'reply target is correlated to selected interface'
    }

    It 'forces fresh ARP discovery without deleting static neighbors' {
        $source = Get-Content -LiteralPath $script:nativePath -Raw
        Assert-StrictMatch $source 'ClearDynamicNeighbors' 'active discovery refresh is present'
        Assert-StrictMatch $source 'entry\.NativeRow\.dwType != 3' 'only dynamic neighbor entries are refreshed'
        Assert-StrictMatch $source 'discoveryCapture != null.*ClearDynamicNeighbors' 'refresh runs only while discovery capture exists'
        Assert-StrictMatch $source 'CalculateDiscoveryCaptureSeconds' 'capture window adapts to target count and timeout'
        Assert-StrictMatch $source 'Math\.Min\(120L' 'adaptive capture window remains bounded'
    }

    It 'binds requests and replies to the selected local MAC' {
        $source = Get-Content -LiteralPath $script:nativePath -Raw
        Assert-StrictMatch $source 'packet\.SourceMac\), localMac' 'captured request source MAC is local'
        Assert-StrictMatch $source 'packet\.TargetMac\), localMac' 'captured reply target MAC is local'
        Assert-StrictMatch $source 'excludedMacs\.Contains\(mac\)' 'excluded MACs cannot enter strict proof'
    }

    It 'supports a stable conflict pair among three or more responders' {
        $source = Get-Content -LiteralPath $script:strictPath -Raw
        Assert-StrictMatch $source 'PairCandidates' 'all responder pairs are generated'
        Assert-StrictMatch $source 'SelectMany.*PairCandidates' 'stable pair counts span repeated rounds'
        Assert-StrictMatch $source 'three responders with stable pair' 'three-responder self-test is registered'
    }

    It 'limits process execution and ARP probe rate' {
        $source = Get-Content -LiteralPath $script:nativePath -Raw
        Assert-StrictMatch $source 'UseShellExecute = false' 'external processes do not use shell execution'
        Assert-StrictMatch $source 'WaitForExit\(timeoutMs\)' 'external processes have a timeout'
        Assert-StrictMatch $source 'ArpProbeRateLimitMs' 'ARP probe rate is limited'
        Assert-StrictMatch $source 'MaxConcurrentVerifications' 'verification concurrency is limited'
    }
}

Describe 'Strict Evidence configuration and build gate' {
    BeforeAll {
        $script:projectRoot = Split-Path $PSScriptRoot -Parent
        $script:buildPath = Join-Path $script:projectRoot 'Build-Executable.ps1'
        $script:configPath = Join-Path $script:projectRoot 'config\config.json'
    }

    It 'ships safe immutable defaults' {
        $config = Get-Content -LiteralPath $script:configPath -Raw | ConvertFrom-Json
        Assert-StrictEqual $config.Monitoring.DetectionMode 'StrictEvidence' 'strict mode is enabled'
        Assert-StrictEqual $config.Monitoring.VerificationRounds 3 'three rounds are configured'
        Assert-StrictEqual $config.Monitoring.RequiredPositiveRounds 2 'two positive rounds are required'
        Assert-StrictEqual $config.Monitoring.RequiredConfirmedCycles 2 'two confirmed cycles are required'
        Assert-StrictTrue $config.Monitoring.RequireCapturedArpRequest 'captured ARP request is mandatory'
        Assert-StrictTrue $config.Monitoring.RequireCorrelatedArpResponses 'correlated ARP replies are mandatory'
        Assert-StrictTrue $config.Monitoring.FailClosedWithoutCapture 'capture failure is fail-closed'
    }

    It 'aborts the build unless all 24 self-tests pass' {
        $build = Get-Content -LiteralPath $script:buildPath -Raw
        Assert-StrictMatch $build '-SelfTestDetection' 'self-test is executed by build'
        Assert-StrictMatch $build '24 passed' 'build requires 24 passed'
        Assert-StrictMatch $build '0 failed' 'build requires zero failed'
        Assert-StrictMatch $build 'BUILD ABORTADO' 'build has explicit failure gate'
    }

    It 'contains a static regression gate' {
        $build = Get-Content -LiteralPath $script:buildPath -Raw
        Assert-StrictMatch $build 'Regression gate' 'regression gate is present'
        Assert-StrictMatch $build 'MinMacTransitions' 'legacy marker is blocked'
        Assert-StrictMatch $build 'status = "SUSPECT"' 'SUSPECT marker is blocked'
        Assert-StrictMatch $build 'exatamente um caminho decisorio' 'single decision path is enforced'
        Assert-StrictMatch $build 'ClearDynamicNeighbors' 'active discovery refresh is gated'
        Assert-StrictMatch $build 'CalculateDiscoveryCaptureSeconds' 'adaptive capture duration is gated'
        Assert-StrictMatch $build 'packet.TargetMac' 'local MAC correlation is gated'
        Assert-StrictMatch $build 'PairCandidates' 'multi-responder pair logic is gated'
    }
}

Describe 'Assisted update experience' {
    BeforeAll {
        $script:projectRoot = Split-Path $PSScriptRoot -Parent
        $script:updaterPath = Join-Path $script:projectRoot 'launcher\IPConflictMonitor.Updater.cs'
        $script:updateExperiencePath = Join-Path $script:projectRoot 'launcher\IPConflictMonitor.UpdateExperience.cs'
    }

    It 'keeps the update flow inside an app-owned page' {
        $updater = Get-Content -LiteralPath $script:updaterPath -Raw
        $experience = Get-Content -LiteralPath $script:updateExperiencePath -Raw
        Assert-StrictMatch $updater 'GetControlFromPosition\(1, 0\)' 'update page replaces the main dashboard cell'
        Assert-StrictMatch $updater 'PreviousContent = previous' 'dashboard content reference is preserved'
        Assert-StrictMatch $updater 'previous\.Visible = false' 'dashboard content is preserved behind the update page'
        Assert-StrictMatch $experience 'UpdateExperiencePage' 'shared update page exists'
        Assert-StrictMatch $experience 'CONSULTA.*DOWNLOAD.*INTEGRIDADE.*INSTALAÇÃO.*REINÍCIO' 'five real update stages are visible'
        Assert-StrictNoMatch $updater 'MessageBox\.Show' 'updater uses no native message boxes'
    }

    It 'prevents competing pages and protects unsaved settings' {
        $updater = Get-Content -LiteralPath $script:updaterPath -Raw
        $gui = Get-Content -LiteralPath (Join-Path $script:projectRoot 'launcher\IPConflictMonitor.NeonGui.cs') -Raw
        Assert-StrictMatch $gui 'TryEnterUpdatePage' 'main shell owns the update navigation lock'
        Assert-StrictMatch $gui 'SetWorkspaceNavigationEnabled\(false\)' 'sidebar navigation is disabled while update owns the content cell'
        Assert-StrictMatch $gui '_settingsPage\.HasUnsavedChanges' 'unsaved settings block update navigation'
        Assert-StrictMatch $updater 'operations\.TryEnterUpdatePage\(\)' 'updater acquires the main shell lock'
        Assert-StrictMatch $updater 'operations\.LeaveUpdatePage\(\)' 'updater releases the main shell lock'
    }

    It 'reports truthful download progress and verifies the complete file' {
        $updater = Get-Content -LiteralPath $script:updaterPath -Raw
        Assert-StrictMatch $updater 'received \* 100L / total' 'download percentage is byte based'
        Assert-StrictMatch $updater 'expectedSize > 0 && received != expectedSize' 'published size is enforced'
        Assert-StrictMatch $updater '\.part' 'download uses a partial file'
        Assert-StrictMatch $updater 'output\.Flush\(true\)' 'download is flushed before promotion'
        Assert-StrictMatch $updater 'File\.Move\(partial, destination\)' 'validated transfer is promoted after completion'
    }

    It 'shows installation recovery retry and restart states' {
        $updater = Get-Content -LiteralPath $script:updaterPath -Raw
        $experience = Get-Content -LiteralPath $script:updateExperiencePath -Raw
        Assert-StrictMatch $updater 'target \+ "\.previous"' 'previous executable is backed up'
        Assert-StrictMatch $updater 'File\.Copy\(backup, target, true\)' 'failed replacement restores the backup'
        Assert-StrictMatch $experience 'ShowFailure' 'failure remains in the update page'
        Assert-StrictMatch $experience 'TENTAR NOVAMENTE' 'retry action is visible'
        Assert-StrictMatch $experience 'ShowRestarting' 'successful install has a restart state'
    }
}
Describe 'Integrated configuration experience' {
    BeforeAll {
        $script:projectRoot = Split-Path $PSScriptRoot -Parent
        $script:settingsPath = Join-Path $script:projectRoot 'launcher\IPConflictMonitor.SettingsExperience.cs'
        $script:portablePath = Join-Path $script:projectRoot 'launcher\IPConflictMonitor.PortableLauncher.cs'
        $script:guiPath = Join-Path $script:projectRoot 'launcher\IPConflictMonitor.NeonGui.cs'
        $script:buildPath = Join-Path $script:projectRoot 'Build-Executable.ps1'
    }

    It 'uses a canonical local profile seeded from the portable sidecar' {
        $settings = Get-Content -LiteralPath $script:settingsPath -Raw
        $portable = Get-Content -LiteralPath $script:portablePath -Raw
        Assert-StrictMatch $settings 'Path\.Combine\(GetUserDataDirectory\(\), "config"\)' 'canonical configuration lives in the Windows local profile'
        Assert-StrictMatch $settings 'File\.ReadAllBytes\(sidecar\)' 'portable sidecar is used only as the initial seed'
        Assert-StrictMatch $settings 'WriteNewFile\(activePath, seed\)' 'seed is promoted to the canonical profile'
        Assert-StrictMatch $portable 'ConfigurationStore\.ResolveActivePath' 'worker resolves the same canonical configuration'
    }

    It 'validates the expanded field ranges before saving' {
        $settings = Get-Content -LiteralPath $script:settingsPath -Raw
        foreach ($marker in @(
            'InterfaceIndex < 0',
            'CaptureSeconds < 2',
            'CaptureSeconds > 120',
            'CaptureWarmupMilliseconds < 0',
            'EvidenceWindowMinutes < 1',
            'HistoryExpirationMinutes < configuration.Monitoring.EvidenceWindowMinutes',
            'MaxConcurrentPings < 1',
            'MaxConcurrentPings > 256',
            'HostnameTimeoutMs < 100',
            'ProxyArpIpThreshold < 2',
            'LogRetentionFiles < 1',
            'TryCountHosts',
            'Webhook habilitado exige uma URL HTTPS'
        )) {
            Assert-StrictMatch $settings ([regex]::Escape($marker)) "settings validation marker $marker is present"
        }
    }

    It 'saves atomically with durable flush validation and previous backup' {
        $settings = Get-Content -LiteralPath $script:settingsPath -Raw
        Assert-StrictMatch $settings 'stream\.Flush\(true\)' 'configuration bytes are flushed to disk'
        Assert-StrictMatch $settings 'NativeMonitor\.LoadConfiguration\(temporary\)' 'temporary configuration is parsed before promotion'
        Assert-StrictMatch $settings 'Validate\(written\)' 'temporary configuration is validated before promotion'
        Assert-StrictMatch $settings 'path \+ "\.previous"' 'previous configuration path is explicit'
        Assert-StrictMatch $settings 'File\.Replace\(temporary, path, backup, true\)' 'configuration is replaced atomically with backup'
        Assert-StrictMatch $settings 'finally[\s\S]*File\.Delete\(temporary\)' 'temporary file is cleaned after every outcome'
    }

    It 'locks Strict Evidence and requires two deliberate actions for exceptions' {
        $settings = Get-Content -LiteralPath $script:settingsPath -Raw
        foreach ($marker in @(
            'DetectionMode = "StrictEvidence"',
            'RequireCapturedArpRequest = true',
            'RequireCorrelatedArpResponses = true',
            'FailClosedWithoutCapture = true',
            'MaxConcurrentVerifications = 1',
            '_sensitiveConfirmationArmed',
            'SensitiveFingerprint',
            'CONFIRMAR E SALVAR'
        )) {
            Assert-StrictMatch $settings ([regex]::Escape($marker)) "protected settings marker $marker is present"
        }
    }

    It 'applies changes on the next worker and resolves Output Directory consistently' {
        $settings = Get-Content -LiteralPath $script:settingsPath -Raw
        $portable = Get-Content -LiteralPath $script:portablePath -Raw
        $gui = Get-Content -LiteralPath $script:guiPath -Raw
        Assert-StrictMatch $settings 'próxima análise' 'settings explain deferred worker application'
        Assert-StrictMatch $settings 'public static string ResolveOutputDirectory\(\)' 'output resolver exists'
        Assert-StrictMatch $portable 'ConfigurationStore\.ResolveOutputDirectory' 'status uses configured output root'
        Assert-StrictMatch $gui 'ConfigurationStore\.ResolveOutputDirectory' 'dashboard and reports use configured output root'
        Assert-StrictMatch (Get-Content -LiteralPath $script:buildPath -Raw) 'notepad\.exe.*FillEllipse.*DrawEllipse' 'build blocks legacy editor and circular decoration markers'
    }

    It 'keeps invalid profiles recoverable and disposes detached sections' {
        $settings = Get-Content -LiteralPath $script:settingsPath -Raw
        Assert-StrictMatch $settings 'catch \(Exception exception\)[\s\S]*?_dirty = true;[\s\S]*?_saveButton\.Enabled = true;[\s\S]*?PERFIL PRECISA SER RECRIADO' 'invalid profile loads safe editable defaults'
        Assert-StrictMatch $settings 'private void LoadDefaults\(\)[\s\S]*?_saveButton\.Enabled = true;' 'loading defaults always restores save capability'
        Assert-StrictMatch $settings 'protected override void Dispose\(bool disposing\)[\s\S]*?_sections\.Values\.ToArray\(\)[\s\S]*?_sections\.Clear\(\)' 'detached settings sections are disposed explicitly'
    }

    It 'preserves the complete validated integer domain without overflow' {
        $settings = Get-Content -LiteralPath $script:settingsPath -Raw
        $native = Get-Content -LiteralPath (Join-Path $script:projectRoot 'launcher\IPConflictMonitor.NativeMonitor.cs') -Raw
        foreach ($marker in @(
            '_intervalSeconds = CreateNumber(1, Int32.MaxValue',
            '_pingTimeout = CreateNumber(50, Int32.MaxValue',
            '_arpProbeRate = CreateNumber(100, Int32.MaxValue',
            '_logMaxMb = CreateNumber(1, Int32.MaxValue'
        )) {
            Assert-StrictMatch $settings ([regex]::Escape($marker)) "full numeric domain marker $marker is present"
        }
        Assert-StrictMatch $native 'long remainingMilliseconds = Math\.Max\(1L,[\s\S]*?\* 1000L' 'cycle interval multiplication uses 64-bit arithmetic'
        Assert-StrictMatch $native 'WaitCancelable\(EventWaitHandle stopEvent, long milliseconds\)' 'long waits remain cancelable'
    }

    It 'keeps dashboard navigation coherent and stops the worker only after a real close' {
        $gui = Get-Content -LiteralPath $script:guiPath -Raw
        Assert-StrictMatch $gui 'FormClosed \+= delegate \{ StopWorker\(false\); \};' 'worker stops after the form actually closes'
        $closing = [regex]::Match($gui, 'private void HandleFormClosing[\s\S]*?private static List<string> ParseCsv').Value
        Assert-StrictNoMatch $closing 'StopWorker' 'a canceled close does not stop the worker'
        Assert-StrictMatch $gui 'ShowWorkspacePage\(_dashboardContent, _overviewNavigation\); OpenReports\(\);' 'reports restores dashboard content and selection together'
    }
}

Describe 'Compiled Strict Evidence executable' {
    BeforeAll {
        $script:projectRoot = Split-Path $PSScriptRoot -Parent
        $script:exe = Join-Path $script:projectRoot 'dist\IPConflictMonitor.exe'
    }

    It 'reports version 3.4.0.0' {
        Assert-StrictTrue (Test-Path -LiteralPath $script:exe) 'compiled executable exists'
        Assert-StrictEqual (Get-Item -LiteralPath $script:exe).VersionInfo.FileVersion '3.4.0.0' 'compiled executable version is correct'
    }

    It 'passes all synthetic detection scenarios' {
        $result = & $script:exe -SelfTestDetection | Out-String
        Assert-StrictEqual $LASTEXITCODE 0 'self-test process exits successfully'
        Assert-StrictMatch $result '(?m)^24 passed\s*$' 'all 24 scenarios pass'
        Assert-StrictMatch $result '(?m)^0 failed\s*$' 'no synthetic scenario fails'
    }

    It 'contains the strict states and evidence markers' {
        function Test-BinarySequence {
            param([byte[]]$Haystack, [byte[]]$Needle)
            if (-not $Needle -or $Needle.Length -eq 0 -or $Needle.Length -gt $Haystack.Length) { return $false }
            for ($offset = 0; $offset -le $Haystack.Length - $Needle.Length; $offset++) {
                $match = $true
                for ($index = 0; $index -lt $Needle.Length; $index++) {
                    if ($Haystack[$offset + $index] -ne $Needle[$index]) { $match = $false; break }
                }
                if ($match) { return $true }
            }
            return $false
        }
        $bytes = [IO.File]::ReadAllBytes($script:exe)
        foreach ($marker in @('MONITORING_LIMITED','UNVERIFIED','STRICT_PROOF','-SelfTestDetection','-UpdateScreenshot','-SettingsScreenshot','UpdateExperiencePage','SettingsExperiencePage','ConfigurationStore','EvidenceHash','PairCandidates','ClearDynamicNeighbors')) {
            $asciiMarker = [Text.Encoding]::ASCII.GetBytes($marker)
            $unicodeMarker = [Text.Encoding]::Unicode.GetBytes($marker)
            Assert-StrictTrue ((Test-BinarySequence $bytes $asciiMarker) -or (Test-BinarySequence $bytes $unicodeMarker)) "binary marker $marker is present"
        }
    }
}


