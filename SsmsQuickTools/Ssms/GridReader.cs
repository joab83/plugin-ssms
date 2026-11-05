using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Microsoft.SqlServer.Management.UI.Grid;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;

namespace SsmsQuickTools.Ssms
{
    /// <summary>
    /// Lee el grid de resultados activo de SSMS usando la interfaz publica
    /// <see cref="IGridControl"/> (Microsoft.SqlServer.GridControl.dll), la misma que usa
    /// SSMS para su propio "Copy"/"Copy with headers". Solo se recurre a busqueda de
    /// controles WinForms (fragil entre versiones) para *encontrar* la instancia del grid;
    /// una vez encontrada, todo el acceso a datos es a traves de la interfaz publica,
    /// no de campos internos por reflection.
    /// </summary>
    public sealed class GridReader : IResultSetReader
    {
        public ResultSetData TryRead(out string failureReason)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var grid = TryFindGrid(out failureReason);
            if (grid == null)
            {
                return null;
            }

            try
            {
                var hasSelection = grid.SelectedCells != null && grid.SelectedCells.Count > 0;
                var dataObject = grid.GetDataObject(hasSelection, true);
                var text = dataObject?.GetData(DataFormats.UnicodeText) as string
                           ?? dataObject?.GetData(DataFormats.Text) as string;

                if (string.IsNullOrWhiteSpace(text))
                {
                    failureReason = "El grid no devolvio datos (¿esta vacio?).";
                    return null;
                }

                var parsed = TsvParser.Parse(text);
                if (parsed.Columns.Count == 0 || parsed.Rows.Count == 0)
                {
                    failureReason = "El grid no tiene filas para exportar.";
                    return null;
                }

                failureReason = null;
                return new ResultSetData(parsed.Columns, parsed.Rows, hasSelection);
            }
            catch (Exception ex)
            {
                failureReason = "Error leyendo el grid: " + ex.Message;
                return null;
            }
        }

        /// <summary>
        /// Nombres de todas las columnas del grid, en orden, leidos de los encabezados
        /// (<c>GetHeaderInfo</c>), sin depender de <c>GetDataObject</c>. El indice UI 0 es la
        /// columna del numero de fila y se omite.
        /// </summary>
        public IReadOnlyList<string> TryReadAllColumnNames(out string failureReason)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var grid = TryFindGrid(out failureReason);
            if (grid == null)
            {
                return null;
            }

            try
            {
                var names = new List<string>();
                for (var x = 1; x < grid.ColumnsNumber; x++)
                {
                    names.Add(GetHeaderText(grid, x));
                }

                if (names.Count == 0)
                {
                    failureReason = "El grid no tiene columnas.";
                    return null;
                }

                failureReason = null;
                return names;
            }
            catch (Exception ex)
            {
                failureReason = "Error leyendo los encabezados del grid: " + ex.Message;
                return null;
            }
        }

        /// <summary>
        /// Nombres de las columnas que tienen celdas seleccionadas, en el orden del grid. Recorre
        /// todos los bloques de <c>SelectedCells</c> (uno por rango / Ctrl+click) y lee cada
        /// encabezado con <c>GetHeaderInfo</c>; <c>GetDataObject</c> no sirve: con columnas no
        /// contiguas devuelve solo una. Devuelve null si no hay seleccion o no se pudo leer
        /// (<paramref name="hasSelection"/> distingue ambos casos; el motivo queda en
        /// <paramref name="failureReason"/>).
        /// </summary>
        public IReadOnlyList<string> TryReadSelectedColumnNames(out bool hasSelection, out string failureReason)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            hasSelection = false;

            var grid = TryFindGrid(out failureReason);
            if (grid == null)
            {
                return null;
            }

            try
            {
                var blocks = grid.SelectedCells;
                if (blocks == null || blocks.Count == 0)
                {
                    failureReason = null;
                    return null;
                }

                // Indices UI de columna de datos (>= 1; el 0 es el numero de fila).
                var selected = new SortedSet<int>();
                foreach (BlockOfCells block in blocks)
                {
                    for (var x = Math.Max(block.X, 1); x < block.X + block.Width && x < grid.ColumnsNumber; x++)
                    {
                        selected.Add(x);
                    }
                }

                if (selected.Count == 0)
                {
                    failureReason = null;
                    return null;
                }

                hasSelection = true;
                failureReason = null;
                return selected.Select(x => GetHeaderText(grid, x)).ToList();
            }
            catch (Exception ex)
            {
                hasSelection = true;
                failureReason = "Error leyendo la seleccion del grid: " + ex.Message;
                return null;
            }
        }

        private static string GetHeaderText(IGridControl grid, int uiColumnIndex)
        {
            grid.GetHeaderInfo(uiColumnIndex, out string text, out System.Drawing.Bitmap _);
            return text ?? string.Empty;
        }

        private static IGridControl TryFindGrid(out string failureReason)
        {
            IVsWindowFrame frame;
            try
            {
                frame = GetActiveWindowFrame();
            }
            catch (Exception ex)
            {
                failureReason = "No se pudo obtener la ventana activa: " + ex.Message;
                return null;
            }

            if (frame == null)
            {
                failureReason = "No hay una ventana de resultados activa.";
                return null;
            }

            Control root;
            try
            {
                root = GetFrameControl(frame);
            }
            catch (Exception ex)
            {
                failureReason = "No se pudo acceder al control de la ventana activa: " + ex.Message;
                return null;
            }

            if (root == null)
            {
                failureReason = "La ventana activa no expone un control de UI accesible.";
                return null;
            }

            var grid = FindGridControl(root);
            if (grid == null)
            {
                failureReason = "No se encontro un grid de resultados en la ventana activa. " +
                                 "Ejecuta una consulta y hace click en el grid de resultados antes de usar este comando.";
                return null;
            }

            failureReason = null;
            return grid;
        }

        private static IVsWindowFrame GetActiveWindowFrame()
        {
            var monitorSelection = ServiceProvider.GlobalProvider.GetService(typeof(SVsShellMonitorSelection)) as IVsMonitorSelection;
            if (monitorSelection == null)
            {
                return null;
            }

            monitorSelection.GetCurrentElementValue((uint)VSConstants.VSSELELEMID.SEID_WindowFrame, out var frameObj);
            return frameObj as IVsWindowFrame;
        }

        private static Control GetFrameControl(IVsWindowFrame frame)
        {
            // VSFPROPID_DocView es el objeto de vista del documento. La ventana de query de
            // SSMS (editor + grid de resultados) es WinForms clasico, asi que este objeto es
            // directamente un Control (a diferencia del editor de texto moderno de VS, que
            // expone un objeto COM en su lugar).
            if (ErrorHandler.Succeeded(frame.GetProperty((int)__VSFPROPID.VSFPROPID_DocView, out var docView)) && docView is Control docViewControl)
            {
                return docViewControl;
            }

            return null;
        }

        /// <summary>
        /// Recorre el arbol de controles WinForms buscando uno que implemente IGridControl.
        /// Si hay varios (varios result sets en tabs), se prioriza el que tiene el foco.
        /// </summary>
        private static IGridControl FindGridControl(Control root)
        {
            IGridControl found = null;
            IGridControl focused = null;
            var stack = new System.Collections.Generic.Stack<Control>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                var current = stack.Pop();

                if (current is IGridControl gridControl)
                {
                    if (found == null)
                    {
                        found = gridControl;
                    }
                    if (current.ContainsFocus)
                    {
                        focused = gridControl;
                    }
                }

                foreach (Control child in current.Controls)
                {
                    stack.Push(child);
                }
            }

            return focused ?? found;
        }
    }
}
