using System;
using System.ComponentModel.Design;
using System.Windows.Forms;
using Microsoft.VisualStudio.Shell;
using SsmsQuickTools.Ssms;

namespace SsmsQuickTools.Features.CommaList
{
    /// <summary>
    /// Comando "Copiar seleccion como lista separada por coma": lee la seleccion del grid
    /// activo (sin fallback de portapapeles: requiere seleccion explicita), arma una linea
    /// por columna con <see cref="CommaListBuilder"/> y la deja en el portapapeles.
    /// </summary>
    public sealed class CopyCommaListCommand
    {
        private readonly AsyncPackage _package;
        private readonly OleMenuCommandService _commandService;

        public CopyCommaListCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            _package = package;
            _commandService = commandService;
        }

        public void Register()
        {
            var commandId = new CommandID(PackageGuids.QuickToolsCmdSet, (int)PkgCmdId.CopyCommaListCommand);
            var command = new OleMenuCommand(Execute, commandId);
            _commandService.AddCommand(command);
        }

        private void Execute(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var data = new GridReader().TryRead(out var reason);
            if (data == null)
            {
                MessageBox.Show(reason, "SSMS Quick Tools", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!data.HasSelection)
            {
                MessageBox.Show(
                    "Selecciona celdas en el grid de resultados antes de usar este comando.",
                    "SSMS Quick Tools",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            string text;
            try
            {
                text = CommaListBuilder.Build(data.Columns, data.Rows);
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo generar la lista: " + ex.Message, "SSMS Quick Tools", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            try
            {
                Clipboard.SetText(text, TextDataFormat.UnicodeText);
            }
            catch (Exception ex)
            {
                MessageBox.Show("No se pudo copiar al portapapeles: " + ex.Message, "SSMS Quick Tools", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
