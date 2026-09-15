#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$OutputDirectory = '',
    [string]$CertificateThumbprint = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) { $OutputDirectory = Join-Path $PSScriptRoot 'dist' }

$config = Join-Path $PSScriptRoot 'config\config.json'
$manifest = Join-Path $PSScriptRoot 'launcher\app.portable.manifest'
$readme = Join-Path $PSScriptRoot 'README.md'
$strictSource = Join-Path $PSScriptRoot 'launcher\IPConflictMonitor.StrictEvidence.cs'
$nativeSource = Join-Path $PSScriptRoot 'launcher\IPConflictMonitor.NativeMonitor.cs'
$sources = @(
    (Join-Path $PSScriptRoot 'launcher\IPConflictMonitor.PortableLauncher.cs'),
    $strictSource,
    $nativeSource,
    (Join-Path $PSScriptRoot 'launcher\IPConflictMonitor.NeonGui.cs'),
    (Join-Path $PSScriptRoot 'launcher\IPConflictMonitor.UpdateExperience.cs'),
    (Join-Path $PSScriptRoot 'launcher\IPConflictMonitor.Updater.cs')
)

foreach ($path in @($config, $manifest, $readme) + $sources) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Arquivo necessario ausente: $path" }
}

$configuration = Get-Content -LiteralPath $config -Raw | ConvertFrom-Json
if ($configuration.Monitoring.DetectionMode -ne 'StrictEvidence' -or
    -not $configuration.Monitoring.RequireCapturedArpRequest -or
    -not $configuration.Monitoring.RequireCorrelatedArpResponses -or
    -not $configuration.Monitoring.FailClosedWithoutCapture) {
    throw 'STRICT DETECTION SAFETY DISABLED na configuracao de build.'
}

$strictText = Get-Content -LiteralPath $strictSource -Raw
$nativeText = Get-Content -LiteralPath $nativeSource -Raw
$guiText = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'launcher\IPConflictMonitor.NeonGui.cs') -Raw
$updaterText = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'launcher\IPConflictMonitor.Updater.cs') -Raw
$updateExperienceText = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'launcher\IPConflictMonitor.UpdateExperience.cs') -Raw
if (($strictText | Select-String -Pattern 'decision\.State\s*=\s*StrictDetectionState\.CONFIRMED' -AllMatches).Matches.Count -ne 1) {
    throw 'Regression gate: deve existir exatamente um caminho decisorio que atribui CONFIRMED.'
}
foreach ($forbidden in @('MinMacTransitions', 'MinObservationsPerMac', 'status = "SUSPECT"', 'row.Status == "SUSPECT"')) {
    if (($nativeText + $guiText).IndexOf($forbidden, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
        throw "Regression gate: regra/estado proibido encontrado: $forbidden"
    }
}
foreach ($required in @('EvaluateConflict', 'PairCandidates', 'RequireCapturedArpRequest', 'RequireCorrelatedArpResponses', 'FailClosedWithoutCapture', 'RequiredConfirmedCycles', 'ClearDynamicNeighbors', 'CalculateDiscoveryCaptureSeconds', 'packet.TargetMac', 'excludedMacs.Contains(mac)', 'entry.NativeRow.dwType != 3', 'MONITORING_LIMITED', 'UNVERIFIED')) {
    if (($strictText + $nativeText).IndexOf($required, [StringComparison]::Ordinal) -lt 0) {
        throw "Regression gate: marcador Strict Evidence ausente: $required"
    }
}
foreach ($requiredUpdateMarker in @('UpdateExperiencePage', 'GetControlFromPosition(1, 0)', 'ShowFailure', 'ShowRestarting', 'expectedSize', 'output.Flush(true)', '.previous')) {
    if (($updaterText + $updateExperienceText).IndexOf($requiredUpdateMarker, [StringComparison]::Ordinal) -lt 0) {
        throw "Regression gate: marcador de atualização assistida ausente: $requiredUpdateMarker"
    }
}
if ($updaterText.IndexOf('MessageBox.Show', [StringComparison]::Ordinal) -ge 0) { throw 'Regression gate: o fluxo de atualização não pode usar diálogos nativos.' }

$compiler = @(
    "$env:WINDIR\Microsoft.NET\Framework64\v4.0.30319\csc.exe",
    "$env:WINDIR\Microsoft.NET\Framework\v4.0.30319\csc.exe"
) | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if (-not $compiler) { throw 'Compilador C# do .NET Framework nao encontrado.' }

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$configOutput = Join-Path $OutputDirectory 'config'
New-Item -ItemType Directory -Path $configOutput -Force | Out-Null
$exe = Join-Path $OutputDirectory 'IPConflictMonitor.exe'
$preview = Join-Path $OutputDirectory 'IPConflictMonitor-dashboard.png'
$updatePreview = Join-Path $OutputDirectory 'IPConflictMonitor-update.png'
$zip = Join-Path $OutputDirectory 'IPConflictMonitor-Windows.zip'
$hashFile = Join-Path $OutputDirectory 'IPConflictMonitor.exe.sha256'
foreach ($artifact in @($exe, $preview, $updatePreview, $zip, $hashFile)) {
    if (Test-Path -LiteralPath $artifact) { Remove-Item -LiteralPath $artifact -Force }
}

$compilerArguments = @(
    '/nologo', '/target:exe', '/platform:anycpu', '/optimize+', '/debug-',
    '/reference:System.Windows.Forms.dll',
    '/reference:System.Drawing.dll',
    '/reference:System.Core.dll',
    '/reference:System.Web.Extensions.dll',
    '/reference:System.IO.Compression.dll',
    '/reference:System.IO.Compression.FileSystem.dll',
    "/out:$exe", "/win32manifest:$manifest", "/resource:$config,IPConflictMonitor.DefaultConfig.json"
) + $sources

& $compiler @compilerArguments
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $exe)) { throw "Compilacao falhou com codigo $LASTEXITCODE." }

