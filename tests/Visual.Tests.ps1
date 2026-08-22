Describe 'Strict Evidence field interface' {
    BeforeAll {
        $script:projectRoot = Split-Path $PSScriptRoot -Parent
    }

    It 'renders only the allowed operator states' {
        $source = Get-Content -LiteralPath (Join-Path $script:projectRoot 'launcher\IPConflictMonitor.NeonGui.cs') -Raw
        $source | Should Match 'CONFLITO CONFIRMADO'
        $source | Should Match 'NÃO VERIFICADO'
        $source | Should Match 'MONITORAMENTO LIMITADO'
        $source | Should Not Match '\bSUSPECT\b'
    }

    It 'shows proof rounds cycles correlation and Evidence ID' {
        $source = Get-Content -LiteralPath (Join-Path $script:projectRoot 'launcher\IPConflictMonitor.NeonGui.cs') -Raw
        foreach ($marker in @('RequestObserved','PositiveRounds','RequiredRounds','ConfirmedCycles','RequiredCycles','CorrelatedArpReplies','EvidenceId','EvidenceQuality')) {
            $source | Should Match $marker
        }
    }

    It 'renders a full-size version 3.2 dashboard preview' {
        Add-Type -AssemblyName System.Drawing
        $preview = Join-Path $script:projectRoot 'dist\IPConflictMonitor-dashboard.png'
        Test-Path -LiteralPath $preview | Should Be $true
        $image = [Drawing.Image]::FromFile($preview)
        try {
            $image.Width | Should BeGreaterThan 1300
            $image.Height | Should BeGreaterThan 800
        }
        finally {
            $image.Dispose()
        }
        (Get-Content -LiteralPath (Join-Path $script:projectRoot 'launcher\IPConflictMonitor.NeonGui.cs') -Raw) | Should Match 'FIELD EDITION 3.2'
    }
}
