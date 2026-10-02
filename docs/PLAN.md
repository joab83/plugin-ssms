# Plugin SSMS 22 — Quick Connections, Script desde Grid, Generar ALTER/CREATE

## Contexto

El usuario quiere una extensión propia para SQL Server Management Studio 22.6.0 (y posteriores) con seis funcionalidades de productividad que hoy requieren pasos manuales repetitivos:

1. **Quick Connections**: un combo en la barra de herramientas para reconectar la ventana de query activa a un servidor/base de datos definidos en un archivo de configuración.
2. **Grid → script SELECT**: tomar el resultado visible de una consulta y dejar en el portapapeles un script autocontenido que reproduce esos datos al pegarlo y ejecutarlo.
3. **Generar ALTER / Generar CREATE**: al seleccionar el nombre de un objeto en el editor, generar el script correspondiente desde el menú **Quick Tools** (los menús contextuales de SSMS no fusionan grupos de terceros, ver M3/M5).
4. **Copiar resultado como XML Spreadsheet**: copiar la selección del grid al portapapeles en formato SpreadsheetML, que Excel pega con tipos y precisión preservados (M4).
5. **Auto Replacement**: expandir un token corto escrito en el editor (ej. `cm`) en un snippet SQL configurado por el usuario en `%APPDATA%\SsmsQuickTools\autoreplacement.xml` al presionar Enter. Es la única función que intercepta el tecleo del editor (M6).
6. **About**: ítem del menú **Quick Tools** que muestra versión de build, fecha de compilación y autor en un `MessageBox`; versión y fecha se generan en build (M5).


## Viabilidad — resumen

Es **viable**, con dificultad media-alta. SSMS 21+ está construido sobre el shell de Visual Studio 2022, es 64-bit, y acepta instalación de archivos `.vsix`. Confirmado por extensiones vivas que ya soportan SSMS 21/22 (`SqlCeToolbox`, `SSMS-Schema-Folders`).

Advertencias que deben quedar claras antes de empezar:

- Microsoft **no da soporte oficial** a extensiones de terceros en SSMS 21+. Funciona, pero cualquier feedback a Microsoft será cerrado. Una actualización de SSMS puede romper la extensión.
- Las funcionalidades 1 y 3 usan APIs internas de SSMS (`SQLEditors.dll`, `SqlWorkbench.Interfaces.dll`) documentadas solo parcialmente y a través de referencias directas a los DLL instalados.
- La funcionalidad 5 (Auto Replacement) no usa APIs internas de SSMS ni MEF: intercepta `VSStd2K.RETURN` con `IVsTextView.AddCommandFilter` (COM legacy del shell VS). Es la más invasiva —toca el tecleo del editor— pero su riesgo de ruptura es bajo; si el filtro deja de engancharse, queda el comando manual "Expandir token" (`Ctrl+K, Ctrl+5`). La funcionalidad 6 (About) no tiene dependencias de SSMS.
- La funcionalidad 2, con el enfoque elegido (**leer el grid por reflection**), depende de tipos internos no documentados (`Microsoft.SqlServer.Management.UI.Grid.GridControl`, `IGridStorage`). Es la parte más frágil del proyecto y la que más probablemente se rompa entre versiones. Se mitiga aislándola tras una interfaz con un fallback por portapapeles (TSV).
- El grid solo expone los valores **como texto**, no los tipos SQL. Los tipos del script generado se **infieren** por heurística sobre el texto. Aceptable para pegar y ejecutar; no es fiel al esquema original.

Esfuerzo estimado: M0+M1 en un par de sesiones; M2 es el riesgo real (iteración con SSMS abierto); M3 es directo.

## Decisiones tomadas

| Tema | Decisión |
|---|---|
| Origen de datos para el SELECT | Leer el grid de resultados por reflection |
| Formato del script | `INSERT INTO XXXXXXXX SELECT * FROM (VALUES (...),(...)) v(cols);` |
| Quick Connections | Reconectar la ventana de query activa (cambio de conexión + `USE database`) |
| Autenticación en el archivo | Solo Windows / Integrated (sin credenciales en el archivo) |

## Stack y configuración del proyecto

- Visual Studio 2022 (17.14+) con workload **Visual Studio extension development**.
- Plantilla **VSIX Project (VSPackage)**, formato VSIX v3.
- Target: **.NET Framework 4.8**, `PlatformTarget=AnyCPU`, `<Prefer32Bit>false</Prefer32Bit>` (necesario para que cargue también en Windows ARM64, donde el CLR es ARM64 nativo).
- Referencias a DLL de SSMS desde `C:\Program Files\Microsoft SQL Server Management Studio 22\Release\Common7\IDE\`, todas con `CopyLocal=false`:
  - `SQLEditors.dll` — `ServiceCache`, `IScriptFactory`, `UIConnectionInfo`
  - `SqlWorkbench.Interfaces.dll`
  - `Microsoft.SqlServer.Management.UI.Grid.dll` (o el ensamblado que contenga `GridControl` en la versión instalada; verificar en la instalación real)

**Versión mínima soportada: SSMS 22.6.0.** Referenciar los DLL de SSMS de la versión **más baja** soportada (22.6.0), no de la más alta: las referencias de .NET Framework se enlazan por nombre fuerte y una referencia a un ensamblado más nuevo no cargará sobre una instalación 22.6.0. Si la máquina de desarrollo tiene una versión superior, copiar los DLL de 22.6.0 a `lib\ssms22.6\` en el repositorio y referenciarlos desde ahí (`HintPath`, `CopyLocal=false`, `SpecificVersion=false`).

Además, `SpecificVersion=False` en todas las referencias a DLL de SSMS, para tolerar los incrementos de versión entre 22.6 y las siguientes.
  - `Microsoft.SqlServer.Smo.dll`, `Microsoft.SqlServer.ConnectionInfo.dll`, `Microsoft.SqlServer.Management.Sdk.Sfc.dll` (vía NuGet `Microsoft.SqlServer.SqlManagementObjects` si la versión es compatible; si no, referenciar las de SSMS)
- `source.extension.vsixmanifest`:

```xml
<Installation>
  <InstallationTarget Id="Microsoft.VisualStudio.Ssms" Version="[22.6,24.0)">
    <ProductArchitecture>amd64</ProductArchitecture>
  </InstallationTarget>
</Installation>
<Prerequisites>
  <Prerequisite Id="Microsoft.VisualStudio.Component.CoreEditor" Version="[17.0,)" DisplayName="Visual Studio core editor" />