Write-Host 'Executando Strict Detection Self-Test...' -ForegroundColor Cyan
$selfTest = & $exe -SelfTestDetection | Out-String
$selfTest | Write-Host
if ($LASTEXITCODE -ne 0 -or $selfTest -notmatch '(?m)^24 passed\s*$' -or $selfTest -notmatch '(?m)^0 failed\s*$') {
    throw 'BUILD ABORTADO: o Strict Detection Self-Test nao aprovou os 24 cenarios.'
}

Copy-Item -LiteralPath $config -Destination (Join-Path $configOutput 'config.json') -Force
Copy-Item -LiteralPath $readme -Destination (Join-Path $OutputDirectory 'README.md') -Force
& $exe -ValidateConfiguration -ConfigPath (Join-Path $configOutput 'config.json')
if ($LASTEXITCODE -ne 0) { throw "Validacao de configuracao falhou com codigo $LASTEXITCODE." }

$help = & $exe -Help | Out-String
$status = & $exe -Status | Out-String
if ($help -notmatch 'Strict Evidence Detection' -or $status -notmatch 'fail-closed') { throw 'O executavel nao confirmou a politica Strict Evidence.' }
if ((Get-Item -LiteralPath $exe).VersionInfo.FileVersion -ne '3.3.1.0') { throw 'A versao compilada nao e 3.3.1.0.' }

$assembly = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes($exe))
$resources = $assembly.GetManifestResourceNames()
if (@($resources).Count -ne 1 -or $resources[0] -ne 'IPConflictMonitor.DefaultConfig.json') { throw "Recursos inesperados no EXE: $($resources -join ', ')" }

function Test-ByteSequence {
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
$bytes = [IO.File]::ReadAllBytes($exe)
foreach ($indicator in @('ExecutionPolicy', 'powershell.exe', 'schtasks.exe')) {
    $asciiIndicator = [Text.Encoding]::ASCII.GetBytes($indicator)
    $unicodeIndicator = [Text.Encoding]::Unicode.GetBytes($indicator)
    if ((Test-ByteSequence $bytes $asciiIndicator) -or (Test-ByteSequence $bytes $unicodeIndicator)) {
        throw "Indicador legado encontrado no EXE: $indicator"
    }
}
foreach ($marker in @('ANALISAR REDE', 'STRICT_PROOF', '-SelfTestDetection', '-CheckUpdate', '-ApplyUpdate', '-UpdateScreenshot', 'UpdateExperiencePage', 'IPConflictMonitor-Windows.zip')) {
    $asciiMarker = [Text.Encoding]::ASCII.GetBytes($marker)
    $unicodeMarker = [Text.Encoding]::Unicode.GetBytes($marker)
    if (-not (Test-ByteSequence $bytes $asciiMarker) -and -not (Test-ByteSequence $bytes $unicodeMarker)) {
        throw "Marcador obrigatorio ausente no EXE: $marker"
    }
}

& $exe -GuiScreenshot $preview
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $preview)) { throw 'A renderizacao da interface falhou.' }
& $exe -UpdateScreenshot $updatePreview
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $updatePreview)) { throw 'A renderizacao da pagina de atualizacao falhou.' }

if (-not [string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
    $certificate = Get-ChildItem -Path Cert:\CurrentUser\My, Cert:\LocalMachine\My -CodeSigningCert |
        Where-Object Thumbprint -eq $CertificateThumbprint | Select-Object -First 1
    if (-not $certificate) { throw "Certificado de assinatura nao encontrado: $CertificateThumbprint" }
    $signed = Set-AuthenticodeSignature -LiteralPath $exe -Certificate $certificate -HashAlgorithm SHA256
    if ($signed.Status -notin @('Valid', 'NotTrusted')) { throw "Falha ao assinar: $($signed.StatusMessage)" }
}

Compress-Archive -LiteralPath $exe, $configOutput, (Join-Path $OutputDirectory 'README.md') -DestinationPath $zip -CompressionLevel Optimal
$hash = Get-FileHash -LiteralPath $exe -Algorithm SHA256
"$($hash.Hash.ToLowerInvariant())  IPConflictMonitor.exe" | Set-Content -LiteralPath $hashFile -Encoding ASCII
$signature = Get-AuthenticodeSignature -LiteralPath $exe
Write-Host "Executavel portatil: $exe" -ForegroundColor Green
Write-Host "Pacote: $zip" -ForegroundColor Green
Write-Host "Preview da atualização: $updatePreview" -ForegroundColor Cyan
Write-Host "SHA-256: $($hash.Hash)" -ForegroundColor Cyan
Write-Host "Self-Test: 24 passed / 0 failed" -ForegroundColor Green
Write-Host "Regression Gate: PASS" -ForegroundColor Green
Write-Host "Assinatura: $($signature.Status)" -ForegroundColor $(if ($signature.Status -eq 'Valid') { 'Green' } else { 'Yellow' })


