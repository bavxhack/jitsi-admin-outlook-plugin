using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using JitsiAdmin.OutlookPlugin.Core;
using Outlook = Microsoft.Office.Interop.Outlook;
using Word = Microsoft.Office.Interop.Word;

namespace JitsiAdmin.OutlookPlugin
{
    internal sealed class OutlookTarget : IConferenceTarget, IDisposable
    {
        private const string Marker = "MeetLinkState";
        private readonly Outlook.Application application;
        private readonly UiDispatcher ui;
        private Outlook.Explorer explorer;
        private Outlook.Inspector inspector;
        private object item;
        private bool closed;
        private bool pending;
        private Action stopEditor;
        public bool IsNewAppointmentFlow { get; private set; }
        public string Key { get; private set; }
        public OutlookTarget(Outlook.Application application, object context, UiDispatcher ui)
        {
            this.application = application; this.ui = ui; ui.Verify();
            if (context is Outlook.Explorer main)
            {
                explorer = main; IsNewAppointmentFlow = true;
                var identity = Marshal.GetIUnknownForObject(main);
                try { Key = "explorer:" + identity.ToInt64(); } finally { Marshal.Release(identity); }
                ((Outlook.ExplorerEvents_10_Event)explorer).Close += OnExplorerClose;
            }
            else if (context is Outlook.Inspector window)
            {
                inspector = window; item = inspector.CurrentItem;
                if (!(item is Outlook.AppointmentItem) && !(item is Outlook.MailItem)) throw new MeetLinkException("Dieser Outlook-Kontext wird nicht unterstützt.");
                if (!IsEditable) throw new MeetLinkException("Das Outlook-Element ist nicht bearbeitbar.");
                var id = GetProperty("MeetLinkContextId");
                Guid parsed;
                if (!Guid.TryParse(id, out parsed)) { id = Guid.NewGuid().ToString("D"); SetProperty("MeetLinkContextId", id); }
                Key = "item:" + id;
                ((Outlook.InspectorEvents_10_Event)inspector).Close += OnInspectorClose;
            }
            else throw new MeetLinkException("Bitte ein Outlook-Hauptfenster oder ein separates Bearbeitungsfenster verwenden.");
        }
        public bool IsEditable
        {
            get
            {
                ui.Verify(); if (closed) return false;
                try
                {
                    if (item == null)
                    {
                        Outlook.MAPIFolder folder = null;
                        try { folder = explorer?.CurrentFolder; return folder != null; }
                        finally { Release(folder); }
                    }
                    // Reading the captured inspector detects disconnected/closed windows. Never consult ActiveInspector.
                    var current = inspector.CurrentItem;
                    if (current == null) return false;
                    if (item is Outlook.MailItem mail && mail.Sent) return false;
                    if (item is Outlook.AppointmentItem appointment &&
                        (appointment.MeetingStatus == Outlook.OlMeetingStatus.olMeetingReceived ||
                         appointment.MeetingStatus == Outlook.OlMeetingStatus.olMeetingReceivedAndCanceled)) return false;
                    string entry = item is Outlook.MailItem m ? m.EntryID : ((Outlook.AppointmentItem)item).EntryID;
                    if (!string.IsNullOrEmpty(entry))
                    {
                        Outlook.PropertyAccessor accessor = item is Outlook.MailItem mm ? mm.PropertyAccessor : ((Outlook.AppointmentItem)item).PropertyAccessor;
                        try { if ((((int)accessor.GetProperty("http://schemas.microsoft.com/mapi/proptag/0x0FF40003")) & 1) == 0) return false; }
                        finally { Release(accessor); }
                    }
                    return true;
                }
                catch (COMException) { return false; }
            }
        }
        public bool HasConference { get { ui.Verify(); return pending || (item != null && !string.IsNullOrEmpty(GetProperty(Marker))); } }
        public void MarkPending(string serverId)
        {
            ui.Verify();
            if (item != null) { SetProperty(Marker, "pending-or-uncertain"); SetProperty("MeetLinkServerId", serverId); }
            pending = true;
        }
        public void MarkCreated(string serverId, Conference conference)
        {
            ui.Verify();
            if (!IsEditable) throw new MeetLinkException("Das ursprüngliche Fenster ist geschlossen.");
            if (item == null)
            {
                item = application.CreateItem(Outlook.OlItemType.olAppointmentItem);
                var appointment = (Outlook.AppointmentItem)item;
                // No subject/time/location/recurrence changes: Outlook supplies all native defaults.
                SetProperty("MeetLinkContextId", Guid.NewGuid().ToString("D"));
                SetProperty(Marker, "created-insertion-pending");
                SetProperty("MeetLinkServerId", serverId);
                appointment.Display(false);
                inspector = appointment.GetInspector;
                ((Outlook.InspectorEvents_10_Event)inspector).Close += OnInspectorClose;
            }
            SetProperty(Marker, "created-insertion-pending");
            SetProperty("MeetLinkServerId", serverId);
            if (conference.Id != null) SetProperty("MeetLinkConferenceId", conference.Id);
        }
        public async Task InsertAsync(ConfigurationSnapshot config, Conference conference, CancellationToken ct)
        {
            ui.Verify();
            if (!IsEditable) throw new MeetLinkException("Das ursprüngliche Element ist nicht mehr bearbeitbar.");
            string block = config.Heading + (config.MockMode ? " (MOCK – keine echte Konferenz)" : "") + "\r\n" +
                config.LinkLabel + " " + conference.JoinUrl.AbsoluteUri + "\r\n";
            if (item is Outlook.MailItem plain && plain.BodyFormat == Outlook.OlBodyFormat.olFormatPlain)
                plain.Body = (plain.Body ?? "") + "\r\n\r\n" + block;
            else
            {
                await WaitForEditorAsync(ct);
                ui.Verify();
                if (!IsEditable) throw new MeetLinkException("Das ursprüngliche Element ist nicht mehr bearbeitbar.");
                Word.Document document = null; Word.Range content = null; Word.Range range = null; Word.Range link = null;
                Word.Hyperlinks hyperlinks = null; Word.Hyperlink hyperlink = null;
                try
                {
                    document = (Word.Document)inspector.WordEditor;
                    if (document.ProtectionType != Word.WdProtectionType.wdNoProtection || document.ReadOnly)
                        throw new MeetLinkException("Der Outlook-Editor ist schreibgeschützt.");
                    content = document.Content;
                    int start = Math.Max(0, content.End - 1);
                    range = document.Range(start, start);
                    string prefix = "\r\r";
                    string wordBlock = block.Replace("\r\n", "\r");
                    range.InsertAfter(prefix + wordBlock);
                    int linkStart = start + prefix.Length + wordBlock.IndexOf(conference.JoinUrl.AbsoluteUri, StringComparison.Ordinal);
                    link = document.Range(linkStart, linkStart + conference.JoinUrl.AbsoluteUri.Length);
                    hyperlinks = document.Hyperlinks;
                    hyperlink = hyperlinks.Add(link, conference.JoinUrl.AbsoluteUri);
                }
                finally { Release(hyperlink); Release(hyperlinks); Release(link); Release(range); Release(content); Release(document); }
            }
            SetProperty(Marker, "inserted"); // No explicit Save or Send. Native Outlook persists these properties.
        }
        private Task WaitForEditorAsync(CancellationToken ct)
        {
            ui.Verify();
            var completion = new TaskCompletionSource<bool>();
            var timer = new System.Windows.Forms.Timer { Interval = 100 };
            var deadline = DateTime.UtcNow.AddSeconds(8);
            Outlook.InspectorEvents_10_Event events = (Outlook.InspectorEvents_10_Event)inspector;
            bool finished = false;
            Outlook.InspectorEvents_10_ActivateEventHandler activated = null;
            EventHandler tick = null;
            Action cleanup = () => { if (finished) return; finished = true; stopEditor = null; timer.Stop(); timer.Tick -= tick; timer.Dispose(); try { events.Activate -= activated; } catch (Exception e) when (e is COMException || e is InvalidComObjectException) { } };
            Action check = () => {
                if (finished) return;
                try
                {
                    ui.Verify();
                    if (ct.IsCancellationRequested) { cleanup(); completion.TrySetCanceled(); return; }
                    if (!IsEditable) throw new MeetLinkException("Das ursprüngliche Fenster wurde geschlossen.");
                    // WordEditor is documented only when both conditions hold.
                    if (inspector.IsWordMail() && inspector.EditorType == Outlook.OlEditorType.olEditorWord)
                    {
                        object editor = inspector.WordEditor;
                        bool ready = editor is Word.Document;
                        Release(editor);
                        if (ready) { cleanup(); completion.TrySetResult(true); return; }
                    }
                    if (DateTime.UtcNow >= deadline) throw new MeetLinkException("Der Outlook-Editor wurde nicht rechtzeitig bereit.");
                }
                catch (COMException)
                {
                    if (DateTime.UtcNow >= deadline) { cleanup(); completion.TrySetException(new MeetLinkException("Der Outlook-Editor wurde nicht rechtzeitig bereit.")); }
                }
                catch (Exception e) { cleanup(); completion.TrySetException(e); }
            };
            activated = () => check(); tick = (s, e) => check();
            stopEditor = () => { cleanup(); completion.TrySetCanceled(); };
            try { events.Activate += activated; timer.Tick += tick; timer.Start(); check(); }
            catch (Exception e) { cleanup(); completion.TrySetException(e); }
            return completion.Task;
        }
        private string GetProperty(string name)
        {
            Outlook.UserProperties properties = null; Outlook.UserProperty property = null;
            try { properties = Properties(); property = properties.Find(name, true); return property?.Value as string; }
            finally { Release(property); Release(properties); }
        }
        private void SetProperty(string name, string value)
        {
            Outlook.UserProperties properties = null; Outlook.UserProperty property = null;
            try { properties = Properties(); property = properties.Find(name, true) ?? properties.Add(name, Outlook.OlUserPropertyType.olText, false); property.Value = value; }
            finally { Release(property); Release(properties); }
        }
        private Outlook.UserProperties Properties() => item is Outlook.MailItem mail ? mail.UserProperties : ((Outlook.AppointmentItem)item).UserProperties;
        private void OnExplorerClose() { closed = true; }
        private void OnInspectorClose() { closed = true; }
        // Release only temporary COM references acquired by this class, never FinalRelease shared RCWs.
        private static void Release(object value) { if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
        public void Dispose()
        {
            ui.Verify(); closed = true; stopEditor?.Invoke();
            try { if (explorer != null) ((Outlook.ExplorerEvents_10_Event)explorer).Close -= OnExplorerClose; } catch (Exception e) when (e is COMException || e is InvalidComObjectException) { }
            try { if (inspector != null) ((Outlook.InspectorEvents_10_Event)inspector).Close -= OnInspectorClose; } catch (Exception e) when (e is COMException || e is InvalidComObjectException) { }
            explorer = null; inspector = null; item = null;
        }
    }
}
