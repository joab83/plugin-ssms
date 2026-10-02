/*
    manual-tests.sql - Checklist manual para SsmsQuickTools (M1-M4).

    Cada bloque empieza con "-- CASO Mx.n". Ejecutar cada bloque por separado
    (seleccionar + F5), no el archivo entero: los casos de grid necesitan que
    el resultado quede en pantalla antes de usar el comando.

    Atajos actuales (ver SsmsQuickTools.vsct):
        Ctrl+K, Ctrl+1  Copy Selection as Script SELECT
        Ctrl+K, Ctrl+2  Script Object as CREATE
        Ctrl+K, Ctrl+3  Script Object as ALTER
        Ctrl+K, Ctrl+4  Copy Selection as SpreadSheet (Excel)
        Ctrl+K, Ctrl+5  Expandir token (Autoreplacement)
        Ctrl+K, Ctrl+6  Locate in Object Explorer

    Los comandos tambien estan en el menu Quick Tools.
    Anotar OK / FALLA por caso y pasar el resultado para actualizar docs/PLAN.md.
*/


/* ============================================================================
   CASO M4.1 - Tipos mixtos (copiar como XML Spreadsheet)
   ----------------------------------------------------------------------------
   1. Ejecutar la consulta de abajo.
   2. Seleccionar TODAS las celdas del grid (Ctrl+A en el grid).
   3. Ctrl+K, Ctrl+4.
   4. Pegar en una hoja de Excel nueva.

   Esperado por columna:
     id_int              Number
     big_ok              Number (9007199254740991 = 2^53 - 1, exacto)
     big_mayor_2_53      Texto (> 2^53, no hace round-trip como double)
     dec_10_2            Number, visible como 12.50 (no 12.5)
     dec_38_10           Texto (mas de 15 digitos significativos)
     monto               Number
     texto_ceros         Texto, conserva 00123
     dt2_7               Texto exacto con 7 decimales (> 3 digitos de fraccion)
     dt2_3               Fecha/hora real, formato yyyy-mm-dd hh:mm:ss
     fecha               Fecha real, formato yyyy-mm-dd
     hora                Texto (time no tiene equivalente limpio)
     fecha_antes_1900    Texto (Excel no representa fechas < 1900)
     bit_col             Number (1), no TRUE/FALSE
     nulo                Celda vacia
     guid_col            Texto
     especiales          Texto: a<b & c, tab, tab (sin romper el XML)
     texto_null_literal  Celda vacia (limitacion conocida: "NULL" literal
                         es indistinguible de NULL real)
     Fila 2              Todas vacias salvo id_int = 2
   ============================================================================ */
SELECT
    CAST(1 AS int)                                      AS id_int,
    CAST(9007199254740991 AS bigint)                    AS big_ok,
    CAST(9007199254740993 AS bigint)                    AS big_mayor_2_53,
    CAST(12.50 AS decimal(10,2))                        AS dec_10_2,
    CAST(1234567890.1234567891 AS decimal(38,10))       AS dec_38_10,
    CAST(1234.56 AS money)                              AS monto,
    N'00123'                                            AS texto_ceros,
    CAST('2026-03-04 10:20:30.1234567' AS datetime2(7)) AS dt2_7,
    CAST('2026-03-04 10:20:30.123' AS datetime2(3))     AS dt2_3,
    CAST('2026-03-04' AS date)                          AS fecha,
    CAST('10:20:30.1234567' AS time(7))                 AS hora,
    CAST('1850-01-01' AS datetime2(0))                  AS fecha_antes_1900,
    CAST(1 AS bit)                                      AS bit_col,
    CAST(NULL AS int)                                   AS nulo,
    CAST('6F9619FF-8B86-D011-B42D-00C04FC964FF' AS uniqueidentifier) AS guid_col,
    N'a<b & c' + CHAR(9) + N'tab'                       AS especiales,
    N'NULL'                                             AS texto_null_literal
UNION ALL
SELECT 2, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL, NULL,
       NULL, NULL, NULL, NULL, NULL;
GO
----------------------------------------------------------------------------
--CASO M4.1 - RESULTADO OK
----------------------------------------------------------------------------

