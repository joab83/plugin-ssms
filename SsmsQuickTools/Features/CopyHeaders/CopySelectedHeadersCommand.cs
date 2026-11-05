using System;
using System.ComponentModel.Design;
using System.Windows.Forms;
using Microsoft.VisualStudio.Shell;
using SsmsQuickTools.Ssms;

namespace SsmsQuickTools.Features.CopyHeaders
{
    /// <summary>
    /// Comando "Copy Selected Headers": copia al portapapeles los nombres de las columnas con
    /// celdas seleccionadas en el grid activo como <c>[A], [B], [C]</c>. Requiere seleccion
    /// explicita (sin fallback de portapapeles ni "todos los headers").
    /// </summary>
    public sealed class CopySelectedHeadersCommand
    {
        private readonly AsyncPackage _package;
        private readonly OleMenuCommandService _commandService;

        public CopySelectedHeadersCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            _package = package;
            _commandService = commandService;
        }

        public void Register()
        {
            var commandId = new CommandID(PackageGuids.QuickToolsCmdSet, (int)PkgCmdId.CopySelectedHeadersCommand);
            var command = new OleMenuCommand(Execute, commandId);
            _commandService.AddCommand(command);
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var columns = new GridReader().TryReadSelectedColumnNames(out var hasSelection, out var reason);
            if (columns == null)
            {
                MessageBox.Show(
                    hasSelection || reason != null
                        ? reason ?? "No se pudo leer la seleccion del grid."
                        : "Selecciona celdas en el grid de resultados antes de usar este comando.",
                    "SSMS Quick Tools",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
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
