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
