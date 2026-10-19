using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace JitsiAdmin.OutlookPlugin.Core
{
    public interface IUiDispatcher
    {
        Task<T> InvokeAsync<T>(Func<T> action);
        Task InvokeAsync(Func<Task> action);
    }
    public interface IConferenceTarget
    {
        string Key { get; }
        bool IsEditable { get; }
        bool HasConference { get; }
        void MarkPending(string serverId);
        void MarkCreated(string serverId, Conference conference);
        Task InsertAsync(ConfigurationSnapshot config, Conference conference, CancellationToken cancellation);
    }
    public sealed class CreationOutcome
    {
        public string Message { get; }
        public Uri RecoveryUrl { get; }
        public bool Inserted { get; }
        public CreationOutcome(string message, Uri recoveryUrl = null, bool inserted = false) { Message = message; RecoveryUrl = recoveryUrl; Inserted = inserted; }
    }
    public sealed class CreationCoordinator
    {
        private readonly IMeetLinkApi api;
        private readonly IUiDispatcher ui;
        private readonly object gate = new object();
        // Kept until shutdown: successful, ambiguous and partially inserted operations must not be retried silently.
        private readonly HashSet<string> attempted = new HashSet<string>(StringComparer.Ordinal);
        public CreationCoordinator(IMeetLinkApi api, IUiDispatcher ui) { this.api = api; this.ui = ui; }
        public void CompleteNewAppointmentFlow(string key) { lock (gate) attempted.Remove(key); }
        public async Task<CreationOutcome> CreateAsync(ConfigurationSnapshot config, Server server, IConferenceTarget target, CancellationToken ct)
        {
            if (config == null) return new CreationOutcome("Konfiguration ist ungültig. Bitte neu laden.");
            if (server == null) return new CreationOutcome("Bitte einen gültigen Server auswählen.");
            try { ExampleApiAdapter.ValidateId(server.Id); }
            catch (MeetLinkException e) { return new CreationOutcome(e.Message); }
            string key = await ui.InvokeAsync(() => target.Key).ConfigureAwait(false);
            lock (gate)
            {
                if (!attempted.Add(key)) return new CreationOutcome("Für dieses Element läuft bereits eine Erstellung oder es wurde bereits eine angefordert. Bitte vorhandene Konferenz bzw. Serverstatus prüfen.");
            }
            bool dispatched = false;
            Conference conference = null;
            try
            {
                await ui.InvokeAsync(() => {
                    if (!target.IsEditable) throw new MeetLinkException("Das ursprüngliche Outlook-Fenster ist geschlossen oder nicht bearbeitbar.");
                    if (target.HasConference) throw new MeetLinkException("Dieses Element enthält bereits Konferenzmetadaten. Keine weitere Konferenz erstellt.");
                    ct.ThrowIfCancellationRequested();
                    target.MarkPending(server.Id); // Conservative persistent guard BEFORE any remote side effect.
                    return true;
                }).ConfigureAwait(false);
                dispatched = true;
                conference = await api.CreateAsync(config, server.Id, ct).ConfigureAwait(false);
                await ui.InvokeAsync(async () => {
                    if (!target.IsEditable) throw new MeetLinkException("Das ursprüngliche Fenster ist geschlossen oder nicht mehr bearbeitbar.");
                    target.MarkCreated(server.Id, conference); // Retain marker even if editor insertion fails.
                    await target.InsertAsync(config, conference, ct);
                }).ConfigureAwait(false);
                return new CreationOutcome(config.MockMode ? "MOCK: Beispiellink eingefügt – keine echte Konferenz." : "Meet-Link-Konferenz eingefügt.", inserted: true);
            }
            catch (Exception e)
            {
                if (!dispatched) { lock (gate) attempted.Remove(key); }
                if (conference != null) return new CreationOutcome("Konferenz erstellt, Einfügen fehlgeschlagen. Bitte den Teilnehmerlink manuell kopieren.", conference.JoinUrl);
                var error = e as MeetLinkException;
                return new CreationOutcome((error?.Message ?? "Die Aktion konnte nicht abgeschlossen werden.") +
                    (dispatched ? " Der Erstellungsschutz bleibt aktiv. Vor einem weiteren Versuch den Serverstatus prüfen; ein Abbruch macht keine Erstellung rückgängig." : ""));
            }
        }
    }
    public sealed class ServerCache
    {
        private readonly IMeetLinkApi api;
        private readonly object gate = new object();
        private long generation;
        private ConfigurationSnapshot config;
        private IReadOnlyList<Server> servers = new Server[0];
        private DateTime expires;
        private bool loading;
        private string error;
        public event Action Changed;
        public ServerCache(IMeetLinkApi api) { this.api = api; }
        public CacheView View { get { lock (gate) return new CacheView(config, servers, loading, error, generation); } }
        public void Reload(ConfigurationSnapshot value, string configurationError = null)
        {
            lock (gate) { generation++; config = value; servers = new Server[0]; expires = DateTime.MinValue; loading = false; error = configurationError; }
            Changed?.Invoke();
        }
        public async Task RefreshAsync(bool force, CancellationToken ct)
        {
            ConfigurationSnapshot snapshot; long version;
            lock (gate)
            {
                if (config == null || loading || (!force && expires > DateTime.UtcNow)) return;
                snapshot = config; version = ++generation; loading = true; error = null;
            }
            Changed?.Invoke();
            try
            {
                var loaded = await api.GetServersAsync(snapshot, ct).ConfigureAwait(false);
                lock (gate)
                {
                    if (version != generation) return;
                    servers = loaded; expires = DateTime.UtcNow.AddSeconds(snapshot.ServerCacheSeconds); loading = false;
                }
            }
            catch (Exception e)
            {
                lock (gate)
                {
                    if (version != generation) return;
                    servers = new Server[0]; loading = false;
                    error = (e as MeetLinkException)?.Message ?? "Server konnten nicht geladen werden.";
                    // Do not start endless background requests on every Ribbon redraw after an error.
                    expires = DateTime.UtcNow.AddSeconds(10);
                }
            }
            Changed?.Invoke();
        }
    }
    public sealed class CacheView
    {
        public ConfigurationSnapshot Config { get; }
        public IReadOnlyList<Server> Servers { get; }
        public bool Loading { get; }
        public string Error { get; }
        public long Generation { get; }
        internal CacheView(ConfigurationSnapshot c, IReadOnlyList<Server> s, bool l, string e, long g)
        { Config = c; Servers = s; Loading = l; Error = e; Generation = g; }
    }
}