</Prerequisites>
```

- Depuración: propiedades del proyecto → Debug → programa externo `Ssms.exe` con argumento `/rootsuffix Exp` si el hive experimental existe; si no, instalar el `.vsix` generado y adjuntar el depurador al proceso `Ssms.exe`.
- Las extensiones se instalan en `%LocalAppData%\Microsoft\SSMS\<version_id>\Extensions\` (ej. `22.0_8a60b991`). Útil para limpiar instalaciones fallidas.

## Estructura propuesta

```
plugin-ssms/
  SsmsQuickTools.sln
  SsmsQuickTools/
    SsmsQuickTools.csproj
    source.extension.vsixmanifest
    SsmsQuickToolsPackage.cs        // AsyncPackage, InitializeAsync, registro de comandos
    SsmsQuickTools.vsct             // toolbar, combos y menú contextual
    Ssms/
      SsmsHost.cs                   // acceso a ServiceCache, conexión activa, ventana activa
      GridReader.cs                 // reflection sobre GridControl  (aislado)
      ClipboardGridReader.cs        // fallback TSV
      IResultSetReader.cs           // interfaz común: columnas + filas de string
      TextViewEditor.cs             // helpers IVsTextView (leer/reemplazar/caret) para Auto Replacement
    Features/
      QuickConnect/
        ConnectionCatalog.cs        // carga/watch del archivo de configuración
        QuickConnectCommands.cs     // handler del combo unico "Quick Connections"
      ScriptData/
        ValuesScriptBuilder.cs      // inferencia de tipos + generación del INSERT
        ScriptDataCommand.cs
      ScriptObject/
        ObjectNameParser.cs         // parseo de [db].[schema].[obj] desde el texto seleccionado
        ObjectScripter.cs           // SMO / OBJECT_DEFINITION
        ScriptObjectCommands.cs
      CopyXmlSpreadsheet/
        XmlSpreadsheetBuilder.cs    // SpreadsheetML (lógica pura)
        ClipboardDataObject.cs      // IDataObject COM propio
      AutoReplacement/
        AutoReplacementCatalog.cs   // carga/watch de autoreplacement.xml
        TokenScanner.cs             // token pegado al cursor
        SqlContextScanner.cs        // léxico T-SQL: no expandir en cadenas/comentarios
        AutoReplacementExpander.cs  // CursorPositionMarker / SelectReplacement
        AutoReplacementCommandFilter.cs // IOleCommandTarget sobre VSStd2K.RETURN
        AutoReplacementService.cs   // engancha el filtro a cada vista
        ExpandTokenCommand.cs       // comando manual, Ctrl+K, Ctrl+5
      About/
        AboutCommand.cs             // MessageBox con BuildInfo.g.cs (generado en build)
  README.md
```

## Milestone 0 — Esqueleto que carga

1. Crear la solución y el proyecto VSIX con `AsyncPackage` y `ProvideAutoLoad(UIContextGuids80.NoSolution / EmptySolution)` — en SSMS nunca hay solución abierta, así que la carga debe dispararse por contexto de UI o por el propio comando.
2. Añadir un comando de prueba en el menú **Tools** que muestre un `MessageBox`.
3. Compilar, instalar el `.vsix` en SSMS 22.6.0 y verificar que aparece.

**Este hito valida el 80% del riesgo de plataforma.** No avanzar sin él.

## Milestone 1 — Quick Connections

Archivo de configuración en `%APPDATA%\SsmsQuickTools\connections.json`, recargado con `FileSystemWatcher`.
Lista plana de conexiones con nombre (un registro = un par servidor+base):

```json
{
  "connections": [
    { "name": "PROD.Ventas", "server": "sql-prod01\\INST1", "database": "Ventas" },
    { "name": "PROD.Facturacion", "server": "sql-prod01\\INST1", "database": "Facturacion" },
    { "name": "DEV.VentasDev", "server": "localhost", "database": "VentasDev" }
  ]
}
```

Solo autenticación integrada; no se almacenan credenciales.

En el `.vsct`, un combo único ("Quick Connections") en una toolbar propia:

```xml
<Combo guid="guidQuickToolsCmdSet" id="cmdidConnectionCombo" priority="0x0001"
       type="DropDownCombo" idCommandList="cmdidConnectionComboGetList"
       defaultWidth="220">
```

- Un handler responde con la lista (`OleMenuCmdEventArgs.OutValue` = `string[]`) y otro con la selección.
- Al seleccionar una conexión: `SsmsHost.TryReconnectActiveWindow` reconecta **in-place** la ventana de query
  activa, sin abrir una ventana nueva salvo que no haya ninguna. Verificado por IL
  (`ildasm` sobre `lib/ssms22.6/SQLEditors.dll`, SSMS 22.6.11806.211) contra
  `ScriptAndResultsEditorControl`/`SqlScriptEditorControl`:
  - **Mismo servidor** (compara `ServerName` + `AuthenticationType` con la conexión activa,
    `ISqlToolsWindowWithConnectionState.Connection`): solo cambia la propiedad pública
    `SqlScriptEditorControl.CurrentDB`, la misma vía que usa el combo de bases nativo de SSMS
    (`ChangeDatabase` internamente). No abre ninguna conexión ADO.NET propia.
  - **Servidor distinto, o ventana sin conectar**: si estaba conectada, primero `Disconnect()`
    (miembro `family`, declarado en `ScriptAndResultsEditorControl`) — sin esto,
    `ISqlScriptWindowWithConnection.SetConnection(info, dbConnection)` rechaza con
    `InvalidOperationException` ("no se puede cambiar la conexión cuando ya se está conectado",
    chequeo interno atado a `IsConnected`/`m_connection`, no a la propiedad pública). Luego
    `SetConnection(info, dbConnection)` con una conexión ADO.NET ya abierta por la extensión, y como
    mejor esfuerzo `OnScriptGotNewConnection(m_connectionInfoList, m_connection)` (miembro interno,
    también en la clase base) para refrescar barra de estado y combo de base nativos.
  - **Guardas antes de tocar la conexión**: `IsExecuting` (propiedad `family` de
    `ScriptAndResultsEditorControl`) y `SELECT @@TRANCOUNT` sobre la conexión activa; si hay una
    consulta en ejecución o una transacción abierta, se aborta con aviso sin desconectar nada (no se
    usan los diálogos de commit/rollback propios de SSMS).
  - Todos los miembros no públicos se resuelven por reflection con nombre en constante y
    `try/catch`, buscando el tipo de la jerarquía que los declara (`ScriptAndResultsEditorControl` o
    `SqlScriptEditorControl` según el caso) — igual criterio de aislamiento que `GridReader`.
  - **Sin ninguna ventana de query activa** (foco en Object Explorer, o ninguna ventana abierta): se
    abre una ventana nueva conectada (`ScriptFactory.Instance.CreateNewBlankScript`), único caso en
    que Quick Connect crea una ventana.

## Milestone 2 — Grid → script SELECT (parte de mayor riesgo)

Definir `IResultSetReader` que devuelve `(string[] columnas, IReadOnlyList<string[]> filas)`, con dos implementaciones:

**A. `GridReader` (principal, reflection).** Pasos:
1. Frame activo vía `IVsMonitorSelection` / `IVsUIShell.GetDocumentWindowEnum`; obtener el HWND con `__VSFPROPID.VSFPROPID_ParentHwnd`.
2. `Control.FromChildHandle(hwnd)` y recorrer el árbol de controles WinForms buscando el tipo cuyo nombre completo termine en `.GridControl`.
3. Sobre esa instancia, por reflection: `GridStorage` (`IGridStorage`) → `NumRows()` y `GetCellDataAsString(row, col)`; columnas desde `GetColumnInfo(...)` / la colección interna de columnas.
4. Si hay selección de celdas, limitarse a ese rango; si no, tomar el grid completo.
5. Todo el acceso por reflection va con nombres en constantes y `try/catch`; ante cualquier fallo se registra el motivo y se ofrece el fallback.

**B. `ClipboardGridReader` (fallback).** Parsea TSV del portapapeles (con la opción "incluir encabezados" de SSMS). Se usa automáticamente si A falla, con un aviso al usuario.

**Generación del script** (`ValuesScriptBuilder`): inferencia de tipo por columna, evaluando todos los valores no nulos y quedándose con el tipo más restrictivo que los acepte a todos: `bit` → `int` → `bigint` → `decimal(p,s)` → `uniqueidentifier` → `datetime2` → `nvarchar(n)` (fallback). `NULL` textual se mapea a `NULL` real. Escapado de comillas simples, prefijo `N'` para texto.

