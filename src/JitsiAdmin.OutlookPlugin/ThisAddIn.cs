using System;
using Office = Microsoft.Office.Core;

namespace JitsiAdmin.OutlookPlugin
{
    public partial class ThisAddIn
    {
        private AddInController controller;
        private void ThisAddIn_Startup(object sender, EventArgs e)
        {
            try { EnsureController(); }
            catch { System.Windows.Forms.MessageBox.Show("Meet Link konnte nicht gestartet werden. Bitte Installation prüfen.", "Meet Link"); }
        }
        private void ThisAddIn_Shutdown(object sender, EventArgs e) { try { controller?.Dispose(); } catch { /* Outlook is shutting down; no modal error UI. */ } finally { controller = null; } }
        private AddInController EnsureController() => controller ?? (controller = new AddInController(Application));
        protected override Office.IRibbonExtensibility CreateRibbonExtensibilityObject()
        {
            try { return EnsureController().Ribbon; }
            catch { System.Windows.Forms.MessageBox.Show("Meet Link konnte das Menü nicht laden. Bitte Installation prüfen.", "Meet Link"); return null; }
        }
        private void InternalStartup() { Startup += ThisAddIn_Startup; Shutdown += ThisAddIn_Shutdown; }
    }
}
