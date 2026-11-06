# SsmsQuickTools

Extensión VSIX para SQL Server Management Studio **22.6.0 en adelante**.

> **Aviso:** SSMS 21+ está construido sobre el shell de VS2022 y acepta instalar `.vsix`, pero Microsoft
> **no da soporte oficial** a extensiones de terceros. Una actualización de SSMS puede romper esta extensión.

## Funcionalidades

1. **Quick Connect**: combo "Quick Connections" en toolbar para reconectar la ventana de query activa a una conexión (servidor + base) definida por nombre en un archivo de configuración local.
2. **Grid → Script** (`Ctrl+K, Ctrl+1`): copia el resultado de una consulta al portapapeles como uno o más statements `INSERT INTO XXXXXXXX SELECT * FROM (VALUES ...) v (...)`, listos para pegar sobre la tabla destino real (reemplazando `XXXXXXXX`) y ejecutar. Si no puede leer el grid directamente, usa el portapapeles como respaldo (requiere la opción de SSMS "incluir encabezados de columna al copiar").
3. **Generar CREATE** (`Ctrl+K, Ctrl+2`) **/ Generar ALTER** (`Ctrl+K, Ctrl+3`): comandos en el menú **Quick Tools** (barra principal) que scriptean el objeto bajo el cursor o la selección. ALTER no está disponible para tablas.
4. **Copiar selección como XML Spreadsheet** (`Ctrl+K, Ctrl+4`): copia la selección del grid de resultados al portapapeles en formato XML Spreadsheet (Excel), preservando tipo y precisión.
5. **Auto Replacement**: escribir un token corto y presionar Enter, Tab o Espacio en el editor de query lo reemplaza por un snippet SQL configurado en `%APPDATA%\SsmsQuickTools\autoreplacement.xml`.
6. **Copiar selección como lista separada por coma** (`Ctrl+K, Ctrl+7`): copia las celdas seleccionadas del grid como una línea `a, b, c` por columna (números sin comillas, texto `N'...'`; omite NULL y duplicados), lista para pegar en un `IN (...)`.
7. **Copiar encabezados (todos / seleccionados)**: copia los nombres de columna del grid como `[A], [B], [C]` (un `]` en el nombre se duplica). "All" copia todas las columnas; "Selected" solo las que tienen celdas seleccionadas (avisa si no hay selección). Sin atajo de teclado.
8. **Locate in Object Explorer** (`Ctrl+K, Ctrl+6`) — *experimental*: ubica y selecciona en el árbol de Object Explorer el objeto bajo el cursor o la selección de la query activa. Puede no encontrar el objeto.
9. **About**: muestra versión y fecha de compilación de la extensión.

## Dónde están los comandos

- **Menú Quick Tools** (barra principal de SSMS):
  - Submenú **Query**: Script Object as CREATE, Script Object as ALTER, Locate in Object Explorer.
  - Submenú **Results Grid**: Copy Selection as Script SELECT, Copy Selection as SpreadSheet (Excel), Copy Selection as Comma List, Copy All Headers, Copy Selected Headers.
  - About.
- **Click derecho en el grid de resultados**: los comandos de Results Grid (Script SELECT, SpreadSheet, Comma List, All/Selected Headers).
- **Toolbar "SSMS Quick Tools"**: combo Quick Connections.

El menú contextual del editor de query no admite comandos de terceros, por eso los comandos de Query viven solo en el menú Quick Tools.

| Atajo | Comando |
| ----- | ------- |
| `Ctrl+K, Ctrl+1` | Grid → Script |
| `Ctrl+K, Ctrl+2` | Generar CREATE |
| `Ctrl+K, Ctrl+3` | Generar ALTER |
| `Ctrl+K, Ctrl+4` | Copiar como XML Spreadsheet |
| `Ctrl+K, Ctrl+6` | Locate in Object Explorer (experimental) |
| `Ctrl+K, Ctrl+7` | Copiar selección como lista separada por coma |

Tras instalar o actualizar la extensión, reiniciar SSMS por completo: la barra de menú se cachea.

## Requisitos de desarrollo

- Visual Studio 2022 (17.14+) con el workload **Visual Studio extension development**.
- SSMS 22.6.0 o superior instalado localmente (para referenciar sus ensamblados y para depurar).
- .NET Framework 4.8 Developer Pack.

## Estructura

```
SsmsQuickTools.sln
SsmsQuickTools/            Proyecto VSIX principal
SsmsQuickTools.Tests/      Tests unitarios (xUnit) de lógica pura
tools/                     Script de desinstalación (Uninstall-SsmsQuickTools.ps1)
lib/ssms22.6/              Ensamblados de SSMS usados como referencia (no versionado, ver "Ensamblados de SSMS")
```

