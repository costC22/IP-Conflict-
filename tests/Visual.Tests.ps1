$projectRoot = Split-Path $PSScriptRoot -Parent

Describe 'IPConflictMonitor field interface' {
    It 'contains the modern visual components and update control' {
        $gui = Get-Content (Join-Path $projectRoot 'launcher\IPConflictMonitor.NeonGui.cs') -Raw
        $updater = Get-Content (Join-Path $projectRoot 'launcher\IPConflictMonitor.Updater.cs') -Raw
        $gui | Should Match 'EventFeed'
        $gui | Should Match 'ScanBar'
        $gui | Should Match 'EM_SETCUEBANNER|0x1501'
        $gui | Should Match 'FIELD EDITION 3.1'
        $gui | Should Match 'messageIndex'
        $updater | Should Match 'VERIFICAR ATUALIZA'
        $updater | Should Match 'GetCurrentVersion\(\)\.ToString\(3\)'
    }

    It 'renders a full-size dashboard preview' {
        Add-Type -AssemblyName System.Drawing
        $preview = Join-Path $projectRoot 'dist\IPConflictMonitor-dashboard.png'
        Test-Path $preview | Should Be $true
        $image = [Drawing.Image]::FromFile($preview)
        try {
            $image.Width | Should BeGreaterThan 1300
            $image.Height | Should BeGreaterThan 800
        }
        finally { $image.Dispose() }
    }
}