/* ============================================================================
   CASO M4.2 - Seleccion parcial de columnas (XML Spreadsheet)
   ----------------------------------------------------------------------------
   Con el resultado de M4.1 en pantalla:
   1. Click en encabezado de columna "big_ok", Ctrl+click en "dec_10_2" y
      "texto_ceros" (columnas no contiguas).
   2. Ctrl+K, Ctrl+4, pegar en Excel.
   Esperado: SOLO esas 3 columnas, con sus encabezados, en ese orden.
   Repetir con un rango contiguo (click en "dt2_3", Shift+click en "hora").
   Repetir sin ninguna seleccion: debe avisar, no copiar.
   ============================================================================ */
	----------------------------------------------------------------------------
	--CASO M4.2 - PUEDO SELECCIONAR COLUMNAS PARCIALES PERO ME COPIA SOLO LA ULTIMA
	----------------------------------------------------------------------------

/* ============================================================================
   CASO M4.3 - Fallback de texto plano
   ----------------------------------------------------------------------------
   Con el resultado de M4.1 en pantalla y la copia ya hecha (Ctrl+K, Ctrl+4):
   pegar en Notepad.
   Esperado: texto tabulado (TSV) con encabezados, una fila por linea.
   ============================================================================ */
	----------------------------------------------------------------------------
	--CASO M4.3 - RESULTADO OK
	----------------------------------------------------------------------------

/* ============================================================================
   CASO M2.1 - Seleccion parcial de columnas (Script SELECT)
   ----------------------------------------------------------------------------
   Con el resultado de M4.1 en pantalla:
   1. Seleccionar columnas id_int, big_ok, dec_10_2, texto_ceros.
   2. Ctrl+K, Ctrl+1.
   3. Pegar en una ventana nueva. Esperado: INSERT INTO XXXXXXXX con
      SELECT * FROM (VALUES ...) v ([id_int], [big_ok], [dec_10_2], [texto_ceros]);
      solo con esas 4 columnas y 2 filas.
   4. Ejecutar el script de abajo: crear #t, reemplazar XXXXXXXX por #t, y
      correr el INSERT pegado. Esperado: 2 filas, 4 columnas, sin error.
   ============================================================================ */
IF OBJECT_ID('tempdb..#t') IS NOT NULL DROP TABLE #t;
CREATE TABLE #t (id_int int, big_ok bigint, dec_10_2 decimal(10,2), texto_ceros nvarchar(20));
-- <pegar aca el INSERT INTO #t ... generado>
SELECT * FROM #t;
GO
	----------------------------------------------------------------------------
	--CASO M2.1 - PUEDO SELECCIONAR COLUMNAS PARCIALES PERO ME COPIA SOLO LA ULTIMA
	----------------------------------------------------------------------------

/* ============================================================================
   CASO M2.2 - Fallback ClipboardTsvReader sin encabezados
   ----------------------------------------------------------------------------
   Caso deliberado: "que pasa si me olvido la opcion".
   1. Tools > Options > Query Results > SQL Server > Results to Grid:
      desmarcar "Include column headers when copying or saving the results".
      (Cierra y reabre la ventana de query para que aplique.)
   2. Ejecutar la consulta de abajo (3 filas).
   3. Seleccionar todo el grid, Ctrl+K, Ctrl+1.
   Anotar:
     a) Hubo aviso de fallback? (si/no)
     b) La salida tiene 3 filas o 2? (documentado: pierde la primera fila
        porque la toma como encabezado)
   4. Volver a marcar la opcion al terminar.
   ============================================================================ */
SELECT n AS numero, CONCAT(N'fila_', n) AS texto
FROM (VALUES (1), (2), (3)) v (n);
GO
	----------------------------------------------------------------------------
	--CASO M2.2 - a) no, b) 3 filas. RESULTADO OK
	----------------------------------------------------------------------------

/* ============================================================================
   CASO M3 - Generar CREATE / ALTER
   ----------------------------------------------------------------------------
   Paso 0: crear objetos de prueba en tempdb (cada CREATE en su propio batch).
   ============================================================================ */
