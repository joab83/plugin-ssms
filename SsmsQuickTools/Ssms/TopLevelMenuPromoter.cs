using System;
using Microsoft.VisualStudio.Settings;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Settings;
using SsmsQuickTools.Features.TopLevelMenu;

namespace SsmsQuickTools.Ssms
{
    /// <summary>
    /// Saca el menu "Quick Tools" de Extensiones y lo deja en la barra principal, imitando lo que
    /// hace el dialogo Extensiones > Personalizar menu al destildar un menu: agrega su entrada a
    /// <c>Menus\AllowedExtensionMenus</c> (UserSettings). SSMS lee esa lista al arrancar, asi que
    /// el efecto se ve desde el segundo arranque tras instalar. API interna no documentada
    /// (verificada contra SSMS 22.6, TopLevelMenuPage): si cambia, el menu simplemente queda
    /// dentro de Extensiones, como antes.
    /// </summary>
    internal static class TopLevelMenuPromoter
    {
        private const string AllowedCollection = "Menus\\AllowedExtensionMenus";
        private const string OwnCollection = "SsmsQuickTools";
        private const string PromotedProperty = "TopLevelMenuPromoted";

        /// <summary>Debe llamarse en el hilo principal. Nunca lanza.</summary>
        public static void EnsurePromoted(IServiceProvider provider)
        {
            try
            {
                var store = new ShellSettingsManager(provider).GetWritableSettingsStore(SettingsScope.UserSettings);

                // Una sola vez: si el usuario despues lo vuelve a tildar en Personalizar menu, se respeta.
                if (store.CollectionExists(OwnCollection) && store.GetBoolean(OwnCollection, PromotedProperty, false))
                {
                    return;
                }

                if (!store.CollectionExists(AllowedCollection))
                {
                    store.CreateCollection(AllowedCollection);
                }

                string entry = AllowedMenuList.FormatEntry(PackageGuids.QuickToolsCmdSet, PkgCmdId.QuickToolsMenu);
                string existing = store.PropertyExists(AllowedCollection, string.Empty)
                    ? store.GetString(AllowedCollection, string.Empty, string.Empty)
                    : string.Empty;
                string updated = AllowedMenuList.AddEntry(existing, entry);

                if (!string.Equals(existing, updated, StringComparison.Ordinal))
                {
                    store.SetString(AllowedCollection, string.Empty, updated);
                    store.SetInt64(string.Empty, "ConfigurationChanged", DateTime.Now.ToFileTimeUtc());
                }

                if (!store.CollectionExists(OwnCollection))
                {
                    store.CreateCollection(OwnCollection);
                }

                store.SetBoolean(OwnCollection, PromotedProperty, true);
            }
            catch (Exception ex)
            {
                ActivityLog.TryLogWarning(nameof(SsmsQuickToolsPackage), "TopLevelMenuPromoter: " + ex.Message);
            }
        }
    }
}
