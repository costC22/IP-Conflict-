Describe 'Strict Evidence field interface' {
    BeforeAll {
        function global:Assert-VisualTrue {
            param($Condition, [string]$Message)
            if (-not [bool]$Condition) { throw "Assertion failed: $Message" }
        }
        function global:Assert-VisualMatch {
            param([string]$Actual, [string]$Pattern, [string]$Message)
            if ($Actual -notmatch $Pattern) { throw "Assertion failed: $Message. Pattern '$Pattern' was not found." }
        }
        function global:Assert-VisualNoMatch {
            param([string]$Actual, [string]$Pattern, [string]$Message)
            if ($Actual -match $Pattern) { throw "Assertion failed: $Message. Forbidden pattern '$Pattern' was found." }
        }

        $script:projectRoot = Split-Path $PSScriptRoot -Parent
    }

    It 'renders only the allowed operator states' {
        $source = Get-Content -LiteralPath (Join-Path $script:projectRoot 'launcher\IPConflictMonitor.NeonGui.cs') -Raw
        Assert-VisualMatch $source 'CONFLITO CONFIRMADO' 'confirmed state is rendered'
        Assert-VisualMatch $source 'NÃO VERIFICADO' 'unverified state is rendered'
        Assert-VisualMatch $source 'MONITORAMENTO LIMITADO' 'limited state is rendered'
        Assert-VisualNoMatch $source '\bSUSPECT\b' 'SUSPECT state is absent'
    }

    It 'shows proof rounds cycles correlation and Evidence ID' {
        $source = Get-Content -LiteralPath (Join-Path $script:projectRoot 'launcher\IPConflictMonitor.NeonGui.cs') -Raw
        foreach ($marker in @('RequestObserved','PositiveRounds','RequiredRounds','ConfirmedCycles','RequiredCycles','CorrelatedArpReplies','EvidenceId','EvidenceQuality')) {
            Assert-VisualMatch $source $marker "evidence field $marker is rendered"
        }
    }

    It 'renders a full-size version 3.2 dashboard preview' {
        Add-Type -AssemblyName System.Drawing
        $preview = Join-Path $script:projectRoot 'dist\IPConflictMonitor-dashboard.png'
        Assert-VisualTrue (Test-Path -LiteralPath $preview) 'dashboard preview exists'
        $image = [Drawing.Image]::FromFile($preview)
        try {
            Assert-VisualTrue ($image.Width -gt 1000) 'dashboard width is greater than 1000px'
            Assert-VisualTrue ($image.Height -gt 600) 'dashboard height is greater than 600px'
        }
        finally {
            $image.Dispose()
        }
        Assert-VisualMatch (Get-Content -LiteralPath (Join-Path $script:projectRoot 'launcher\IPConflictMonitor.NeonGui.cs') -Raw) 'FIELD EDITION 3.2' 'field edition label is present'
    }
}
