using System;
using System.ComponentModel.Design;
using System.Windows.Forms;
using Microsoft.VisualStudio.Shell;
using SsmsQuickTools.Ssms;

namespace SsmsQuickTools.Features.AutoReplacement
{
    /// <summary>
    /// Comando manual "Expandir token" en el menu Quick Tools, con atajo de teclado. Plan C
    /// (docs/PLAN.md:309-313): entrega la funcion aunque el filtro de comandos sobre Enter/Tab no se
    /// enganche en alguna version futura de SSMS. Ejecuta la misma logica que
    /// AutoReplacementCommandFilter, sobre la vista activa en lugar de "su" vista.
    /// </summary>
    public sealed class ExpandTokenCommand
    {
        private readonly AsyncPackage _package;
        private readonly OleMenuCommandService _commandService;
        private readonly AutoReplacementCatalog _catalog;

        public ExpandTokenCommand(AsyncPackage package, OleMenuCommandService commandService, AutoReplacementCatalog catalog)
        {
            _package = package;
            _commandService = commandService;
            _catalog = catalog;
        }

        public void Register()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var commandId = new CommandID(PackageGuids.QuickToolsCmdSet, (int)PkgCmdId.ExpandTokenCommand);
            var command = new OleMenuCommand(OnExpandToken, commandId);
            _commandService.AddCommand(command);
        }

        private void OnExpandToken(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var view = SsmsHost.GetActiveTextView();
            if (view == null)
            {
                return;
            }

            if (!TextViewEditor.TryGetLineTextToCaret(view, out var caretLine, out var caretColumn, out var linePrefix))
            {
                return;
            }

            var token = TokenScanner.ExtractTokenBeforeCaret(linePrefix);
            if (token == null)
            {
                MessageBox.Show("No hay ningun token pegado al cursor.", "SSMS Quick Tools", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var entry = _catalog.FindByToken(token.Text);
            if (entry == null)
            {
                MessageBox.Show($"'{token.Text}' no es un token configurado en autoreplacement.xml.", "SSMS Quick Tools", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var textUpToCaret = TextViewEditor.GetTextFromStartToCaret(view, caretLine, caretColumn);
            if (SqlContextScanner.IsInsideLiteralOrComment(textUpToCaret))
            {
                MessageBox.Show("El cursor esta dentro de una cadena o un comentario.", "SSMS Quick Tools", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var expansion = AutoReplacementExpander.Build(entry);

            TextViewEditor.ReplaceSpan(view, caretLine, token.StartColumn, caretLine, token.EndColumn, expansion.Text);
            TextViewEditor.PlaceCaret(view, caretLine, token.StartColumn, expansion.Text, expansion.CaretOffset, expansion.SelectAll);
        }
    }
}
