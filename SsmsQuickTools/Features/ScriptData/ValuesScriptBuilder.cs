using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace SsmsQuickTools.Features.ScriptData
{
    /// <summary>
    /// Tipos SQL que se pueden inferir del texto de una celda de grid, en orden de
    /// especificidad creciente (el resultado de una columna es el mayor de los tipos
    /// de sus celdas no nulas).
    /// </summary>
    public enum InferredSqlType
    {
        Bit = 0,
        Int = 1,
        BigInt = 2,
        Decimal = 3,
        UniqueIdentifier = 4,
        DateTime2 = 5,
        NVarChar = 6,
    }

    /// <summary>
    /// Genera uno o mas statements SQL <c>INSERT INTO ... SELECT * FROM (VALUES ...) v (...)</c>
    /// que reproducen el resultado de un grid, listos para pegar sobre la tabla destino real
    /// (reemplazando el marcador <see cref="DefaultTargetTable"/>) y ejecutar. Logica pura, sin
    /// dependencias de SSMS: inferencia de tipos por columna a partir del texto de las celdas,
    /// escapado y particionado en bloques (VALUES admite hasta 1000 filas por constructor; por
    /// encima de eso se emite un INSERT por bloque).
    /// </summary>
    public static class ValuesScriptBuilder
    {
        private const int MaxRowsPerValuesBlock = 1000;

        public static readonly string NullLiteral = "NULL";

        /// <summary>
        /// Marcador de tabla destino que el usuario reemplaza a mano al pegar el script.
        /// </summary>
        public const string DefaultTargetTable = "XXXXXXXX";

        /// <summary>
        /// Construye el script completo. <paramref name="targetTable"/> se emite crudo (sin
        /// QuoteIdentifier) tras <c>INSERT INTO</c>: por defecto es el marcador <see cref="DefaultTargetTable"/>.
        /// </summary>
        public static string Build(IReadOnlyList<string> columns, IReadOnlyList<string[]> rows, string targetTable = DefaultTargetTable)
        {
            if (columns == null || columns.Count == 0)
            {
                throw new ArgumentException("Se necesita al menos una columna.", nameof(columns));
            }
            if (rows == null)
            {
                throw new ArgumentException("Filas nulas.", nameof(rows));
            }

            var quotedColumns = columns.Select(QuoteIdentifier).ToArray();
            var columnList = string.Join(", ", quotedColumns);

            if (rows.Count == 0)
            {
                // Sin filas: se genera un SELECT vacio tipado como NVARCHAR para que al menos
                // ejecute sin error, aclarando que no habia datos.
                var emptySelect = string.Join(", ", quotedColumns.Select(c => $"CAST(NULL AS nvarchar(1)) AS {c}"));
                return $"INSERT INTO {targetTable}{Environment.NewLine}    SELECT {emptySelect} WHERE 1 = 0;";
            }

            var columnTypes = InferColumnTypes(columns.Count, rows);
            var blocks = Partition(rows, MaxRowsPerValuesBlock);

            var sb = new StringBuilder();
            for (var b = 0; b < blocks.Count; b++)
            {
                if (b > 0)
                {
                    sb.AppendLine();
                }
                sb.Append("INSERT INTO ").Append(targetTable).AppendLine();
                sb.AppendLine("    SELECT * FROM (VALUES");
                AppendValuesBlock(sb, blocks[b], columnTypes);
                sb.Append("    ) v (").Append(columnList).Append(");");
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static void AppendValuesBlock(StringBuilder sb, IReadOnlyList<string[]> rows, InferredSqlType[] columnTypes)
        {
            for (var r = 0; r < rows.Count; r++)
            {
                var row = rows[r];
                sb.Append("        (");
                for (var c = 0; c < columnTypes.Length; c++)
                {
                    if (c > 0)
                    {
                        sb.Append(", ");
                    }

                    var raw = c < row.Length ? row[c] : null;
                    var literal = FormatLiteral(raw, columnTypes[c]);
                    sb.Append(literal);
                }
                sb.Append(")");
                if (r < rows.Count - 1)
                {
                    sb.Append(",");
                }
                sb.AppendLine();
            }
        }

        public static InferredSqlType[] InferColumnTypes(int columnCount, IReadOnlyList<string[]> rows)
        {
            var types = new InferredSqlType[columnCount];
            for (var c = 0; c < columnCount; c++)
            {
                var best = InferredSqlType.Bit;
                var sawAnyValue = false;

                foreach (var row in rows)
                {
                    var value = c < row.Length ? row[c] : null;
                    if (IsNullText(value))
                    {
                        continue;
                    }

                    sawAnyValue = true;
                    var t = InferCellType(value);
                    if (t > best)
                    {
                        best = t;
                    }
                }

                types[c] = sawAnyValue ? best : InferredSqlType.NVarChar; // columna toda NULL: texto por defecto
            }

            return types;
        }

        private static InferredSqlType InferCellType(string value)
        {
            if (value == "0" || value == "1")
            {
                return InferredSqlType.Bit;
            }
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                return InferredSqlType.Int;
            }
            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            {
                return InferredSqlType.BigInt;
            }
            if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
            {
                return InferredSqlType.Decimal;
            }
            if (Guid.TryParse(value, out _))
            {
                return InferredSqlType.UniqueIdentifier;
            }
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
            {
                return InferredSqlType.DateTime2;
            }
            return InferredSqlType.NVarChar;
        }

        public static string FormatLiteral(string value, InferredSqlType type)
        {
            if (IsNullText(value))
            {
                return NullLiteral;
            }

            switch (type)
            {
                case InferredSqlType.Bit:
                case InferredSqlType.Int:
                case InferredSqlType.BigInt:
                case InferredSqlType.Decimal:
                    return value;
                case InferredSqlType.UniqueIdentifier:
                    return "'" + EscapeQuotes(value) + "'";
                case InferredSqlType.DateTime2:
                    return "'" + EscapeQuotes(value) + "'";
                default:
                    return "N'" + EscapeQuotes(value) + "'";
            }
        }

        public static bool IsNullText(string value)
        {
            return value == null || value == NullLiteral;
        }

        private static string EscapeQuotes(string value) => value.Replace("'", "''");

        public static string QuoteIdentifier(string identifier) => "[" + identifier.Replace("]", "]]") + "]";

        private static List<IReadOnlyList<string[]>> Partition(IReadOnlyList<string[]> rows, int blockSize)
        {
            var blocks = new List<IReadOnlyList<string[]>>();
            for (var i = 0; i < rows.Count; i += blockSize)
            {
                var count = Math.Min(blockSize, rows.Count - i);
                blocks.Add(rows.Skip(i).Take(count).ToArray());
            }
            return blocks;
        }
    }
}
