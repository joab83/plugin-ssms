# SsmsQuickTools

Extensión VSIX para SQL Server Management Studio **22.6.0 en adelante**.

## Funcionalidades

1. **Quick Connect**: combo "Quick Connections" en toolbar para reconectar la ventana de query activa a una conexión (servidor + base) definida por nombre en un archivo de configuración local.
2. **Grid → Script**: copia el resultado de una consulta al portapapeles como uno o más statements `INSERT INTO XXXXXXXX SELECT * FROM (VALUES ...) v (...)`, listos para pegar sobre la tabla destino real (reemplazando `XXXXXXXX`) y ejecutar.
3. **Generar CREATE / Generar ALTER**: comandos en el menú **Quick Tools** (barra principal) que scriptean el objeto bajo el cursor o la selección.
4. **Copiar selección como XML Spreadsheet**: copia la selección del grid de resultados al portapapeles en formato XML Spreadsheet (Excel), preservando tipo y precisión.
5. **Auto Replacement**: escribir un token corto y presionar Enter en el editor de query lo reemplaza por un snippet SQL configurado en `%APPDATA%\SsmsQuickTools\autoreplacement.xml`.

Ver el plan de diseño completo en `docs/PLAN.md`.

## Requisitos de desarrollo

- Visual Studio 2022 (17.14+) con el workload **Visual Studio extension development**.
- SSMS 22.6.0 o superior instalado localmente (para referenciar sus ensamblados y para depurar).
- .NET Framework 4.8 Developer Pack.

## Estructura

```
SsmsQuickTools.sln
SsmsQuickTools/            Proyecto VSIX principal
SsmsQuickTools.Tests/      Tests unitarios (xUnit) de lógica pura
lib/ssms22.6/              Ensamblados de SSMS 22.6.0 usados como referencia (no redistribuibles, ver lib/ssms22.6/README.md)
```

## Compilar

Abrir `SsmsQuickTools.sln` en Visual Studio 2022 y compilar. El VSIX resultante queda en
`SsmsQuickTools\bin\Release\net48\SsmsQuickTools.vsix` (o `bin\Debug\...` en Debug).

## Depurar

Propiedades del proyecto → Debug → Start external program → `Ssms.exe` (ruta de tu instalación).
Ver detalle en `docs/PLAN.md`.

## Instalar

Doble clic en el `.vsix` generado, o Extensions → Manage Extensions en SSMS.

## Desinstalar

Cerrar SSMS y ejecutar `Uninstall-SsmsQuickTools.ps1`. El build lo copia junto al `.vsix`
(`SsmsQuickTools\bin\Release\net48\`); el original está en `tools\`.

```
powershell -ExecutionPolicy Bypass -File Uninstall-SsmsQuickTools.ps1
```

El script busca `VSIXInstaller.exe` de SSMS 22 (con `vswhere` y, si no, en la ruta estándar) y
desinstala la extensión. Si `VSIXInstaller` falla o deja restos, borra las carpetas de la extensión
bajo `%LocalAppData%\Microsoft\SSMS\<version>\Extensions\`. Si SSMS está instalado en otra ubicación,
indicar su raíz (la carpeta que contiene `Common7\IDE`):

```
powershell -ExecutionPolicy Bypass -File Uninstall-SsmsQuickTools.ps1 -SsmsPath "D:\SSMS22\Release"
```

Códigos de salida:

| Código | Significado |
| ------ | ----------- |
| `0` | Desinstalada (o ya no estaba instalada) |
| `1` | SSMS está en ejecución; no se modificó nada |
| `2` | No se encontró `VSIXInstaller.exe` (usar `-SsmsPath`) |
| `3` | La extensión sigue presente tras desinstalar y limpiar |

Si SSMS se instaló para todos los usuarios, puede hacer falta ejecutar PowerShell como administrador.
Los datos de usuario en `%APPDATA%\SsmsQuickTools\` (`connections.json`, `autoreplacement.xml`) nunca se
borran; para eliminarlos, hacerlo a mano.

## Configurar Quick Connect

Editar `%APPDATA%\SsmsQuickTools\connections.json` (se crea con un ejemplo si no existe al iniciar SSMS).
Lista plana de conexiones con nombre, cada una con su servidor y base:

```json
{
  "connections": [
    { "name": "DEV01.Figuritas", "server": "LENOVOJOSE\\DEV01", "database": "Figuritas" },
    { "name": "DEV01.ClinicaTurnos", "server": "LENOVOJOSE\\DEV01", "database": "ClinicaTurnos" }
  ]
}
```

Solo autenticación de Windows; el archivo no admite usuario/password.

## Configurar Auto Replacement

Editar `%APPDATA%\SsmsQuickTools\autoreplacement.xml` (se crea con un ejemplo si no existe al
iniciar SSMS). Cada `<AutoReplacement>` admite uno o varios `<Token>` (alias) que expanden al mismo
texto al presionar Enter en el editor de query:

```xml
<?xml version="1.0" encoding="utf-8"?>
<AutoReplacements>

  <AutoReplacement>
    <Token>cm</Token>
    <Token>colamen</Token>
    <CaseSensitive>false</CaseSensitive>
    <Name>Cola Mensajes</Name>
    <Replacement>SELECT TOP 200 * FROM dbo.cola_mensajes_n3 WITH(NOLOCK) WHERE 1=1 #
-- AND id_linea = 11111111
ORDER BY id_mensaje DESC</Replacement>
    <SelectReplacement>false</SelectReplacement>
    <CursorPositionMarker>#</CursorPositionMarker>
  </AutoReplacement>

</AutoReplacements>
```

`CursorPositionMarker` indica el carácter que marca dónde queda el cursor tras expandir (se borra del
texto); si no aparece en `Replacement`, el cursor queda al final. `SelectReplacement=true` selecciona
todo el texto insertado en lugar de posicionar el cursor. No expande dentro de cadenas ni comentarios
SQL. Si el filtro de Enter no se engancha en alguna instalación, la misma expansión está disponible
como comando manual "Expandir token" (Quick Tools, `Ctrl+K, Ctrl+5`).
