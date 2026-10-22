using System;
using System.ComponentModel.Design;
using System.Data;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.SqlServer.Management.Smo.RegSvrEnum;
using Microsoft.VisualStudio.Shell;
using SsmsQuickTools.Ssms;

namespace SsmsQuickTools.Features.QuickConnect
{
    /// <summary>
    /// Combo "Quick Connections" de la toolbar. Al elegir una conexion, reconecta la ventana de
    /// query activa (cambia conexion + hace USE de la base indicada en connections.json).
    /// </summary>
    public sealed class QuickConnectCommands
    {
        private readonly AsyncPackage _package;
        private readonly OleMenuCommandService _commandService;
        private readonly ConnectionCatalog _catalog;

        private string _selectedConnectionName;

        public QuickConnectCommands(AsyncPackage package, OleMenuCommandService commandService, ConnectionCatalog catalog)
        {
            _package = package;
            _commandService = commandService;
            _catalog = catalog;
        }

        public void Register()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            AddCommand(PkgCmdId.ConnectionCombo, OnConnectionCombo);
            AddCommand(PkgCmdId.ConnectionComboGetList, OnConnectionComboGetList);
        }

        private void AddCommand(uint id, EventHandler handler)
        {
            var commandId = new CommandID(PackageGuids.QuickToolsCmdSet, (int)id);
            var command = new OleMenuCommand(handler, commandId);
            _commandService.AddCommand(command);
        }

        private void OnConnectionCombo(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            if (!(e is OleMenuCmdEventArgs args))
            {
                return;
            }

            if (args.InValue is string newChoice)
            {
                var connection = _catalog.FindConnection(newChoice);
                if (connection == null)
                {
                    return;
                }

                _selectedConnectionName = connection.Name;
                ConnectToSelection();
                return;
            }

            if (args.OutValue != IntPtr.Zero)
            {
                Marshal.GetNativeVariantForObject(_selectedConnectionName ?? string.Empty, args.OutValue);
            }
        }

        private void OnConnectionComboGetList(object sender, EventArgs e)
        {
            if (!(e is OleMenuCmdEventArgs args) || args.OutValue == IntPtr.Zero)
            {
                return;
            }

            var names = _catalog.Connections.Select(c => c.Name).ToArray();
            Marshal.GetNativeVariantForObject(names, args.OutValue);
        }

        private void ConnectToSelection()
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            var connection = _catalog.FindConnection(_selectedConnectionName);
            if (connection == null)
            {
                return;
            }

            var connectionInfo = new UIConnectionInfo
            {
                ServerName = connection.Server,
                AuthenticationType = 0, // Windows Authentication
                ApplicationName = "SsmsQuickTools",
                // Database Engine: sin este GUID, VerifyConnectionInfo (ConnectionDlg.dll) rechaza
                // la conexion con "Tipo de conexion inesperado".
                ServerType = new Guid("8c91a03d-f9b4-46c0-a305-b5dcc79ff907"),
            };
            connectionInfo.AdvancedOptions["DATABASE"] = connection.Database;

            // Reconecta in-place la ventana activa (mismo servidor: solo cambia de base; servidor
            // distinto: desconecta y reconecta). OpenConnection solo se invoca cuando SsmsHost
            // necesita una conexion ADO.NET nueva para una reconexion completa; en ese caso la
            // conexion queda en poder de SsmsHost (adoptada por la ventana si tiene exito,
            // descartada si falla).
            var outcome = SsmsHost.TryReconnectActiveWindow(
                connectionInfo,
                connection.Database,
                () => OpenConnection(connectionInfo, connection.Database),
                out var reason);

            switch (outcome)
            {
                case ReconnectOutcome.DatabaseChanged:
                case ReconnectOutcome.Reconnected:
                    return;

                case ReconnectOutcome.NoQueryWindow:
                    // No hay ninguna ventana de query activa: se abre una nueva ya conectada, con
                    // una conexion ADO.NET propia (SsmsHost no abrio ninguna para este caso).
                    var dbConnection = OpenConnection(connectionInfo, connection.Database);
                    try
                    {
                        SsmsHost.OpenNewScriptWindow(null, connectionInfo, dbConnection);
                    }
                    catch
                    {
                        dbConnection.Dispose();
                        throw;
                    }
                    return;

                case ReconnectOutcome.Blocked:
                case ReconnectOutcome.Failed:
                    MessageBox.Show(reason, "SSMS Quick Tools", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
            }
        }

        private static IDbConnection OpenConnection(UIConnectionInfo connectionInfo, string database)
        {
            // Microsoft.Data.SqlClient usa Encrypt=Mandatory por defecto, y SSMS lo muestra asi en la
            // ventana; se fuerza Optional (como el dialogo de conexion de SSMS).
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(
                SsmsHost.BuildConnectionString(connectionInfo, database))
            {
                Encrypt = Microsoft.Data.SqlClient.SqlConnectionEncryptOption.Optional,
            };
            var dbConnection = new Microsoft.Data.SqlClient.SqlConnection(builder.ConnectionString);
            try
            {
                dbConnection.Open();
                return dbConnection;
            }
            catch
            {
                dbConnection.Dispose();
                throw;
            }
        }
    }
}
