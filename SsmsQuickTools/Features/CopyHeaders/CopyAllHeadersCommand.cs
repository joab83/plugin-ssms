using System;
using System.ComponentModel.Design;
using System.Windows.Forms;
using Microsoft.VisualStudio.Shell;
using SsmsQuickTools.Ssms;

namespace SsmsQuickTools.Features.CopyHeaders
{
    /// <summary>
    /// Comando "Copy All Headers": copia al portapapeles los nombres de todas las columnas del
    /// grid activo como <c>[A], [B], [C]</c>, haya o no celdas seleccionadas.
    /// </summary>
    public sealed class CopyAllHeadersCommand
    {
        private readonly AsyncPackage _package;
        private readonly OleMenuCommandService _commandService;

        public CopyAllHeadersCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            _package = package;
            _commandService = commandService;
        }

        public void Register()
        {
            var commandId = new CommandID(PackageGuids.QuickToolsCmdSet, (int)PkgCmdId.CopyAllHeadersCommand);
            var command = new OleMenuCommand(Execute, commandId);
            _commandService.AddCommand(command);
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var columns = new GridReader().TryReadAllColumnNames(out var reason);
            if (columns == null)
            {
                MessageBox.Show(reason, "SSMS Quick Tools", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                Clipboard.SetText(HeaderListBuilder.Build(columns), TextDataFormat.UnicodeText);
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo copiar al portapapeles: " + ex.Message, "SSMS Quick Tools", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
