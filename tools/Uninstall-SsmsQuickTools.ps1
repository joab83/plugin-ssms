<#
.SYNOPSIS
    Desinstala la extension SsmsQuickTools de SQL Server Management Studio 22.6+.

.DESCRIPTION
    No toca %APPDATA%\SsmsQuickTools\ (connections.json, autoreplacement.xml).

    Codigos de salida:
      0 = desinstalada (o ya no estaba instalada)
      1 = SSMS en ejecucion
      2 = no se encontro VSIXInstaller.exe
      3 = restos de la extension tras desinstalar y limpiar

.PARAMETER SsmsPath
    Opcional. Raiz de SSMS 22 (carpeta que contiene Common7\IDE).
#>
param(
    [string]$SsmsPath
)

$ErrorActionPreference = 'Stop'

# Debe coincidir con Identity/@Id de SsmsQuickTools\source.extension.vsixmanifest.
$VsixId = 'SsmsQuickTools.a1c9e6a2-6b7a-4a3e-9b1d-7e6f2f4a9c10'

if (Get-Process -Name 'Ssms' -ErrorAction SilentlyContinue) {
    Write-Host 'SSMS esta en ejecucion. Cerralo y volve a correr este script.' -ForegroundColor Red
    exit 1
}

# Devuelve la ruta de VSIXInstaller.exe bajo una raiz de SSMS, o $null si no existe.
function Get-VsixInstallerPath([string]$Root) {
    if (-not $Root) { return $null }
    $candidate = Join-Path $Root 'Common7\IDE\VSIXInstaller.exe'
    if (Test-Path -LiteralPath $candidate) { return $candidate }
    return $null
}

# Orden: -SsmsPath > vswhere > ruta estandar.
function Find-VsixInstaller([string]$OverridePath) {
    if ($OverridePath) { return Get-VsixInstallerPath $OverridePath }

    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path -LiteralPath $vswhere) {
        $roots = & $vswhere -products * -prerelease -property installationPath
        foreach ($root in $roots) {
            # Solo instalaciones de SSMS (VS comun tambien trae VSIXInstaller.exe).
            if (-not (Test-Path -LiteralPath (Join-Path $root 'Common7\IDE\Ssms.exe'))) { continue }
            $found = Get-VsixInstallerPath $root
            if ($found) { return $found }
        }
    }

    return Get-VsixInstallerPath 'C:\Program Files\Microsoft SQL Server Management Studio 22\Release'
}

$VsixInstaller = Find-VsixInstaller $SsmsPath
if (-not $VsixInstaller) {
    Write-Host 'No se encontro VSIXInstaller.exe. Usa -SsmsPath con la raiz de SSMS 22 (carpeta que contiene Common7\IDE).' -ForegroundColor Red
    exit 2
}
Write-Host "VSIXInstaller: $VsixInstaller"

Write-Host "Desinstalando $VsixId ..."
$proc = Start-Process -FilePath $VsixInstaller -ArgumentList '/quiet', "/uninstall:$VsixId" -Wait -PassThru
$InstallerExitCode = $proc.ExitCode
Write-Host "VSIXInstaller termino con codigo $InstallerExitCode"