Salida en portapapeles:

```sql
INSERT INTO XXXXXXXX
    SELECT * FROM (VALUES
        (1, N'Ana', '2026-01-15'),
        (2, N'Luis', '2026-02-01')
    ) v ([Id], [Nombre], [Fecha]);
```

`XXXXXXXX` es un marcador literal (no un nombre citado con corchetes) que el usuario reemplaza a mano
por la tabla destino real antes de ejecutar. Sin `CAST` explícito: literales simples (números, `NULL`) y
con prefijo `N'...'`/comillas para texto, fecha y GUID. Límite configurable de filas (por defecto 1000)
con confirmación si se supera. `VALUES` admite hasta 1000 filas por constructor: por encima de eso, se
emite un `INSERT INTO XXXXXXXX SELECT * FROM (VALUES ...) v (...);` completo por cada bloque de 1000,
separados por línea en blanco, en vez de `UNION ALL`.

**Corrección verificada contra el motor (`SsmsQuickTools.Tests/ScriptRoundTripTests.cs`):** la inferencia de tipo del motor solo aplica a literales numéricos sin comillas (`int`/`bigint`/`decimal`/`bit`). Un literal entre comillas simples (`datetime2`, `uniqueidentifier`) **no se convierte** por sí solo — nada en un `SELECT` suelto fuerza esa conversión, así que la columna vuelve como `varchar`/`nvarchar`, no como fecha o GUID reales. Confirmado con `SQL_VARIANT_PROPERTY(..., 'BaseType')` contra `LENOVOJOSE\DEV01`. Con el `INSERT INTO` real (formato actual de este milestone) la conversión sí ocurre, pero la hace el motor **contra el tipo de la columna destino** al insertar, no el script: el riesgo se traslada del script en sí al esquema de la tabla que reciba el `INSERT` (una columna `datetime2`/`uniqueidentifier` mal tipada en destino puede rechazar el literal en vez de convertirlo en silencio). Aceptado como limitación conocida por ahora; no bloquea M2.

Comando expuesto en el menú contextual del grid de resultados y en **Tools**, con atajo de teclado.

## Milestone 3 — Generar ALTER / Generar CREATE

1. ~~Menú contextual del editor: grupo bajo `IDM_VS_CTXT_CODEWIN` con dos botones~~. **Descartado
   tras verificación empírica (2026-09-09) contra SSMS 22.6**: el editor de query de SSMS arma su
   menú contextual ("Ventana Código" en Personalizar → Comandos → Menú contextual) a mano, no
   fusiona grupos de terceros vía VSCT pese a aparecer como "customizable" en ese diálogo — se
   probó con `IDM_VS_CTXT_CODEWIN` (guid estándar de VS) y con `GUID_SQLEditorCommandSet` /
   `IDM_SQLWB_SQLSCRIPT_CONTEXT` (guid propio de `SQLEditors.dll`, `Microsoft.SqlServer.Management.UI.VSIntegration.Editors.SQLWorkbenchCommands`),
   ninguno se fusiona. En su lugar: "Generar CREATE" y "Generar ALTER" van en el menú **Tools**
   (mismo grupo que "Copiar resultado como script SELECT") con atajo de teclado
   (`Ctrl+K, Ctrl+2` / `Ctrl+K, Ctrl+3`; originalmente `Ctrl+Shift+C`/`Ctrl+Shift+A`, cambiado
   luego para no chocar con atajos de otras extensiones).
2. Texto seleccionado vía `IVsTextManager.GetActiveView` → `IVsTextView.GetSelectedText()`; si no hay selección, tomar la palabra bajo el cursor.
3. `ObjectNameParser` normaliza `[db].[schema].[obj]`, `schema.obj` u `obj` (con `dbo` y la base actual como defaults).
4. `ObjectScripter`, sobre la conexión activa:
   - Objetos programables (procedimiento, vista, función, trigger): `OBJECT_DEFINITION(OBJECT_ID(@name))`. Para ALTER, reemplazar el primer `CREATE` por `ALTER` con una expresión regular anclada al inicio del cuerpo (respeta comentarios previos).
   - Tablas: SMO `Scripter` con `ScriptingOptions { ScriptDrops = false, Indexes = true, DriAll = true, SchemaQualify = true }`. **No existe ALTER para una tabla**: en ese caso "Generar ALTER" se deshabilita y se avisa.
   - `IVsMonitorSelection` / `OleMenuCommand.BeforeQueryStatus` decide visibilidad y habilitación según lo que exista en el servidor (una consulta a `sys.objects` para resolver el tipo).
5. Resultado: nueva ventana de query con el script (`ScriptFactory.CreateNewBlankScript` + inserción de texto), y copia al portapapeles.

## Milestone 4 — Copy selection as XML Spreadsheet

**Estado: funcionando (2026-09-11, v0.1.10, confirmado contra SSMS 22.6 real).** Al pegar en Excel los
campos llegan formateados (encabezado en negrita, tipos preservados). Historial de los tres intentos de
escritura al portapapeles hasta llegar a esto:

1. **v0.1.7** — `System.Windows.Forms.DataObject.SetData("XML Spreadsheet", new MemoryStream(...))` +
   `Clipboard.SetDataObject`. Excel pegaba texto plano. Causa: el `DataObject` de WinForms expone un
   `Stream` via `TYMED_ISTREAM`, Excel pide `TYMED_HGLOBAL` — no matchea, Excel descarta el formato.
2. **v0.1.9** — reescrito con la API cruda de Win32 (`RegisterClipboardFormat` +
   `OpenClipboard`/`EmptyClipboard`/`GlobalAlloc`/`SetClipboardData`, portapapeles "global"). Sin excepción
   al ejecutar, pero **verificado con un inspector de portapapeles (2026-09-11) que el formato "XML
   Spreadsheet" ni siquiera queda escrito** — el inspector solo ve el `CF_UNICODETEXT` de fallback. El
   `SetClipboardData` del XML no devolvía error (si hubiera fallado, el código no habría llegado a escribir
   el texto: ver orden en el código de esa versión), así que el dato técnicamente entra a la tabla global
   de formatos, pero ni Excel ni el inspector lo ven — ambos consultan por el lado OLE
   (`OleGetClipboard`/`IDataObject`), no por `GetClipboardData` crudo.
