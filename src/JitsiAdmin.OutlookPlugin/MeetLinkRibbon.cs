using System.Runtime.InteropServices;
using System.Xml.Linq;
using Office = Microsoft.Office.Core;

namespace JitsiAdmin.OutlookPlugin
{
    [ComVisible(true)]
    public sealed class MeetLinkRibbon : Office.IRibbonExtensibility
    {
        private readonly AddInController controller;
        private static readonly XNamespace Ns = "http://schemas.microsoft.com/office/2009/07/customui";
        internal MeetLinkRibbon(AddInController controller) { this.controller = controller; }
        public string GetCustomUI(string ribbonID)
        {
            if (ribbonID != "Microsoft.Outlook.Explorer") return null;
            return new XElement(Ns + "customUI",
                new XElement(Ns + "ribbon",
                    new XElement(Ns + "tabs",
                        new XElement(Ns + "tab", new XAttribute("id", "MeetLinkTab"), new XAttribute("label", "Meet Link"),
                            new XElement(Ns + "group", new XAttribute("id", "MeetLinkGroup"), new XAttribute("label", "Meet Link – ohne API"),
                                new XElement(Ns + "button", new XAttribute("id", "MeetLinkCreateAppointment"),
                                    new XAttribute("label", "Kalendereintrag erstellen"),
                                    new XAttribute("screentip", "Neuen Outlook-Termin öffnen"),
                                    new XAttribute("supertip", "Öffnet einen lokalen Termin im Standardkalender. Speichern erfolgt mit den normalen Outlook-Befehlen. Keine API-Verbindung."),
                                    new XAttribute("onAction", "CreateAppointment"))))))).ToString();
        }
        public void CreateAppointment(Office.IRibbonControl control)
        {
            try { controller.CreateLocalAppointment(control.Context); }
            catch { controller.ReportBoundaryError(); }
        }
    }
}
