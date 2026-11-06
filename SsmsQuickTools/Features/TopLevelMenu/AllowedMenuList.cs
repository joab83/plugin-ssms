using System;
using System.Collections.Generic;
using System.Linq;

namespace SsmsQuickTools.Features.TopLevelMenu
{
    /// <summary>
    /// Logica pura sobre el valor de <c>Menus\AllowedExtensionMenus</c>: lista separada por ';' de
    /// entradas "{guid},id" (formato que escribe el dialogo "Personalizar menu" de SSMS).
    /// </summary>
    internal static class AllowedMenuList
    {
        public static string FormatEntry(Guid cmdSet, uint id)
        {
            return "{" + cmdSet.ToString().ToUpperInvariant() + "}," + id;
        }

        public static bool Contains(string existing, string entry)
        {
            return Split(existing).Any(e => string.Equals(e, entry, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Devuelve <paramref name="existing"/> sin cambios si la entrada ya esta.</summary>
        public static string AddEntry(string existing, string entry)
        {
            if (Contains(existing, entry))
            {
                return existing ?? string.Empty;
            }

            var items = Split(existing).ToList();
            items.Add(entry);
            return string.Join(";", items);
        }

        private static IEnumerable<string> Split(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return Enumerable.Empty<string>();
            }

            return value.Split(';')
                .Select(s => s.Trim())
                .Where(s => s.Length > 0);
        }
    }
}
