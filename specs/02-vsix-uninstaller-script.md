# 02 - Desinstalador del VSIX (script PowerShell)

**Estado:** Aprobado
**Depende de:** ninguno
**Fecha:** 2026-10-01

**Objetivo:** agregar un script PowerShell `Uninstall-SsmsQuickTools.ps1` que desinstala la extension SsmsQuickTools de SSMS 22.6+ (via `VSIXInstaller.exe`, con limpieza manual como fallback) y se distribuye junto al `.vsix` en la salida del build.

## Alcance

**Incluido:**
- Script nuevo `tools/Uninstall-SsmsQuickTools.ps1`, ejecutable con `powershell -ExecutionPolicy Bypass -File ...`.
- Desinstalacion via `VSIXInstaller.exe /uninstall:<VSIX Id>` usando el `Identity/@Id` de `source.extension.vsixmanifest` (`SsmsQuickTools.a1c9e6a2-6b7a-4a3e-9b1d-7e6f2f4a9c10`).
- Autodeteccion de `VSIXInstaller.exe` de SSMS 22: primero `vswhere.exe` (en `%ProgramFiles(x86)%\Microsoft Visual Studio\Installer\`), luego ruta estandar `C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE\VSIXInstaller.exe`. Parametro opcional `-SsmsPath` para sobrescribir la raiz de SSMS.
- Deteccion de `Ssms.exe` en ejecucion: el script aborta con mensaje claro (exit code distinto de 0) antes de tocar nada.
- Verificacion posterior: confirma que la carpeta de la extension ya no existe bajo `%LocalAppData%\Microsoft\SSMS\<version_id>\Extensions\` (se busca por el `Id` del VSIX en cada `extension.vsixmanifest`/`manifest` instalado).
- Fallback de limpieza: si `VSIXInstaller.exe` falla o la verificacion posterior encuentra restos, el script borra la(s) carpeta(s) de la extension bajo `%LocalAppData%\Microsoft\SSMS\<version_id>\Extensions\` (cubre instalaciones rotas, ver CLAUDE.md).
- Los datos del usuario en `%APPDATA%\SsmsQuickTools\` (`connections.json`, `autoreplacement.xml`) **nunca** se tocan.
- Empaquetado: target MSBuild en `SsmsQuickTools.csproj` que copia el script al directorio de salida (`bin\<Configuration>\net48\`) junto al `.vsix`.
- Documentar uso en `README.md`.

**No incluido (fuera de este spec):**
- Comando "Uninstall" dentro del menu Quick Tools de SSMS (la extension no puede borrarse mientras corre; necesitaria proceso externo igual).
- Instalador/desinstalador `.exe` o `.msi` standalone.
- Opcion de borrar `%APPDATA%\SsmsQuickTools\` (ni siquiera con flag).
- Script de instalacion (`Install-...ps1`).
- Soporte para SSMS anterior a 22.6 o instalaciones multiples/side-by-side de SSMS 22 (se opera sobre la instalacion detectada).
- Limpieza de entradas en el registro o del hive experimental `Exp` (`/rootsuffix Exp`).

## Modelo de datos

No introduce estructuras de datos ni persistencia. Parametros del script:

```powershell
param(
    [string]$SsmsPath   # opcional: raiz de SSMS 22 (carpeta que contiene Common7\IDE)
)
```

Constantes internas: `$VsixId = 'SsmsQuickTools.a1c9e6a2-6b7a-4a3e-9b1d-7e6f2f4a9c10'`.

Codigos de salida: `0` = desinstalada (o ya no estaba instalada), `1` = SSMS en ejecucion, `2` = no se encontro `VSIXInstaller.exe`, `3` = restos de la extension tras desinstalar y limpiar.

## Plan de implementacion

1. **tools/Uninstall-SsmsQuickTools.ps1** — esqueleto con `param`, constante `$VsixId`, y chequeo de `Ssms.exe` en ejecucion (exit `1`). El sistema sigue compilando; el script ya es ejecutable y aborta correctamente.
2. Agregar deteccion de `VSIXInstaller.exe` (`-SsmsPath` > `vswhere` > ruta estandar); exit `2` si no aparece. Prueba manual: ejecutar y ver la ruta detectada impresa.
3. Agregar la llamada `VSIXInstaller.exe /quiet /uninstall:$VsixId` y captura del exit code de `VSIXInstaller`.
4. Agregar verificacion posterior (buscar el `Id` en `%LocalAppData%\Microsoft\SSMS\*\Extensions\*\extension.vsixmanifest`) y fallback de borrado de carpeta si `VSIXInstaller` fallo o quedaron restos; exit `3` si siguen existiendo.
5. **SsmsQuickTools.csproj** — target `CopyUninstallScript` (`AfterBuild`/`AfterTargets="Build"`) que copia `..\tools\Uninstall-SsmsQuickTools.ps1` a `$(OutDir)`. Prueba: `msbuild -t:Rebuild`, ver el `.ps1` junto al `.vsix`.
6. **README.md** — seccion "Desinstalar" con el comando de ejecucion, el parametro `-SsmsPath`, los codigos de salida y la nota de que `%APPDATA%\SsmsQuickTools\` se conserva.
7. **Verificacion manual** contra SSMS 22.6 real (no hay logica pura que amerite test unitario).

## Criterios de aceptacion

- [ ] `tools/Uninstall-SsmsQuickTools.ps1` existe y `msbuild SsmsQuickTools.sln -t:Rebuild -p:Configuration=Release` lo copia a `SsmsQuickTools\bin\Release\net48\` junto al `.vsix`.
- [ ] Con SSMS abierto, el script no modifica nada, imprime un mensaje pidiendo cerrar SSMS y sale con codigo `1`.
- [ ] Con SSMS cerrado y la extension instalada, el script la desinstala y sale con codigo `0`; al abrir SSMS el menu Quick Tools ya no aparece.
- [ ] Tras la desinstalacion, no queda ninguna carpeta bajo `%LocalAppData%\Microsoft\SSMS\*\Extensions\` cuyo manifest contenga el `Id` del VSIX.
- [ ] Con una instalacion rota (carpeta de la extension presente pero `VSIXInstaller` falla), el fallback borra la carpeta y el script sale con `0`.
- [ ] Con la extension ya desinstalada, el script sale con `0` e informa que no habia nada que desinstalar.
- [ ] `%APPDATA%\SsmsQuickTools\connections.json` y `autoreplacement.xml` existen sin cambios (mismo contenido) despues de correr el script.
- [ ] Con `-SsmsPath` apuntando a una ruta valida distinta, el script usa ese `VSIXInstaller.exe`; con una ruta invalida sale con codigo `2`.
- [ ] `README.md` documenta el comando y los codigos de salida.
- [ ] `dotnet test SsmsQuickTools.Tests` sigue pasando sin cambios.

## Decisiones tomadas y descartadas

- **Si:** script PowerShell externo. La extension no puede desinstalarse a si misma mientras SSMS la tiene cargada, y un script es lo mas simple de mantener y revisar.
- **No:** comando dentro del menu Quick Tools. Necesitaria lanzar igual un proceso externo y cerrar SSMS; mas complejidad sin beneficio.
- **No:** `.exe`/`.msi` standalone. Proyecto nuevo y pipeline de empaquetado para un caso que cubre un script.
- **Si:** `VSIXInstaller.exe /uninstall` como camino principal. Es el mecanismo soportado para quitar VSIX en el shell VS2022 y deja el estado de SSMS consistente.
- **Si:** fallback de borrado de carpeta. CLAUDE.md ya documenta instalaciones rotas bajo `%LocalAppData%\Microsoft\SSMS\<version_id>\Extensions\`; el script debe poder recuperarlas.
- **Si:** autodeteccion (`vswhere` + ruta estandar) con override `-SsmsPath`. Evita ruta hardcodeada que falle en instalaciones distintas.
- **No:** ruta fija unica. Falla si SSMS esta instalado en otra unidad/ubicacion.
- **Si:** conservar siempre `%APPDATA%\SsmsQuickTools\`. Evita perder `connections.json`/snippets del usuario; reinstalar recupera la configuracion. Borrarlos queda manual.
- **No:** flag para borrar datos de usuario. Decidido no ofrecer el camino destructivo en este spec.
- **Si:** abortar si `Ssms.exe` corre. `VSIXInstaller` no puede quitar la extension con SSMS abierto y el fallback de borrado de carpeta fallaria por archivos bloqueados.
- **Si:** verificacion posterior por `Id` en el manifest instalado, no solo por exit code. El exit code de `VSIXInstaller` no garantiza que no queden restos.
- **Si:** `Id` del VSIX como constante en el script. El script viaja junto al `.vsix`, sin acceso al manifest del repo en runtime.

## Riesgos identificados

- **`Id` duplicado en dos lugares.** El `Identity/@Id` vive en `source.extension.vsixmanifest` y en el script. Si cambia el manifest, el script desinstala nada. Mitigacion: nota en el script y en CLAUDE.md/README; el criterio de aceptacion de desinstalacion real lo detecta.
- **Estructura de `Extensions\` no documentada.** El nombre de carpeta (hash) bajo `%LocalAppData%\Microsoft\SSMS\<version_id>\Extensions\` puede cambiar entre versiones de SSMS; por eso se busca por `Id` en los manifests y no por nombre de carpeta.
- **`VSIXInstaller.exe` puede pedir elevacion o mostrar UI.** Se usa `/quiet`; si SSMS 22 lo instalo para todos los usuarios, puede requerir ejecutar PowerShell como administrador. Se documenta en el README.
- **Soporte no oficial.** Microsoft no soporta extensiones de terceros en SSMS 21+; un update de SSMS puede cambiar la ruta o el comportamiento de `VSIXInstaller`. El fallback de borrado de carpeta reduce el impacto.

## Lo que **no** esta en este spec

- Comando de desinstalar dentro de SSMS.
- Instalador/desinstalador `.exe` o `.msi`.
- Borrado de `%APPDATA%\SsmsQuickTools\` en ningun modo.
- Script de instalacion.
- Limpieza del registro o del hive experimental `Exp`.