3. **v0.1.10 — funciona.** Se reemplazó todo por un `IDataObject` COM propio (`ClipboardDataObject.cs`),
   publicado con `OleSetClipboard` + `OleFlushClipboard`. Dos formatos (`"XML Spreadsheet"` registrado y
   `CF_UNICODETEXT`), ambos ofrecidos explícitamente con `TYMED_HGLOBAL` vía
   `GetData`/`QueryGetData`/`EnumFormatEtc`. Confirmado: pegado en Excel con encabezado en negrita y tipos
   preservados. Era el mecanismo correcto — control total sobre qué TYMED se negocia, publicado por el
   canal OLE que Excel realmente consulta.

Pendiente todavía (no bloqueante, ver checklist en `## Verificación`): el resto del checklist de M4 —
tipos mixtos completos (bigint grande, decimal de alta precisión, ceros a la izquierda, fechas, NULL,
caracteres especiales), selección parcial de columnas, y confirmar el fallback TSV en Notepad.

Copiar con Ctrl+C / Ctrl+Shift+C desde el grid de resultados pega en Excel una representación de texto
(TSV): Excel tiene que adivinar el tipo de cada celda según la configuración regional del equipo. Daño
típico: `00123` pierde los ceros, `2026-03-04` se lee como 4 de marzo o 3 de abril según el locale,
`12.50` se confunde con fecha o entero, y los identificadores largos pierden dígitos de precisión.

Nuevo comando que copia la selección actual del grid al portapapeles en formato **XML Spreadsheet 2003**
(SpreadsheetML), que Excel pega de forma nativa con tipo explícito por celda: encabezados en negrita,
strings como strings, fechas como fechas, y numéricos/monetarios con su precisión y escala.

**Lectura del grid.** Reutiliza `GridReader` (`Ssms/GridReader.cs`), con **selección obligatoria**:
`GridReader.cs:68-79` ya calcula `hasSelection` desde `grid.SelectedCells` y llama
`grid.GetDataObject(hasSelection, true)`, pero `ResultSetData` (`Ssms/IResultSetReader.cs:9-19`) no expone
ese dato hacia afuera. Se agrega `bool HasSelection` a `ResultSetData` (`GridReader` lo setea,
`ClipboardTsvReader` siempre `false`) para que el comando rechace sin selección. `GetDataObject(true,
true)` ya restringe a las columnas seleccionadas y devuelve sus encabezados, así que la selección parcial
de columnas funciona gratis. Sin fallback a `ClipboardTsvReader` en este comando: ese lector no puede
probar que hubo selección, y sin la opción "incluir encabezados" de SSMS pierde en silencio la primera
fila de datos.

**Invocación — tres caminos, en este orden de esfuerzo:**

1. **Menú contextual del grid de resultados (plan A, probar primero).** A diferencia del menú contextual
   del *editor* (descartado en M3), el popup del grid de resultados sí es un menú VSCT: por reflection
   sobre `lib/ssms22.6/SQLEditors.dll`, `SQLWorkbenchCommands.IDM_SQLWB_SQLRESGRID_CONTEXT = 112`
   (`0x0070`) vive en `GUID_SQLEditorCommandSet = {52692960-56bc-4989-b5d3-94c47a513e8d}`, y los metadatos
   del ensamblado sí referencian `IVsUIShell.ShowContextMenu`. Declarar ese guid/id como símbolos externos
   en `SsmsQuickTools.vsct` y parentar un grupo nuevo:
   ```xml
   <Group guid="guidQuickToolsCmdSet" id="ResultsGridContextGroup" priority="0x0600">
     <Parent guid="guidSqlEditorCmdSet" id="IDM_SQLWB_SQLRESGRID_CONTEXT" />
   </Group>
   ...
   <GuidSymbol name="guidSqlEditorCmdSet" value="{52692960-56bc-4989-b5d3-94c47a513e8d}">
     <IDSymbol name="IDM_SQLWB_SQLRESGRID_CONTEXT" value="0x0070" />
   </GuidSymbol>
   ```
   El botón se declara dos veces (bajo `ToolsMenuGroup` y bajo `ResultsGridContextGroup`) con mismo
   `guid`/`id`, así ambas entradas disparan el mismo `OleMenuCommand`. Con `DynamicVisibility` +
   `BeforeQueryStatus` (patrón en `ScriptObjectCommands.cs:32-63`) para ocultar el ítem si no hay grid con
   selección. **Verificación empírica obligatoria antes de dar por buena esta vía** (mismo tipo de prueba
   que M3): instalar el VSIX, click derecho sobre el grid de resultados, confirmar que el ítem aparece; y
   revisar Herramientas → Personalizar → Comandos → Menú contextual. Si no aparece, esta vía está muerta —
   M3 ya demostró que SSMS ignora grupos de terceros en `GUID_SQLEditorCommandSet` para el menú del editor
   de script (id 80), así que no vale la pena intentar variantes.

   **Descartado tras verificación empírica (2026-09-10) contra SSMS 22.6.**  Se implementó (el `<Button>`
   se declara una sola vez con parent `ToolsMenuGroup`, ubicado también en `ResultsGridContextGroup` vía
   `<CommandPlacement>` — declarar el mismo `guid`/`id` dos veces como `<Button>` no compila, VSCT lo
   rechaza como definición duplicada), se instaló el VSIX (v0.1.7) y se probó con click derecho sobre el
   grid de resultados: **el ítem no aparece**. Mismo resultado que M3 con el menú del editor: SSMS arma el
   popup del grid de resultados a mano y tampoco fusiona grupos de terceros en `GUID_SQLEditorCommandSet`,
   pese a que los metadatos del ensamblado sí referencian `IVsUIShell.ShowContextMenu`. Se revirtió el
   grupo/placement/símbolos del intento (código muerto sin utilidad) y se descartó también el plan B por
   el mismo motivo que originalmente lo hacía condicional: sin plan A, no vale el riesgo de enganchar
   `ContextMenuStrip` en runtime para ganar un click derecho que Tools + `Ctrl+K, Ctrl+4` ya cubre
   (confirmado funcionando). El comando queda solo por esas dos vías, como en M2/M3.
