#requires -Version 5.1
[CmdletBinding()]
param(
    [string]$OutputDirectory = '',
    [string]$CertificateThumbprint = '',
    [switch]$SkipExecutableValidation
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $PSScriptRoot 'dist'
}

$config = Join-Path $PSScriptRoot 'config\config.json'
$manifest = Join-Path $PSScriptRoot 'launcher\app.portable.manifest'
$readme = Join-Path $PSScriptRoot 'README.md'
$sources = @(
    (Join-Path $PSScriptRoot 'launcher\IPConflictMonitor.PortableLauncher.3_1.cs'),
    (Join-Path $PSScriptRoot 'launcher\IPConflictMonitor.NativeMonitor.cs'),
    (Join-Path $PSScriptRoot 'launcher\IPConflictMonitor.NeonGui.cs'),
    (Join-Path $PSScriptRoot 'launcher\IPConflictMonitor.Updater.cs')
)

foreach ($path in @($config, $manifest, $readme) + $sources) {
    if (-not (Test-Path -LiteralPath $path)) { throw "Arquivo necessario ausente: $path" }
}
[void](Get-Content -LiteralPath $config -Raw | ConvertFrom-Json)

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
$zip = Join-Path $OutputDirectory 'IPConflictMonitor-Windows.zip'
$hashFile = Join-Path $OutputDirectory 'IPConflictMonitor.exe.sha256'
foreach ($artifact in @($exe, $preview, $zip, $hashFile)) {
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
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $exe)) {
    throw "Compilacao falhou com codigo $LASTEXITCODE."
}

if (-not [string]::IsNullOrWhiteSpace($CertificateThumbprint)) {
    $certificate = Get-ChildItem -Path Cert:\CurrentUser\My, Cert:\LocalMachine\My -CodeSigningCert |
        Where-Object Thumbprint -eq $CertificateThumbprint |
        Select-Object -First 1
    if (-not $certificate) { throw "Certificado de assinatura nao encontrado: $CertificateThumbprint" }
    $signed = Set-AuthenticodeSignature -LiteralPath $exe -Certificate $certificate -HashAlgorithm SHA256
    if ($signed.Status -notin @('Valid', 'NotTrusted')) { throw "Falha ao assinar: $($signed.StatusMessage)" }
}

Copy-Item -LiteralPath $config -Destination (Join-Path $configOutput 'config.json') -Force
Copy-Item -LiteralPath $readme -Destination (Join-Path $OutputDirectory 'README.md') -Force

if (-not $SkipExecutableValidation) {
    & $exe -ValidateConfiguration -ConfigPath (Join-Path $configOutput 'config.json')
    if ($LASTEXITCODE -ne 0) { throw "Validacao de configuracao falhou com codigo $LASTEXITCODE." }

    $help = & $exe -Help | Out-String
    $status = & $exe -Status | Out-String
    if ($help -notmatch 'sem PowerShell' -or $status -notmatch 'C# nativo') {
        throw 'O executavel nao confirmou o modo nativo e portatil.'
    }
    if ((Get-Item -LiteralPath $exe).VersionInfo.FileVersion -ne '3.1.0.0') {
        throw 'A versao compilada nao e 3.1.0.0.'
    }

    $assembly = [Reflection.Assembly]::Load([IO.File]::ReadAllBytes($exe))
    $resources = $assembly.GetManifestResourceNames()
    if (@($resources).Count -ne 1 -or $resources[0] -ne 'IPConflictMonitor.DefaultConfig.json') {
        throw "Recursos inesperados no EXE: $($resources -join ', ')"
    }

    $bytes = [IO.File]::ReadAllBytes($exe)
    $ascii = [Text.Encoding]::ASCII.GetString($bytes)
    $unicode = [Text.Encoding]::Unicode.GetString($bytes)
    foreach ($indicator in @('ExecutionPolicy', 'powershell.exe', 'schtasks.exe')) {
        if ($ascii.IndexOf($indicator, [StringComparison]::OrdinalIgnoreCase) -ge 0 -or
            $unicode.IndexOf($indicator, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw "Indicador legado encontrado no EXE: $indicator"
        }
    }
    foreach ($marker in @('ANALISAR REDE', 'TELEMETRIA EM TEMPO REAL', '-CheckUpdate', '-ApplyUpdate', 'IPConflictMonitor-Windows.zip')) {
        if ($ascii.IndexOf($marker, [StringComparison]::Ordinal) -lt 0 -and
            $unicode.IndexOf($marker, [StringComparison]::Ordinal) -lt 0) {
            throw "Marcador obrigatorio ausente no EXE: $marker"
        }
    }

    & $exe -GuiScreenshot $preview
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $preview)) {
        throw 'A renderizacao da interface falhou.'
    }
}

Compress-Archive -LiteralPath $exe, $configOutput, (Join-Path $OutputDirectory 'README.md') -DestinationPath $zip -CompressionLevel Optimal
$hash = Get-FileHash -LiteralPath $exe -Algorithm SHA256
"$($hash.Hash.ToLowerInvariant())  IPConflictMonitor.exe" | Set-Content -LiteralPath $hashFile -Encoding ASCII
$signature = Get-AuthenticodeSignature -LiteralPath $exe

Write-Host "Executavel portatil: $exe" -ForegroundColor Green
Write-Host "Pacote: $zip" -ForegroundColor Green
Write-Host "SHA-256: $($hash.Hash)" -ForegroundColor Cyan
Write-Host "Assinatura: $($signature.Status)" -ForegroundColor $(if ($signature.Status -eq 'Valid') { 'Green' } else { 'Yellow' })
