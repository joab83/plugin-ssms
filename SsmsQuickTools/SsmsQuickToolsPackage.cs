using System;
using System.ComponentModel.Design;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using SsmsQuickTools.Features.About;
using SsmsQuickTools.Features.AutoReplacement;
using SsmsQuickTools.Features.CommaList;
using SsmsQuickTools.Features.CopyHeaders;
using SsmsQuickTools.Features.CopyXmlSpreadsheet;
using SsmsQuickTools.Features.LocateObject;
using SsmsQuickTools.Features.QuickConnect;
using SsmsQuickTools.Features.ScriptData;
using SsmsQuickTools.Features.ScriptObject;
using SsmsQuickTools.Ssms;
using Task = System.Threading.Tasks.Task;

namespace SsmsQuickTools
{
    /// <summary>
    /// Paquete principal. SSMS nunca tiene una solucion abierta, asi que la carga se dispara
    /// por el contexto NoSolution (equivalente a "siempre", en SSMS) en lugar de un comando.
    /// </summary>
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuids.PackageString)]
    [ProvideAutoLoad(VSConstants.UICONTEXT.NoSolution_string, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    public sealed class SsmsQuickToolsPackage : AsyncPackage
    {
        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            await JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            TopLevelMenuPromoter.EnsurePromoted(this);

            var commandService = await GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService == null)
            {
                // Sin servicio de comandos no hay nada para inicializar; se registra igual
                // para no tirar abajo la carga del paquete completo.
                return;
            }

            var catalog = new ConnectionCatalog();
            new QuickConnectCommands(this, commandService, catalog).Register();
            new ScriptDataCommand(this, commandService).Register();
            new ScriptObjectCommands(this, commandService).Register();
            new CopyXmlSpreadsheetCommand(this, commandService).Register();
            new LocateObjectCommand(this, commandService).Register();
            new CopyCommaListCommand(this, commandService).Register();
            new CopyAllHeadersCommand(this, commandService).Register();
            new CopySelectedHeadersCommand(this, commandService).Register();

            var autoReplacementCatalog = new AutoReplacementCatalog();
            new AutoReplacementService(autoReplacementCatalog);

            new AboutCommand(this, commandService).Register();
        }
    }
}