## Ensamblados de SSMS

Los DLL de SSMS son propietarios y no se versionan (`lib/` está en `.gitignore`). Antes de compilar,
crear `lib/ssms22.6/` y copiar ahí, desde una instalación de SSMS **22.6.0**
(`Common7\IDE` y `Common7\IDE\Extensions\Application`), estos ensamblados:

- `Microsoft.Data.SqlClient`
- `Microsoft.SqlServer.Smo`
- `Microsoft.SqlServer.ConnectionInfo`
- `Microsoft.SqlServer.ConnectionInfoExtended`
- `Microsoft.SqlServer.Management.Sdk.Sfc`
- `Microsoft.SqlServer.GridControl`
- `SQLEditors`
- `SqlWorkbench.Interfaces`
- `Microsoft.SqlServer.RegSvrEnum`
- `ObjectExplorer`

Usar los de la versión **más baja soportada (22.6.0)**, no los de una versión más nueva: .NET Framework
enlaza por nombre fuerte, y una referencia compilada contra un ensamblado más nuevo no carga en una
instalación más vieja. Solo se usan para compilar; no se incluyen en el VSIX.

## Compilar

Abrir `SsmsQuickTools.sln` en Visual Studio 2022 y compilar, o desde línea de comandos:

```
msbuild SsmsQuickTools.sln -t:Restore
msbuild SsmsQuickTools.sln -t:Rebuild -p:Configuration=Release
```

El VSIX resultante queda en `SsmsQuickTools\bin\Release\net48\SsmsQuickTools.vsix` (o `bin\Debug\...` en Debug).

## Tests

```
dotnet test SsmsQuickTools.Tests
```

Cubren solo la lógica pura sin dependencia de SSMS (armado de scripts VALUES, parseo de nombres,
XML Spreadsheet, lista por coma, encabezados y Auto Replacement). Lectura del grid, scripting SMO,
cambio de conexión y el filtro de teclado del editor se verifican a mano contra una instancia real de SSMS.

## Depurar

Propiedades del proyecto → Debug → Start external program → `Ssms.exe` (ruta de tu instalación).
El `.csproj` ya trae `StartProgram`/`StartArguments` configurados para
`C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE\Ssms.exe /rootsuffix Exp`;
ajustar la ruta si SSMS está en otra ubicación.

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
    { "name": "DEV01.BASE_DATOS1", "server": "SERVIDOR1\\DEV01", "database": "BASE_DATOS1" },
    { "name": "DEV01.BASE_DATOS2", "server": "SERVIDOR2\\DEV01", "database": "BASE_DATOS2" }
  ]
}
```

Solo autenticación de Windows; el archivo no admite usuario/password.

## Configurar Auto Replacement

Editar `%APPDATA%\SsmsQuickTools\autoreplacement.xml` (se crea con un ejemplo si no existe al
iniciar SSMS). Cada `<AutoReplacement>` admite uno o varios `<Token>` (alias) que expanden al mismo
texto al presionar Enter o Tab en el editor de query:

```xml
<?xml version="1.0" encoding="utf-8"?>
<AutoReplacements>

	<AutoReplacement>
		<Token>cm</Token>
		<CaseSensitive>false</CaseSensitive>
		<Name>Cola Mensajes</Name>
		<Replacement>
SELECT TOP 200 * FROM dbo.cola_mensajes WITH(NOLOCK) WHERE 1=1 
-- AND id_linea = 11111111 
-- AND fecha_creacion > #'XXXXXX' 
-- AND id_tipo_mensaje IN ('XX') 
-- AND estado_n3 IN (3) 
ORDER BY id_mensaje DESC 
		</Replacement>
		<SelectReplacement>false</SelectReplacement>
		<CursorPositionMarker>#</CursorPositionMarker>
	</AutoReplacement>
	<AutoReplacement>
		<Token>sl</Token>
		<CaseSensitive>false</CaseSensitive>
		<Name>Select top</Name>
		<Replacement>
SELECT TOP 200 * FROM # 
		</Replacement>
		<SelectReplacement>false</SelectReplacement>
		<CursorPositionMarker>#</CursorPositionMarker>
	</AutoReplacement>
</AutoReplacements>
```

`CursorPositionMarker` indica el carácter que marca dónde queda el cursor tras expandir (se borra del
texto); si no aparece en `Replacement`, el cursor queda al final. `SelectReplacement=true` selecciona
todo el texto insertado en lugar de posicionar el cursor. No expande dentro de cadenas ni comentarios
SQL. Se dispara solo con Enter, Tab o Espacio (la tecla se consume al expandir); no hay comando de menú.
