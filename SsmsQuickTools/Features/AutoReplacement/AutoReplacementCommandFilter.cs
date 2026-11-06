using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.OLE.Interop;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.TextManager.Interop;
using SsmsQuickTools.Ssms;

namespace SsmsQuickTools.Features.AutoReplacement
{
    /// <summary>
    /// Filtro de comandos por ventana de query: intercepta Enter (VSStd2K.RETURN), Tab
    /// (VSStd2K.TAB) y Espacio (VSStd2K.TYPECHAR con ' ') y, si la palabra pegada al cursor es un
    /// token configurado fuera de una cadena/comentario, lo expande en lugar de insertar el salto
    /// de linea, el tab o el espacio. Cualquier otro caso, o cualquier error, delega siempre al
    /// siguiente target de la cadena.
    /// </summary>
    internal sealed class AutoReplacementCommandFilter : IOleCommandTarget
    {
        private readonly IVsTextView _view;
        private readonly AutoReplacementCatalog _catalog;
        private IOleCommandTarget _next;

        private AutoReplacementCommandFilter(IVsTextView view, AutoReplacementCatalog catalog)
        {
            _view = view;
            _catalog = catalog;
        }

        public static AutoReplacementCommandFilter Attach(IVsTextView view, AutoReplacementCatalog catalog)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var filter = new AutoReplacementCommandFilter(view, catalog);
            if (ErrorHandler.Failed(view.AddCommandFilter(filter, out var next)))
            {
                return null;
            }

            filter._next = next;
            return filter;
        }

        public void Detach()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _view.RemoveCommandFilter(this);
        }

        public int QueryStatus(ref Guid pguidCmdGroup, uint cCmds, OLECMD[] prgCmds, IntPtr pCmdText)
        {
            return _next?.QueryStatus(ref pguidCmdGroup, cCmds, prgCmds, pCmdText)
                ?? (int)Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_NOTSUPPORTED;
        }

        public int Exec(ref Guid pguidCmdGroup, uint nCmdID, uint nCmdexecopt, IntPtr pvaIn, IntPtr pvaOut)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var isReturn = pguidCmdGroup == VSConstants.VSStd2K && nCmdID == (uint)VSConstants.VSStd2KCmdID.RETURN;
            var isTab = pguidCmdGroup == VSConstants.VSStd2K && nCmdID == (uint)VSConstants.VSStd2KCmdID.TAB;
            var isSpace = pguidCmdGroup == VSConstants.VSStd2K && nCmdID == (uint)VSConstants.VSStd2KCmdID.TYPECHAR && IsSpaceChar(pvaIn);

            if (isReturn || isTab || isSpace)
            {
                try
                {
                    // Tab con seleccion indenta el bloque: no se convierte en expansion.
                    if (!(isTab && TextViewEditor.HasSelection(_view)) && TryExpand())
                    {
                        return VSConstants.S_OK;
                    }
                }
                catch
                {
                    // Cualquier fallo aca no debe romper el Enter/Tab del editor: se delega igual.
                }
            }

            return _next?.Exec(ref pguidCmdGroup, nCmdID, nCmdexecopt, pvaIn, pvaOut)
                ?? (int)Microsoft.VisualStudio.OLE.Interop.Constants.OLECMDERR_E_NOTSUPPORTED;
        }

        // TYPECHAR trae el caracter tipeado como VARIANT (ushort) en pvaIn.
        private static bool IsSpaceChar(IntPtr pvaIn)
        {
            if (pvaIn == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                return Marshal.GetObjectForNativeVariant(pvaIn) is ushort ch && ch == ' ';
            }
            catch
            {
                return false;
            }
        }

        private bool TryExpand()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!TextViewEditor.TryGetLineTextToCaret(_view, out var caretLine, out var caretColumn, out var linePrefix))
            {
                return false;
            }

            var token = TokenScanner.ExtractTokenBeforeCaret(linePrefix);
            if (token == null)
            {
                return false;
            }

            var entry = _catalog.FindByToken(token.Text);
            if (entry == null)
            {
                return false;
            }

            var textUpToCaret = TextViewEditor.GetTextFromStartToCaret(_view, caretLine, caretColumn);
            if (SqlContextScanner.IsInsideLiteralOrComment(textUpToCaret))
            {
                return false;
            }

            var expansion = AutoReplacementExpander.Build(entry);

            TextViewEditor.ReplaceSpan(_view, caretLine, token.StartColumn, caretLine, token.EndColumn, expansion.Text);
            TextViewEditor.PlaceCaret(_view, caretLine, token.StartColumn, expansion.Text, expansion.CaretOffset, expansion.SelectAll);

            return true;
        }
    }
}
