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
        foreach ($marker in @('StrictVerificationReady','RequestObserved','RequestCorrelationValid','PositiveRounds','SameMacPairAcrossRounds','ConsecutivePositiveCycles','PossibleProxyArp','TrustedPair','InterfaceValidation')) {
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
    }
}

Describe 'Compiled Strict Evidence executable' {
    BeforeAll {
        $script:projectRoot = Split-Path $PSScriptRoot -Parent
        $script:exe = Join-Path $script:projectRoot 'dist\IPConflictMonitor.exe'
    }

    It 'reports version 3.2.2.0' {
        Assert-StrictTrue (Test-Path -LiteralPath $script:exe) 'compiled executable exists'
        Assert-StrictEqual (Get-Item -LiteralPath $script:exe).VersionInfo.FileVersion '3.2.2.0' 'compiled executable version is correct'
    }

    It 'passes all synthetic detection scenarios' {
        $result = & $script:exe -SelfTestDetection | Out-String
        Assert-StrictEqual $LASTEXITCODE 0 'self-test process exits successfully'
        Assert-StrictMatch $result '(?m)^24 passed\s*$' 'all 24 scenarios pass'
        Assert-StrictMatch $result '(?m)^0 failed\s*$' 'no synthetic scenario fails'
    }

    It 'contains the strict states and evidence markers' {
        $bytes = [IO.File]::ReadAllBytes($script:exe)
        $ascii = [Text.Encoding]::ASCII.GetString($bytes)
        $unicode = [Text.Encoding]::Unicode.GetString($bytes)
        foreach ($marker in @('MONITORING_LIMITED','UNVERIFIED','STRICT_PROOF','-SelfTestDetection','EvidenceHash')) {
            Assert-StrictTrue ($ascii.Contains($marker) -or $unicode.Contains($marker)) "binary marker $marker is present"
        }
    }
}
