// VSTO host integration. Keep the host blueprint and these members consistent.
#pragma warning disable 414
namespace JitsiAdmin.OutlookPlugin
{
    [Microsoft.VisualStudio.Tools.Applications.Runtime.StartupObjectAttribute(0)]
    [System.Security.Permissions.PermissionSetAttribute(System.Security.Permissions.SecurityAction.Demand, Name = "FullTrust")]
    public sealed partial class ThisAddIn : Microsoft.Office.Tools.Outlook.OutlookAddInBase
    {
        internal Microsoft.Office.Interop.Outlook.Application Application;
        public ThisAddIn(Microsoft.Office.Tools.Outlook.Factory factory, System.IServiceProvider serviceProvider)
            : base(factory, serviceProvider, "AddIn", "ThisAddIn") { Globals.Factory = factory; }
        protected override void Initialize()
        {
            base.Initialize();
            Application = GetHostItem<Microsoft.Office.Interop.Outlook.Application>(typeof(Microsoft.Office.Interop.Outlook.Application), "Application");
            Globals.ThisAddIn = this;
            System.Windows.Forms.Application.EnableVisualStyles();
            if (DataHost != null && DataHost.IsCacheInitialized) DataHost.FillCachedData(this);
        }
        protected override void FinishInitialization() { InternalStartup(); OnStartup(); }
        protected override void InitializeDataBindings() { BeginInitialization(); EndInitialization(); }
        protected override void OnShutdown() { base.OnShutdown(); }
    }
    internal static class Globals
    {
        internal static ThisAddIn ThisAddIn { get; set; }
        internal static Microsoft.Office.Tools.Outlook.Factory Factory { get; set; }
    }
}
