using System;
using System.Collections.Generic;
using System.Text;
using SsmsQuickTools.Features.ScriptData;

namespace SsmsQuickTools.Features.CommaList
{
    /// <summary>
    /// Arma, por cada columna del resultado, una linea con sus valores separados por coma
    /// (<c>a, b, c</c>), lista para pegar en un <c>IN (...)</c>. Logica pura, sin dependencias
    /// de SSMS: reusa la inferencia de tipos y el formateo de literales de
    /// <see cref="ValuesScriptBuilder"/>. Omite NULL y quita duplicados conservando el orden
    /// de primera aparicion.
    /// </summary>
    public static class CommaListBuilder
    {
        public static string Build(IReadOnlyList<string> columns, IReadOnlyList<string[]> rows)
        {
            if (columns == null || columns.Count == 0)
            {
                throw new ArgumentException("Se necesita al menos una columna.", nameof(columns));
            }
            if (rows == null)
            {
                throw new ArgumentException("Filas nulas.", nameof(rows));
            }

            var types = ValuesScriptBuilder.InferColumnTypes(columns.Count, rows);
            var sb = new StringBuilder();

            for (var c = 0; c < columns.Count; c++)
            {
                if (c > 0)
                {
                    sb.AppendLine();
                }

                var seen = new HashSet<string>(StringComparer.Ordinal);
                var first = true;
                foreach (var row in rows)
                {
                    var raw = c < row.Length ? row[c] : null;
                    if (ValuesScriptBuilder.IsNullText(raw) || !seen.Add(raw))
                    {
                        continue;
                    }

                    if (!first)
                    {
                        sb.Append(", ");
                    }
                    sb.Append(ValuesScriptBuilder.FormatLiteral(raw, types[c]));
                    first = false;
                }
            }

            return sb.ToString();
        }
    }
}