2. **`ContextMenuStrip` en tiempo de ejecución (plan B, evaluado y descartado sin implementar).**
   **Decisión (2026-09-10)**: con plan A confirmado muerto, se optó por no intentar plan B — el riesgo
   (posible doble popup o `ContextMenuStrip` que nunca dispara, por `WM_CONTEXTMENU` manejado a mano por
   SSMS) no se justifica para ganar un click derecho que Tools + atajo ya cubre y que el usuario confirmó
   funcionando. Queda documentado por si en una versión futura de SSMS cambia el comportamiento de plan A
   y vale la pena revisar esto de nuevo. `ContextMenuStrip` aparece
   **cero** veces en los metadatos de `SQLEditors.dll`: SSMS nunca asigna esa propiedad en sus grids, así
   que queda libre. `GridResultsGrid` hereda de `Microsoft.SqlServer.Management.UI.Grid.GridControl`
   (`Control` de WinForms común), que expone públicos `MouseButtonClicking` / `MouseButtonClicked`
   (`MouseButtonClickedEventArgs.Button`, `.RowIndex`, `.ColumnIndex`). Habría que engancharse en
   activación de ventana (no en el click, no hay forma de descubrir el grid recién ahí): implementar
   `IVsSelectionEvents.OnElementValueChanged` para `SEID_WindowFrame`, reutilizar el recorrido
   `FindGridControl` de `GridReader.cs:126-156` sobre el `DocView` del frame nuevo, y asignar el
   `ContextMenuStrip` una vez por instancia (idempotente, trackeando en `ConditionalWeakTable` para no
   perder referencia a ventanas cerradas). Riesgo a verificar: SSMS maneja `WM_CONTEXTMENU` a mano para
   mostrar su propio popup, así que el `ContextMenuStrip` de WinForms puede no dispararse nunca, o
   dispararse *además* del menú de SSMS (dos popups). Cualquiera de los dos casos descarta también el
   plan B.
3. **Menú Tools + atajo de teclado (plan C, se entrega siempre).** Igual patrón que M2/M3: `Ctrl+K, Ctrl+4`
   (acorde con prefijo `Ctrl+K`, para no chocar con atajos de otras extensiones), bajo `ToolsMenuGroup`. No es condicional a que A o B funcionen — se
   entrega siempre, así la funcionalidad nunca queda bloqueada por el menú contextual. El resultado de A y
   B (funcionó / no funcionó, y por qué) se documenta acá con fecha y build de SSMS, igual que el
   `~~tachado~~` + "Descartado tras verificación empírica" de M3.

**Inferencia de tipos.** Reutiliza `ValuesScriptBuilder.InferColumnTypes(columnCount, rows)`, ya `public
static` (`Features/ScriptData/ValuesScriptBuilder.cs:112`), mapeado a los tres tipos de SpreadsheetML:

| `InferredSqlType` | `ss:Type` |
|---|---|
| `Bit`, `Int`, `BigInt`, `Decimal` | `Number` (sujeto a la regla de exactitud de abajo) |
| `DateTime2` | `DateTime` (sujeto a la regla de precisión de abajo) |
| `UniqueIdentifier`, `NVarChar` | `String` |

`Bit` mapea a `Number`, no a `Boolean`: una columna `0`/`1` en el grid es mucho más frecuentemente un
`int` que un `bit` real, y un `Boolean` equivocado renderiza `TRUE`/`FALSE` en Excel.

**Reglas de exactitud (el núcleo de este milestone).** Un número de Excel es un double IEEE-754: ~15
dígitos decimales significativos. Una celda se emite como `Number` **solo si el valor hace round-trip sin
pérdida**; si no, cae a `String`, conservando el texto exacto al costo de no ser aritmética en Excel.

- **Enteros**: `Number` si `|v| <= 2^53 - 1` (9007199254740991). `bigint` más grande → `String`.
- **Decimales**: `Number` si el valor tiene 15 o menos dígitos significativos. Más que eso → `String`
  (un `decimal(38,10)` típico cae acá).
- **Separador de miles**: `InferCellType` usa `NumberStyles.Number`, que acepta `1,234.50` — trampa ya
  documentada por `ValuesScriptBuilderTests.cs:110`. Revalidar cada celda numérica con `NumberStyles.Float
  | NumberStyles.AllowLeadingSign` (sin separador de miles) antes de emitir `Number`; si falla → `String`.
- **`NULL`**: semántica de `IsNullText` (`ValuesScriptBuilder.cs:194`) — emitir `<Cell/>` vacío, no el
  texto `NULL`. Limitación heredada de M2: un string real `"NULL"` es indistinguible.
- **Ceros a la izquierda**: un valor con apariencia numérica y cero a la izquierda (`00123`, número de
  cuenta o documento) → `String`, para que Excel no los recorte.

**Reglas de fecha/hora.** `DateTime` de SpreadsheetML es `yyyy-MM-ddTHH:mm:ss[.fff]`, hasta 3 dígitos de
fracción de segundo. `datetime2(7)` en el grid trae 7 (`2026-03-04 10:20:30.1234567`).

- 3 o menos dígitos de fracción → `DateTime`, formateado con `CultureInfo.InvariantCulture`.
- Más de 3 → `String` con el texto original exacto (la precisión se truncaría en silencio).
- Valores solo-hora (`time(7)`) → `String`; no hay equivalente limpio en SpreadsheetML.
- Fechas anteriores a 1900-01-01 → `String`: el sistema de fecha serial de Excel no las representa.

**Estilos.** Un bloque `<Styles>`, un estilo por rol, para que la selección pegada se vea como tabla:

- `sHeader` — `<Font ss:Bold="1"/>` para la fila de encabezado.
- `sDateTime` — `<NumberFormat ss:Format="yyyy\-mm\-dd hh:mm:ss"/>` (columnas solo-fecha:
  `yyyy\-mm\-dd`).
- `sDecimalN` — un estilo por escala distinta encontrada en una columna decimal, `ss:Format="0.000…"`
  construido con la cantidad máxima de decimales vista en esa columna. Esto preserva la escala *visible*
  (`12.50` queda `12.50`, no `12.5`).
- Columnas enteras y de texto usan el estilo por defecto.

El estilo de columna se aplica a nivel `<Column ss:StyleID="…"/>`, así toda la columna lo hereda.

**Forma del documento:**

```xml
<?xml version="1.0"?>
<?mso-application progid="Excel.Sheet"?>
<Workbook xmlns="urn:schemas-microsoft-com:office:spreadsheet"
          xmlns:ss="urn:schemas-microsoft-com:office:spreadsheet">
  <Styles>
    <Style ss:ID="sHeader"><Font ss:Bold="1"/></Style>
    <Style ss:ID="sDateTime"><NumberFormat ss:Format="yyyy\-mm\-dd hh:mm:ss"/></Style>
  </Styles>
  <Worksheet ss:Name="Results">
    <Table>
      <Row ss:StyleID="sHeader">
        <Cell><Data ss:Type="String">Id</Data></Cell>
        <Cell><Data ss:Type="String">Importe</Data></Cell>
      </Row>
      <Row>
        <Cell><Data ss:Type="Number">1</Data></Cell>
        <Cell><Data ss:Type="Number">12.50</Data></Cell>
      </Row>
    </Table>
  </Worksheet>
</Workbook>
```

