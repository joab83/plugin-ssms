using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.VisualStudio.TextManager.Interop;

namespace SsmsQuickTools.Ssms
{
    /// <summary>
    /// Helpers de bajo nivel sobre un <see cref="IVsTextView"/> puntual, para el filtro de
    /// comandos de Auto Replacement (que trabaja sobre "su" vista, no sobre "la vista activa" como
    /// <see cref="SsmsHost"/>). Mismo layer COM legacy (IVsTextView/IVsTextLines) que SsmsHost.
    /// </summary>
    internal static class TextViewEditor
    {
        public static bool HasSelection(IVsTextView view)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (ErrorHandler.Failed(view.GetSelection(out var anchorLine, out var anchorCol, out var endLine, out var endCol)))
            {
                return false;
            }

            return anchorLine != endLine || anchorCol != endCol;
        }

        public static bool TryGetLineTextToCaret(IVsTextView view, out int caretLine, out int caretColumn, out string linePrefix)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            caretLine = 0;
            caretColumn = 0;
            linePrefix = null;

            if (ErrorHandler.Failed(view.GetCaretPos(out caretLine, out caretColumn)))
            {
                return false;
            }

            if (ErrorHandler.Failed(view.GetBuffer(out var buffer)) || buffer == null)
            {
                return false;
            }

            if (ErrorHandler.Failed(buffer.GetLineText(caretLine, 0, caretLine, caretColumn, out linePrefix)))
            {
                return false;
            }

            return true;
        }

        public static string GetTextFromStartToCaret(IVsTextView view, int caretLine, int caretColumn)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (ErrorHandler.Failed(view.GetBuffer(out var buffer)) || buffer == null)
            {
                return string.Empty;
            }

            if (ErrorHandler.Failed(buffer.GetLineText(0, 0, caretLine, caretColumn, out var text)))
            {
                return string.Empty;
            }

            return text ?? string.Empty;
        }

        public static void ReplaceSpan(IVsTextView view, int startLine, int startCol, int endLine, int endCol, string text)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (ErrorHandler.Failed(view.GetBuffer(out var buffer)) || buffer == null)
            {
                return;
            }

            var textPtr = Marshal.StringToCoTaskMemUni(text);
            try
            {
                buffer.ReplaceLines(startLine, startCol, endLine, endCol, textPtr, text.Length, new TextSpan[1]);
            }
            finally
            {
                Marshal.FreeCoTaskMem(textPtr);
            }
        }

        /// <summary>
        /// Ubica el cursor (o la seleccion completa) tras insertar <paramref name="text"/> en
        /// <paramref name="anchorLine"/>/<paramref name="anchorColumn"/>.
        /// </summary>
        public static void PlaceCaret(IVsTextView view, int anchorLine, int anchorColumn, string text, int caretOffset, bool selectAll)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var (endLine, endCol) = OffsetToPosition(anchorLine, anchorColumn, text, text.Length);

            if (selectAll)
            {
                view.SetSelection(anchorLine, anchorColumn, endLine, endCol);
                return;
            }

            var (caretLine, caretCol) = OffsetToPosition(anchorLine, anchorColumn, text, caretOffset);
            view.SetCaretPos(caretLine, caretCol);
        }

        private static (int line, int column) OffsetToPosition(int anchorLine, int anchorColumn, string text, int offset)
        {
            var upToOffset = text.Substring(0, offset);
            var lines = Regex.Split(upToOffset, "\r\n|\r|\n");

            if (lines.Length == 1)
            {
                return (anchorLine, anchorColumn + lines[0].Length);
            }

            return (anchorLine + lines.Length - 1, lines[lines.Length - 1].Length);
        }
    }
}
