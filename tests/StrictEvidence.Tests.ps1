Describe 'Strict Evidence policy' {
    BeforeAll {
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
        $source | Should Match 'EvaluateConflict\('
        @([regex]::Matches($source, 'decision\.State\s*=\s*StrictDetectionState\.CONFIRMED')).Count | Should Be 1
    }

    It 'cannot confirm from history cache score or flapping' {
        $native = Get-Content -LiteralPath $script:nativePath -Raw
        $strict = Get-Content -LiteralPath $script:strictPath -Raw
        $native | Should Not Match 'MinMacTransitions|MinObservationsPerMac'
        $native | Should Not Match 'ConfidenceScore\s*[><=].*CONFIRMED'
        $native | Should Not Match 'directConflict|activeConflict|enough\s*&&\s*transitions'
        $strict | Should Match 'OnlyHistoricalChange'
        $strict | Should Match 'CacheOnlyAmbiguity'
    }

    It 'has no SUSPECT state or incident path' {
        (Get-Content -LiteralPath $script:strictPath -Raw) | Should Not Match '\bSUSPECT\b'
        (Get-Content -LiteralPath $script:nativePath -Raw) | Should Not Match '\bSUSPECT\b'
        (Get-Content -LiteralPath $script:guiPath -Raw) | Should Not Match '\bSUSPECT\b'
    }

    It 'requires capture correlation repeated rounds and cycles' {
        $source = Get-Content -LiteralPath $script:strictPath -Raw
        foreach ($marker in @('StrictVerificationReady','RequestObserved','RequestCorrelationValid','PositiveRounds','SameMacPairAcrossRounds','ConsecutivePositiveCycles','PossibleProxyArp','TrustedPair','InterfaceValidation')) {
            $source | Should Match $marker
        }
    }

    It 'validates invalid broadcast and multicast MAC addresses centrally' {
        $source = Get-Content -LiteralPath $script:strictPath -Raw
        $source | Should Match 'NormalizeAndValidateMac'
        $source | Should Match 'FFFFFFFFFFFF'
        $source | Should Match '\(first & 1\)'
    }

    It 'persists state and snapshot atomically' {
        $source = Get-Content -LiteralPath $script:nativePath -Raw
        $source | Should Match 'WriteAtomicText'
        $source | Should Match 'stream\.Flush\(true\)'
        $source | Should Match 'File\.Replace\('
    }

    It 'correlates an observed request with replies inside a time window' {
        $source = Get-Content -LiteralPath $script:nativePath -Raw
        $source | Should Match 'packet\.Opcode == 1'
        $source | Should Match 'packet\.Opcode == 2'
        $source | Should Match 'ResponseWindowStartUtc'
        $source | Should Match 'ResponseWindowEndUtc'
        $source | Should Match 'String\.Equals\(packet\.TargetIp, selected\.Address'
    }

    It 'limits process execution and ARP probe rate' {
        $source = Get-Content -LiteralPath $script:nativePath -Raw
        $source | Should Match 'UseShellExecute = false'
        $source | Should Match 'WaitForExit\(timeoutMs\)'
        $source | Should Match 'ArpProbeRateLimitMs'
        $source | Should Match 'MaxConcurrentVerifications'
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
        $config.Monitoring.DetectionMode | Should Be 'StrictEvidence'
        $config.Monitoring.VerificationRounds | Should Be 3
        $config.Monitoring.RequiredPositiveRounds | Should Be 2
        $config.Monitoring.RequiredConfirmedCycles | Should Be 2
        $config.Monitoring.RequireCapturedArpRequest | Should Be $true
        $config.Monitoring.RequireCorrelatedArpResponses | Should Be $true
        $config.Monitoring.FailClosedWithoutCapture | Should Be $true
    }

    It 'aborts the build unless all 24 self-tests pass' {
        $build = Get-Content -LiteralPath $script:buildPath -Raw
        $build | Should Match '-SelfTestDetection'
        $build | Should Match '24 passed'
        $build | Should Match '0 failed'
        $build | Should Match 'BUILD ABORTADO'
    }

    It 'contains a static regression gate' {
        $build = Get-Content -LiteralPath $script:buildPath -Raw
        $build | Should Match 'Regression gate'
        $build | Should Match 'MinMacTransitions'
        $build | Should Match 'status = "SUSPECT"'
        $build | Should Match 'exatamente um caminho decisorio'
    }
}

Describe 'Compiled Strict Evidence executable' {
    BeforeAll {
        $script:projectRoot = Split-Path $PSScriptRoot -Parent
        $script:exe = Join-Path $script:projectRoot 'dist\IPConflictMonitor.exe'
    }

    It 'reports version 3.2.1.0' {
        Test-Path -LiteralPath $script:exe | Should Be $true
        (Get-Item -LiteralPath $script:exe).VersionInfo.FileVersion | Should Be '3.2.1.0'
    }

    It 'passes all synthetic detection scenarios' {
        $result = & $script:exe -SelfTestDetection | Out-String
        $LASTEXITCODE | Should Be 0
        $result | Should Match '(?m)^24 passed\s*$'
        $result | Should Match '(?m)^0 failed\s*$'
    }

    It 'contains the strict states and evidence markers' {
        $bytes = [IO.File]::ReadAllBytes($script:exe)
        $ascii = [Text.Encoding]::ASCII.GetString($bytes)
        $unicode = [Text.Encoding]::Unicode.GetString($bytes)
        foreach ($marker in @('MONITORING_LIMITED','UNVERIFIED','STRICT_PROOF','-SelfTestDetection','EvidenceHash')) {
            ($ascii.Contains($marker) -or $unicode.Contains($marker)) | Should Be $true
        }
    }
}