Escapado XML: `&`, `<`, `>` en cada valor y en cada nombre de columna; se eliminan caracteres ilegales en
XML 1.0 (caracteres de control salvo tab/CR/LF). Nombre de hoja limitado a 31 caracteres y sin
`: \ / ? * [ ]`.

**Escritura al portapapeles.** A diferencia de `ScriptDataCommand.cs:71` (`Clipboard.SetText`), Excel solo
reconoce el formato de portapapeles registrado como **`"XML Spreadsheet"`**, con los bytes UTF-8 del XML.
Historial de los tres intentos (v0.1.7, v0.1.9, v0.1.10) en la nota de estado al inicio de este milestone.

Implementación actual (v0.1.10, `Features/CopyXmlSpreadsheet/ClipboardDataObject.cs`): `IDataObject` COM
propio (`System.Runtime.InteropServices.ComTypes.IDataObject`, no el de WinForms), publicado con
`OleSetClipboard` + `OleFlushClipboard`. Dos entradas (`RegisterClipboardFormat("XML Spreadsheet")` y
`CF_UNICODETEXT` como fallback), ambas ofrecidas con `TYMED_HGLOBAL` en `GetData`/`QueryGetData`/
`EnumFormatEtc` — cada `GetData` hace su propio `GlobalAlloc`/`GlobalLock`/`Marshal.Copy`/`GlobalUnlock`
bajo pedido del consumidor (Excel llama `GetData` cuando el usuario pega, no antes). Con `try/catch` y el
mismo tratamiento de `MessageBox` que `ScriptDataCommand.cs:69-77`.

**Volumen.** Reutiliza la confirmación Sí/No de `RowLimitWithoutConfirmation = 1000`
(`ScriptDataCommand.cs:16,45-56`). No hay límite duro tipo `VALUES` acá, así que sin particionado: un solo
`<Table>` con todas las filas seleccionadas.

Comando expuesto en **Tools** y con atajo de teclado (`Ctrl+K, Ctrl+4`); el menú contextual del grid de
resultados se descartó (ver "Invocación" más arriba).

## Milestone 5 — Menú top-level "Quick Tools"

**Estado: funcionando (2026-09-14, v0.1.12, confirmado contra SSMS 22.6 real).**

Los cuatro comandos vivían todos en `IDM_VS_MENU_TOOLS` (`ToolsMenuGroup`) porque M3/M4 confirmaron que
los menús contextuales del editor y del grid de resultados no fusionan grupos de terceros. Se agregó un
menú propio en la barra principal —`QuickToolsMenu`, anclado a `guidSHLMainMenu:IDG_VS_MM_TOOLSADDINS`,
con un grupo plano hijo `QuickToolsMenuGroup`— y se reparentaron ahí los cuatro botones existentes. Es
el mismo mecanismo de merge VSCT que ya funciona contra `IDM_VS_MENU_TOOLS`, aplicado a un punto de
anclaje distinto de la misma barra; no requirió cambios en el código C# (los `OleMenuCommand` siguen
registrados contra los mismos GUID/ID de `PackageGuids`/`PkgCmdId`).

**Confirmado: SSMS 22.6 sí fusiona un menú top-level de terceros** en `IDG_VS_MM_TOOLSADDINS`, junto a
Herramientas. El único obstáculo fue de testing, no de merge: SSMS cachea la barra de menús, así que tras
reinstalar el `.vsix` con SSMS abierto el menú queda registrado (visible en Personalizar → Comandos →
Menú, y se puede agregar a mano ahí) pero gris y sin desplegar. Cerrar y volver a abrir SSMS por completo
refresca el cache y el menú aparece solo, habilitado, con los cuatro comandos. Ver
[[ssms-vsix-testing-workflow]] en la memoria del proyecto: un cambio de `.vsct` requiere reinicio de SSMS
además del bump de versión de siempre.

Se probó primero con una red de seguridad temporal (`<CommandPlacements>` duplicando los comandos en
`ToolsMenuGroup`) por si el merge top-level fallaba; una vez confirmado que funciona, se retiró ese
bloque junto con `ToolsMenuGroup` y su `IDSymbol`. Los cuatro comandos viven ahora únicamente en
`QuickToolsMenuGroup`.

Se agregó además un comando **About** (grupo propio `AboutMenuGroup`, separado por divisor de los
submenús Query/Results Grid) que muestra versión y fecha de build. Como el `source.extension.vsixmanifest`
es un item `None` (nada lo lee en runtime) y el proyecto no tiene `AssemblyInfo.cs`, se agregó un target
MSBuild (`GenerateBuildInfo` en `SsmsQuickTools.csproj`) que lee `Identity/@Version` del manifest con
`XmlPeek` y escribe `BuildInfo.g.cs` con esa versión y la fecha real de compilación (`dd/MM/yyyy`),
compilado en cada build junto con el resto de las fuentes.

Además, el target `BumpVsixVersion` (corre antes que `GenerateBuildInfo`) sube automaticamente el último
número de `Identity/@Version` en cada build real (reescribe el `.vsixmanifest` con `XmlPoke`), para que
SSMS/VS nunca rechacen la reinstalación por versión ya instalada. Se salta en design-time builds del IDE
(`Condition` sobre `$(DesignTimeBuild)`/`$(BuildingProject)`) para no gastar versiones solo por abrir un
archivo. Efecto colateral: el `.vsixmanifest` queda modificado en el working tree después de cada build;
hay que revisarlo/commitearlo como cualquier otro cambio versionado.

## Milestone 6 — Auto Replacement

**Estado: funcionando (2026-09-15, v0.2.0, confirmado contra SSMS 22.6.11806.211 real).** Es la
función más invasiva hasta ahora: es la primera que intercepta el tecleo del editor. El filtro de
comandos engancha Enter sin necesidad del fallback por `IVsSelectionEvents` — el spike no encontró
obstáculos.

Expande un token corto pegado al cursor al presionar **Enter**, reemplazándolo por un snippet SQL
configurado por el usuario en `%APPDATA%\SsmsQuickTools\autoreplacement.xml`. Pensado para consultas
de diagnóstico repetitivas (ej. `cm` → `SELECT TOP 200 * FROM dbo.cola_mensajes_n3 ...`).

**Sin MEF.** El proyecto no tenía ninguna pieza MEF (`docs/PLAN.md` de M3/M4 ya documentó que SSMS
22.6 arma su UI de editor a mano e ignora buena parte de la extensibilidad estándar de VS), así que
meter la primera pieza MEF hubiera sido un riesgo no verificado. En cambio, Enter se intercepta como
comando `VSStd2K.RETURN` sobre el `IVsTextView` activo, vía `IVsTextView.AddCommandFilter` — mismo
layer COM legacy que ya usa `SsmsHost` para leer/escribir el buffer, sin tocar `vsixmanifest`.