USE tempdb;
GO
IF OBJECT_ID('dbo.qt_test_tabla')  IS NOT NULL DROP TABLE dbo.qt_test_tabla;
IF OBJECT_ID('dbo.qt_test_vista')  IS NOT NULL DROP VIEW dbo.qt_test_vista;
IF OBJECT_ID('dbo.qt_test_proc')   IS NOT NULL DROP PROCEDURE dbo.qt_test_proc;
IF OBJECT_ID('dbo.qt_test_funcion') IS NOT NULL DROP FUNCTION dbo.qt_test_funcion;
GO
CREATE TABLE dbo.qt_test_tabla (
    id int NOT NULL CONSTRAINT PK_qt_test_tabla PRIMARY KEY,
    nombre nvarchar(50) NULL
);
GO
-- comentario previo al CREATE: el reemplazo CREATE -> ALTER debe respetarlo
CREATE VIEW dbo.qt_test_vista AS SELECT id, nombre FROM dbo.qt_test_tabla;
GO
CREATE PROCEDURE dbo.qt_test_proc @id int AS
BEGIN
    SELECT id, nombre FROM dbo.qt_test_tabla WHERE id = @id;
END;
GO
CREATE FUNCTION dbo.qt_test_funcion (@x int) RETURNS int AS
BEGIN
    RETURN @x * 2;
END;
GO

/*
   Para cada objeto, escribir el nombre en una query, seleccionarlo (o dejar el
   cursor sobre la palabra) y probar Ctrl+K, Ctrl+2 y Ctrl+K, Ctrl+3.
   Esperado: ventana nueva con el script + copia al portapapeles.

   M3.1 nombre simple      qt_test_vista
   M3.2 con schema         dbo.qt_test_proc
   M3.3 nombre completo    [tempdb].[dbo].[qt_test_funcion]
   M3.4 tabla              dbo.qt_test_tabla
          - CREATE: script SMO con PK
          - ALTER: debe avisar que no existe ALTER para tablas, sin excepcion
   M3.5 inexistente        dbo.no_existe_xyz
          - CREATE y ALTER: aviso, sin excepcion ni ventana nueva
   M3.6 vista con ALTER    el comentario previo al CREATE queda intacto y
                           solo cambia la palabra CREATE por ALTER
*/

-- Limpieza al terminar M3:
-- DROP FUNCTION dbo.qt_test_funcion;
-- DROP PROCEDURE dbo.qt_test_proc;
-- DROP VIEW dbo.qt_test_vista;
-- DROP TABLE dbo.qt_test_tabla;
GO
	----------------------------------------------------------------------------
	--CASO M3 - RESULTADO OK
	----------------------------------------------------------------------------

/* ============================================================================
   CASO M1 - Quick Connections
   ----------------------------------------------------------------------------
   Preparar %APPDATA%\SsmsQuickTools\connections.json con al menos:
     - 2 entradas del MISMO servidor (bases distintas)
     - 1 entrada de OTRO servidor (o la misma con otro alias de instancia)

   M1.1 combo se puebla con los nombres del JSON (editar el JSON con SSMS
        abierto: la lista debe refrescarse sola, FileSystemWatcher).
   M1.2 mismo servidor: elegir otra base. Esperado: cambia la base de la
        ventana sin reconectar. Verificar con la consulta de abajo.
   M1.3 otro servidor: elegir la entrada de otro servidor. Esperado: reconecta
        la misma ventana (no abre una nueva). Verificar con la consulta.
   M1.4 sin ventana de query activa (foco en Object Explorer): abre ventana
        nueva ya conectada.
   M1.5 transaccion abierta: correr el bloque BEGIN TRAN de abajo SIN cerrarlo
        y elegir otra conexion. Esperado: aviso, no desconecta.
        Luego correr ROLLBACK.
   M1.6 consulta en ejecucion: correr WAITFOR DELAY '00:00:20' y elegir otra
        conexion. Esperado: aviso, no desconecta.
   ============================================================================ */
SELECT @@SERVERNAME AS servidor, DB_NAME() AS base;
GO

BEGIN TRAN;
SELECT @@TRANCOUNT AS trancount;
-- (no cerrar; probar M1.5; despues ejecutar: ROLLBACK;)
GO

WAITFOR DELAY '00:00:20';
GO
	----------------------------------------------------------------------------
	--CASO M1 - RESULTADO OK
	----------------------------------------------------------------------------