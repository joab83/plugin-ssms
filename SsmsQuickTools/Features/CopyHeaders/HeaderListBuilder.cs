using System.Collections.Generic;
using System.Text;

namespace SsmsQuickTools.Features.CopyHeaders
{
    /// <summary>
    /// Arma una linea con los nombres de columna entre corchetes separados por <c>", "</c>
    /// (<c>[A], [B], [C]</c>). Logica pura, sin dependencias de SSMS. Un <c>]</c> dentro del
    /// nombre se duplica (regla de identificadores T-SQL). No quita nombres repetidos.
    /// </summary>
    public static class HeaderListBuilder
    {
        public static string Build(IEnumerable<string> columns)
        {
            var sb = new StringBuilder();
            if (columns == null)
            {
                return string.Empty;
            }

            var first = true;
            foreach (var name in columns)
            {
                if (!first)
                {
                    sb.Append(", ");
                }
                first = false;

                sb.Append('[').Append((name ?? string.Empty).Replace("]", "]]")).Append(']');
            }

            return sb.ToString();
        }
    }
}