Arquitectura (`Features/AutoReplacement/`):
- `AutoReplacementEntry` / `AutoReplacementCatalog`: mismo patrón que
  `Features/QuickConnect/ConnectionCatalog.cs` (siembra de archivo de ejemplo, `FileSystemWatcher`
  sin debounce, `catch` silencioso ante XML inválido conservando la última config válida). Parseo con
  `XDocument` en vez de `XmlSerializer`/`DataContractJsonSerializer`, para no generar un ensamblado de
  serialización en el primer arranque y para que `XElement.Value` preserve la indentación literal de
  `<Replacement>`. Cada `<AutoReplacement>` admite varios `<Token>` (alias del mismo snippet).
- `TokenScanner`, `SqlContextScanner`, `AutoReplacementExpander`: lógica pura (sin `using` de
  SSMS/VS), cubierta por tests unitarios y enlazada a `SsmsQuickTools.Tests` igual que
  `ValuesScriptBuilder`/`ObjectNameParser`. `SqlContextScanner` implementa un léxico T-SQL de una
  pasada (cadenas, `[...]`, `"..."`, `--`, `/* */` anidado) para no expandir dentro de literales o
  comentarios, porque el clasificador interno de SSMS no está expuesto.
- `Ssms/TextViewEditor.cs`: helpers sobre un `IVsTextView` puntual (no "la vista activa" como
  `SsmsHost`), mismo marshalling `ReplaceLines`/`CoTaskMem` que `SsmsHost.InsertTextIntoActiveView`.
- `AutoReplacementCommandFilter` (`IOleCommandTarget` por vista) + `AutoReplacementService`
  (`IVsTextManagerEvents`, engancha la vista activa al arrancar y cada vista nueva vía connection
  point sobre `SVsTextManager`, `ConditionalWeakTable` para enganche idempotente sin retener vistas
  cerradas) — mismo criterio que el plan B evaluado (y no implementado) en Milestone 4 para el grid.
- `ExpandTokenCommand`: comando manual "Expandir token" en Quick Tools + `Ctrl+K, Ctrl+5` (plan C,
  igual criterio que M3/M4/M5): si el filtro de Enter deja de engancharse en una versión futura de
  SSMS, la función sigue siendo usable a mano.

Confirmado por prueba manual contra SSMS 22.6.11806.211.

## Milestone 7 — Locate in Object Explorer

**Estado: en progreso (v0.2.9, spec `specs/01-locate-object-in-object-explorer.md`, branch
`spec-01-locate-object-in-object-explorer`).** Comando "Locate in Object Explorer" en Quick
Tools > Query (`Ctrl+K, Ctrl+6`): ubica y selecciona, en el árbol de Object Explorer, el objeto
(tabla/vista/procedimiento/función/trigger) seleccionado o bajo el cursor en la query activa.
Reusa el mismo origen de texto y `ObjectNameParser` que Script CREATE/ALTER (M3).

Es la pieza de mayor riesgo hasta ahora: a diferencia de M1-M6 (que acceden a APIs internas ya
usadas por `SsmsHost`), esta es la primera vez que se navega el árbol de Object Explorer en sí,
sin ningún precedente en el proyecto y sin API pública. Historial de hallazgos por reflection
contra una instalación real de SSMS 22.6.11806.211 (no había forma de probarlo sin instalar y
ejecutar, iterando en varias rondas):

1. **`ObjectExplorer.dll`** (`Common7\IDE`, copiada a `lib/ssms22.6/`) contiene el control real
   del árbol: `Microsoft.SqlServer.Management.UI.VSIntegration.ObjectExplorer.ObjectExplorerControl`
   (un `System.Windows.Forms.TreeView` real por herencia) y `ExplorerHierarchyNode` (un
   `TreeNode` real). **Ambas clases son internas** al ensamblado — no se pueden nombrar desde el
   proyecto — pero sus miembros propios (`NodeName`, `ChildrenEnumerated`, `EnumerateChildren()`)
   son públicos, así que se usan por reflection sobre instancias tratadas como `TreeView`/
   `TreeNode` público.
2. **El `DocView` de la tool window de Object Explorer no es el control**, sino
   `Microsoft.SqlServer.Management.SqlStudio.Explorer.ObjectExplorerToolWindow` — el
   `ToolWindowPane` que lo aloja en el shell VS2022 de SSMS 22 (SSMS 22 no es el shell legado de
   versiones anteriores). Ese pane expone el control vía su propiedad pública `Control`.
   Encontrado recién en la segunda ronda, agregando diagnóstico temporal al mensaje de "no
   encontrado" que listaba caption+tipo real de cada tool window recorrida — la única forma de
   ver esto fue con el usuario probando contra su SSMS real.
3. **Esa DLL no se referencia directamente**: depende de `Microsoft.VisualStudio.Shell.15.0`
   v18.0, más nueva que el `Microsoft.VisualStudio.SDK` 17.11 que usa este proyecto (referenciarla
   rompe la compilación, `CS1705`). Se lee por reflection (nombre de tipo + propiedad `Control`),
   igual criterio de aislamiento que el resto de `SsmsHost.cs`.
4. **No se encontraron nombres localizados de carpetas** ("Databases"/"Tables"/etc, ni sus
   equivalentes en español) ni el GUID público de la tool window de Object Explorer. El diseño
   evita depender de ambos: `SsmsHost.FindObjectExplorerControl()` busca la tool window recorriendo
   *todas* las registradas (`IVsUIShell.GetToolWindowEnum`) y comparando el nombre de tipo del
   `DocView`; `FindNodeByName` hace una búsqueda en anchura acotada por profundidad (2 niveles para
   la base de datos, 4 más para el objeto) comparando solo el nombre de cada nodo contra lo
   buscado, expandiendo con `EnumerateChildren()` donde haga falta.
5. **`CollapseExceptAncestorsOf`**: la búsqueda de arriba expande de más (todas las carpetas que
   recorre en el camino, no solo la que termina llevando al objeto). Se colapsa todo menos la
   cadena de ancestros del nodo encontrado antes de seleccionarlo, imitando el "Locate" nativo de
   SSMS. Confirmado por el usuario que funciona (v0.2.8).

**Pendiente/bloqueado:** con el árbol de Object Explorer totalmente colapsado en frío (conexión
nunca tocada por el usuario), el comando sigue sin encontrar el objeto incluso tras subir el
timeout de espera de expansión de 10s a 30s (`EnsureChildrenLoadedTimeoutMs`, v0.2.9). Hipótesis
sin confirmar: `ChildrenEnumerated` puede volverse `true` con un placeholder de "Conectando..."
antes de tener los hijos reales, o `EnumerateChildren()` no dispara nada útil la primera vez
sobre un nodo de servidor nunca tocado. Retomar mostrando/logueando la cantidad de hijos tras la
espera, o cambiando el poll de `ChildrenEnumerated` por el evento `FinishedExpanding`/
`StateChanged` de la clase.

## Verificación

Todo se verifica contra SSMS real; no hay pruebas automatizadas de la capa de UI. Verificar como mínimo en **22.6.0** (piso soportado) y en la versión más reciente disponible, ya que el `GridReader` por reflection es lo que más probablemente difiera entre ambas.

