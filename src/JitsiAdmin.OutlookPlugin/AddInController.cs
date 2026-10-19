using System;
using System.Windows.Forms;
using System.Threading;
using Outlook = Microsoft.Office.Interop.Outlook;

namespace JitsiAdmin.OutlookPlugin
{
    // This preview deliberately has no API client, configuration loader or server cache.
    internal sealed class AddInController : IDisposable
    {
        private readonly Outlook.Application application;
        private readonly int uiThread = Thread.CurrentThread.ManagedThreadId;
        private bool stopped;
        internal MeetLinkRibbon Ribbon { get; }
        internal AddInController(Outlook.Application application)
        {
            this.application = application;
            Ribbon = new MeetLinkRibbon(this);
        }
        internal void CreateLocalAppointment(object context)
        {
            VerifyUiThread();
            if (stopped) return;
            if (!(context is Outlook.Explorer))
            {
                ShowMessage("Bitte den Kalenderbutton im Outlook-Hauptfenster verwenden.");
                return;
            }
            // CreateItem uses the default calendar. Do not alter native defaults, Save or Send.
            var appointment = (Outlook.AppointmentItem)application.CreateItem(Outlook.OlItemType.olAppointmentItem);
            appointment.Display(false);
        }
        internal void ShowMessage(string text)
        {
            if (!stopped) MessageBox.Show(text, "Meet Link", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        internal void ReportBoundaryError()
        {
            ShowMessage("Der Kalendereintrag konnte nicht geöffnet werden. Bitte Outlook Classic und die Installation prüfen.");
        }
        private void VerifyUiThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != uiThread) throw new InvalidOperationException("Outlook UI thread required.");
        }
        public void Dispose()
        {
            VerifyUiThread();
            if (stopped) return;
            stopped = true;
        }
    }
}
