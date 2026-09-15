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

    It 'renders a full-size version 3.4.0 dashboard preview' {
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
        Assert-VisualMatch (Get-Content -LiteralPath (Join-Path $script:projectRoot 'launcher\IPConflictMonitor.NeonGui.cs') -Raw) 'IPCONFLICTMONITOR 3.4.0' 'versioned product footer is present'
    }
    It 'renders the in-app update page at full dashboard size' {
        Add-Type -AssemblyName System.Drawing
        $preview = Join-Path $script:projectRoot 'dist\IPConflictMonitor-update.png'
        Assert-VisualTrue (Test-Path -LiteralPath $preview) 'update preview exists'
        $image = [Drawing.Image]::FromFile($preview)
        try {
            Assert-VisualTrue ($image.Width -gt 1000) 'update page width is greater than 1000px'
            Assert-VisualTrue ($image.Height -gt 600) 'update page height is greater than 600px'
        }
        finally {
            $image.Dispose()
        }
        $source = Get-Content -LiteralPath (Join-Path $script:projectRoot 'launcher\IPConflictMonitor.UpdateExperience.cs') -Raw
        foreach ($marker in @('ATUALIZAÇÃO ASSISTIDA','CONSULTA','DOWNLOAD','INTEGRIDADE','INSTALAÇÃO','REINÍCIO','PROTEÇÕES ATIVAS')) {
            Assert-VisualMatch $source $marker "update page marker $marker is present"
        }
    }

    It 'renders the integrated settings page at full dashboard size' {
        Add-Type -AssemblyName System.Drawing
        $preview = Join-Path $script:projectRoot 'dist\IPConflictMonitor-settings.png'
        Assert-VisualTrue (Test-Path -LiteralPath $preview) 'settings preview exists'
        $image = [Drawing.Image]::FromFile($preview)
        try {
            Assert-VisualTrue ($image.Width -gt 1000) 'settings page width is greater than 1000px'
            Assert-VisualTrue ($image.Height -gt 600) 'settings page height is greater than 600px'
        }
        finally {
            $image.Dispose()
        }
        $source = Get-Content -LiteralPath (Join-Path $script:projectRoot 'launcher\IPConflictMonitor.SettingsExperience.cs') -Raw
        foreach ($marker in @('SettingsExperiencePage','CONFIGURAÇÃO / PERFIL ATIVO','CONFIRMAR E SALVAR','STRICT EVIDENCE  /  PROTEGIDO')) {
            Assert-VisualMatch $source $marker "settings page marker $marker is present"
        }
    }

    It 'uses the technical appliance typography and native selector contract' {
        $source = Get-Content -LiteralPath (Join-Path $script:projectRoot 'launcher\IPConflictMonitor.SettingsExperience.cs') -Raw
        foreach ($marker in @('Bahnschrift','Segoe UI','Consolas','ComboBoxStyle.DropDownList')) {
            Assert-VisualMatch $source $marker "settings visual marker $marker is present"
        }
    }

    It 'contains no decorative circles or external configuration editor' {
        $uiSource = @(
            (Get-Content -LiteralPath (Join-Path $script:projectRoot 'launcher\IPConflictMonitor.NeonGui.cs') -Raw),
            (Get-Content -LiteralPath (Join-Path $script:projectRoot 'launcher\IPConflictMonitor.UpdateExperience.cs') -Raw),
            (Get-Content -LiteralPath (Join-Path $script:projectRoot 'launcher\IPConflictMonitor.SettingsExperience.cs') -Raw)
        ) -join [Environment]::NewLine
        Assert-VisualNoMatch $uiSource '(?i)notepad\.exe|FillEllipse|DrawEllipse|●|◉' 'UI contains no legacy editor or decorative circular markers'
    }
}