- **M0**: la extensión aparece en Extensions → Manage Extensions y el comando de prueba responde.
- **M1**: editar `connections.json` con varias conexiones (incluyendo dos del mismo servidor); el combo se puebla; seleccionar una conexión cambia la conexión de la ventana activa (verificar con `SELECT @@SERVERNAME, DB_NAME()`).
- **M2**: ejecutar una consulta con columnas de tipos mixtos (int, nvarchar con comilla simple, datetime, NULL, decimal, uniqueidentifier, bit); usar el comando; pegar el resultado en una ventana nueva y confirmar que ejecuta y devuelve las mismas filas. Repetir con selección parcial de celdas y con más de 1000 filas.
  **Hecho** (2026-09-09, contra `LENOVOJOSE\DEV01`/`Figuritas`): checklist manual completo (comando por Tools y por Ctrl+Shift+D, selección parcial de filas, dos result sets, >1000 filas con confirmación, casos de error). Los TSV capturados quedaron como fixtures reales en `SsmsQuickTools.Tests/Fixtures/` (`tipos_mixtos.tsv`, `tipos_mixtos_seleccion_parcial.tsv`, `volumen_1500filas.tsv`) y se ejecutan automáticamente en `ScriptRoundTripTests.cs` cuando `SSMSQT_TEST_CONNECTION` está definida (esas capturas se hicieron sin encabezado a propósito, así que se les agregó a mano el header conocido de cada consulta antes de usarlas como fixture). Pendiente todavía: el fallback de `ClipboardTsvReader` con "Include column headers when copying or saving results" desactivado en Tools → Options — sin encabezado real, toma la primera fila de datos como encabezado y la pierde en silencio; ese caso concreto (deliberado, "qué pasa si me olvido la opción") no se ejercitó todavía en la UI. Tampoco se confirmó selección parcial de *columnas* (la captura recibida trajo filas completas), solo de filas.
  **Re-verificado** (2026-09-18, v0.2.1, formato `INSERT INTO XXXXXXXX`): reinstalado el VSIX contra
  SSMS 22.6 real, `Ctrl+Shift+D` sobre un grid pega el nuevo formato correctamente; probado también con
  más de 1000 filas — confirma la partición en varios statements `INSERT` (sin `UNION ALL`).
  **Checklist manual** (2026-10-01, v0.2.9/0.2.10, `docs/manual-tests.sql`): fallback sin "Include column
  headers" — **OK**: sin aviso de fallback y con las 3 filas completas (el caso "pierde la primera fila"
  ya no se reproduce). **FALLA — selección de columnas no contiguas**: seleccionar varias columnas
  (Ctrl+click) y usar el comando copia solo la última columna seleccionada (caso M2.1).
- **M1** (Quick Connect): **OK** (2026-10-01, checklist `docs/manual-tests.sql` M1.1-M1.6).
- **M3**: **OK** (2026-10-01, checklist `docs/manual-tests.sql`: tabla/vista/procedimiento/función, nombre
  simple, con schema y completo, objeto inexistente). Pendiente original ya cubierto:
  probar sobre una tabla, una vista, un procedimiento y una función; con nombre completo y con nombre simple; y con un objeto inexistente (debe avisar sin excepción).
- **M4**: **Hecho, parcial** (2026-09-11, contra SSMS 22.6, v0.1.10): el comando aparece y ejecuta desde
  Tools y desde `Ctrl+Shift+X` sin errores; pegado en Excel confirmado con encabezado en negrita y tipos
  preservados (bloqueante resuelto, ver nota de estado al inicio del milestone). El menú contextual del
  grid de resultados no lo expone (descartado, ver "Invocación").
  **Checklist manual** (2026-10-01, v0.2.9/0.2.10, `docs/manual-tests.sql`):
  - Tipos mixtos completos (`bigint` mayor a 2^53, `decimal(38,10)`, `money`, ceros a la izquierda,
    `datetime2(7)`, `date`, `time`, `bit`, `NULL`, `uniqueidentifier`, string con `<`, `&` y tab) — **OK**.
  - Fallback de texto plano en Notepad — **OK**.
  - **FALLA — selección parcial de columnas no contiguas**: con Ctrl+click sobre varios encabezados, el
    comando copia solo la última columna seleccionada (caso M4.2).
- Prueba de regresión de riesgo: reiniciar SSMS varias veces y confirmar que no se degrada el arranque ni aparecen errores en `%AppData%\Microsoft\SSMS\ActivityLog.xml` (arrancar con `Ssms.exe /log` para generarlo).
- **M6 (Auto Replacement)**: **Hecho** (2026-09-15, contra SSMS 22.6.11806.211, v0.2.0): probado
  manualmente por el usuario tras reinstalar el VSIX y reiniciar SSMS por completo — funciona bien.
- **M7 (Locate in Object Explorer)**: **Parcial, en progreso** (2026-09-20/21, contra SSMS
  22.6.11806.211, v0.2.7-v0.2.9): encuentra el panel y localiza/selecciona el objeto cuando el
  árbol ya tuvo alguna expansión previa (tibio); con el árbol totalmente colapsado en frío
  (conexión de Object Explorer nunca tocada), sigue fallando — ver Milestone 7 para el detalle y
  la hipótesis pendiente de confirmar.

## Pendientes abiertos (2026-10-01)

1. **Bug: selección de múltiples columnas copia solo la última** (M2 y M4, `GridReader`). Con Ctrl+click
   sobre varios encabezados, `GetDataObject(hasSelection, true)` solo devuelve la última columna. Investigar
   cómo `SelectedCells` representa selecciones múltiples (¿un rango por click?) y armar el dato a mano si
   hace falta. Probar también rango contiguo con Shift+click, que no se llegó a registrar.
2. **M7 caso frío** (ver Milestone 7).
3. ~~`source.extension.vsixmanifest` con dos elementos `<Identity>`~~ **Resuelto** (2026-10-01). Causa: el
   merge `19d9dd0` (`main` → `mejoras_menu`) combinó dos bumps de versión de líneas adyacentes
   (0.2.10 y 0.2.9) sin conflicto y dejó ambas. `BumpVsixVersion` no tuvo la culpa. Se quitó la línea
   0.2.9; queda `Version="0.2.10"`.
4. Atajos citados arriba en Verificación como `Ctrl+Shift+D` / `Ctrl+Shift+X` están desactualizados; los
   actuales son `Ctrl+K, Ctrl+1` y `Ctrl+K, Ctrl+4`.

## Unit tests

`ValuesScriptBuilder`, `ObjectNameParser` y `XmlSpreadsheetBuilder` (Milestone 4) son lógica pura sin dependencias de SSMS: proyecto de tests separado con xUnit cubriendo inferencia de tipos, escapado, particionado en bloques de 1000, parseo de nombres, y las reglas de exactitud/precisión de `XmlSpreadsheetBuilder` (redondeo a `String` cuando el valor no hace round-trip, formato de fecha/hora, escapado XML). `TokenScanner`, `SqlContextScanner` y `AutoReplacementExpander` (Milestone 6) siguen el mismo criterio.
