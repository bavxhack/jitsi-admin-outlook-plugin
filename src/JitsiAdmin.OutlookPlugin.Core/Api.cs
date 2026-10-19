using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JitsiAdmin.OutlookPlugin.Core
{
    public sealed class Server
    {
        public string Id { get; }
        public string Name { get; }
        public Server(string id, string name) { Id = id; Name = name; }
    }
    public sealed class Conference
    {
        public string Id { get; }
        public Uri JoinUrl { get; }
        public Conference(string id, Uri joinUrl) { Id = id; JoinUrl = joinUrl; }
    }
    public interface IAuthentication
    {
        void Authorize(HttpRequestMessage request, ConfigurationSnapshot config);
    }
    public sealed class EnvironmentBearerAuthentication : IAuthentication
    {
        private readonly Func<string, string> read;
        public EnvironmentBearerAuthentication(Func<string, string> read = null) { this.read = read ?? Environment.GetEnvironmentVariable; }
        public void Authorize(HttpRequestMessage request, ConfigurationSnapshot config)
        {
            Configuration.CheckHttps(request.RequestUri);
            if (!config.AllowedOrigins.Any(x => Configuration.SameOrigin(x, request.RequestUri)))
                throw new MeetLinkException("Das Authentifizierungsziel ist nicht freigegeben.");
            var token = read(config.TokenVariable);
            if (string.IsNullOrWhiteSpace(token) || token.Any(char.IsControl) || token.Any(char.IsWhiteSpace))
                throw new MeetLinkException("Zugangsdaten fehlen oder sind ungültig. Bitte die benutzerspezifische Token-Umgebungsvariable prüfen.");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
    }
    public interface IApiAdapter
    {
        HttpRequestMessage ServersRequest(ConfigurationSnapshot config);
        HttpRequestMessage CreateRequest(ConfigurationSnapshot config, string serverId);
        IReadOnlyList<Server> ReadServers(string json);
        Conference ReadConference(string json);
    }
    // EXAMPLE CONTRACT ONLY. Replace this adapter after confirming the real Meet Link API.
    public sealed class ExampleApiAdapter : IApiAdapter
    {
        public HttpRequestMessage ServersRequest(ConfigurationSnapshot c) => new HttpRequestMessage(HttpMethod.Get, c.ServersUri);
        public HttpRequestMessage CreateRequest(ConfigurationSnapshot c, string id)
        {
            ValidateId(id);
            return new HttpRequestMessage(HttpMethod.Post, c.CreateUri) {
                Content = new StringContent(JsonConvert.SerializeObject(new { serverId = id }), Encoding.UTF8, "application/json") };
        }
        public static void ValidateId(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.Length > 256 || id.Any(char.IsControl))
                throw new MeetLinkException("Eine Server-ID fehlt oder ist ungültig.");
        }
        public IReadOnlyList<Server> ReadServers(string json)
        {
            try
            {
                var array = ReadObject(json)["servers"] as JArray;
                if (array == null || array.Count > 1000) throw new MeetLinkException("Die Serverliste fehlt oder ist zu groß.");
                var result = new List<Server>();
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var item in array)
                {
                    var id = ReadString(item, "id"); var name = ReadString(item, "name");
                    ValidateId(id);
                    if (!ids.Add(id)) throw new MeetLinkException("Die Serverliste enthält doppelte Server-IDs.");
                    if (string.IsNullOrWhiteSpace(name) || name.Length > 200 || name.Any(char.IsControl))
                        throw new MeetLinkException("Ein Servername fehlt oder ist ungültig.");
                    result.Add(new Server(id, name));
                }
                return result.AsReadOnly();
            }
            catch (JsonException) { throw new MeetLinkException("Die API hat ungültiges JSON geliefert."); }
        }
        public Conference ReadConference(string json)
        {
            try
            {
                var obj = ReadObject(json); Uri url;
                if (!Uri.TryCreate(ReadString(obj, "joinUrl"), UriKind.Absolute, out url))
                    throw new MeetLinkException("Der Teilnehmerlink fehlt oder ist ungültig.", true);
                Configuration.CheckHttps(url);
                var id = ReadString(obj, "conferenceId");
                if (id != null && (id.Length > 256 || id.Any(char.IsControl))) throw new MeetLinkException("Ungültige Konferenz-ID.", true);
                return new Conference(id, url);
            }
            catch (JsonException) { throw new MeetLinkException("Die API hat ungültiges JSON geliefert.", true); }
        }
        private static JObject ReadObject(string json)
        {
            using (var reader = new JsonTextReader(new System.IO.StringReader(json)) { MaxDepth = 16, DateParseHandling = DateParseHandling.None })
            {
                var obj = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                if (reader.Read()) throw new JsonReaderException("Unexpected trailing JSON content.");
                return obj;
            }
        }
        private static string ReadString(JToken obj, string field)
        {
            var value = obj is JObject ? obj[field] : null;
            if (value == null || value.Type == JTokenType.Null) return null;
            if (value.Type != JTokenType.String) throw new MeetLinkException("Die API-Antwort enthält einen ungültigen Feldtyp.");
            return (string)value;
        }
    }
    public interface IMeetLinkApi
    {
        Task<IReadOnlyList<Server>> GetServersAsync(ConfigurationSnapshot config, CancellationToken cancellation);
        Task<Conference> CreateAsync(ConfigurationSnapshot config, string serverId, CancellationToken cancellation);
    }
    public sealed class MeetLinkApi : IMeetLinkApi, IDisposable
    {
        private readonly HttpClient client;
        private readonly IApiAdapter adapter;
        private readonly IAuthentication auth;
        public static HttpClientHandler CreateHandler() => new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false, UseDefaultCredentials = false };
        public MeetLinkApi(IApiAdapter adapter = null, IAuthentication auth = null, HttpMessageHandler handler = null)
        {
            this.adapter = adapter ?? new ExampleApiAdapter(); this.auth = auth ?? new EnvironmentBearerAuthentication();
            client = new HttpClient(handler ?? CreateHandler()) { Timeout = Timeout.InfiniteTimeSpan, MaxResponseContentBufferSize = 1048576 };
        }
        public async Task<IReadOnlyList<Server>> GetServersAsync(ConfigurationSnapshot c, CancellationToken ct)
        {
            if (c.MockMode) return new[] { new Server("mock-berlin", "Berlin (MOCK)"), new Server("mock-hamburg", "Hamburg & Gäste (MOCK)") };
            using (var request = adapter.ServersRequest(c)) return adapter.ReadServers(await SendAsync(request, c, false, ct).ConfigureAwait(false));
        }
        public async Task<Conference> CreateAsync(ConfigurationSnapshot c, string id, CancellationToken ct)
        {
            ExampleApiAdapter.ValidateId(id); ct.ThrowIfCancellationRequested();
            if (c.MockMode) return new Conference("mock-" + Guid.NewGuid().ToString("N"), new Uri("https://meetlink.example.invalid/mock/" + Guid.NewGuid().ToString("N")));
            using (var request = adapter.CreateRequest(c, id)) return adapter.ReadConference(await SendAsync(request, c, true, ct).ConfigureAwait(false));
        }
        private async Task<string> SendAsync(HttpRequestMessage request, ConfigurationSnapshot c, bool creation, CancellationToken ct)
        {
            auth.Authorize(request, c); // No credentials sent before the origin check; no redirects or retries.
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(c.TimeoutSeconds));
                try
                {
                    using (var response = await client.SendAsync(request, HttpCompletionOption.ResponseContentRead, timeout.Token).ConfigureAwait(false))
                    {
                        int status = (int)response.StatusCode;
                        if (status >= 300 && status < 400) throw new MeetLinkException("API-Weiterleitung abgelehnt. Bitte die direkte Ziel-URL konfigurieren.", creation);
                        if (!response.IsSuccessStatusCode)
                        {
                            string message = status == 401 ? "Anmeldung abgelehnt (401). Token prüfen." :
                                status == 403 ? "Keine Berechtigung für diesen Server (403)." :
                                status == 429 ? "Zu viele Anfragen (429). Bitte später erneut versuchen." :
                                status >= 500 ? "Der Meet-Link-Server meldet einen Fehler." : "Die API hat die Anfrage abgelehnt.";
                            throw new MeetLinkException(message, creation && status >= 500);
                        }
                        if (response.Content == null) throw new MeetLinkException("Die API-Antwort ist leer.", creation);
                        if (response.Content.Headers.ContentLength > 1048576) throw new MeetLinkException("Die API-Antwort ist zu groß.", creation);
                        using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                        using (var memory = new System.IO.MemoryStream())
                        {
                            var buffer = new byte[8192]; int count;
                            while ((count = await stream.ReadAsync(buffer, 0, buffer.Length, timeout.Token).ConfigureAwait(false)) != 0)
                            {
                                if (memory.Length + count > 1048576) throw new MeetLinkException("Die API-Antwort ist zu groß.", creation);
                                memory.Write(buffer, 0, count);
                            }
                            return Encoding.UTF8.GetString(memory.ToArray());
                        }
                    }
                }
                catch (OperationCanceledException) { throw new MeetLinkException(creation ? "Zeitüberschreitung oder Abbruch: Ausgang der Erstellung unklar. Nicht automatisch erneut erstellen." : "Serverabfrage abgebrochen oder Zeitüberschreitung.", creation); }
                catch (Exception e) when (e is HttpRequestException || e is System.IO.IOException)
                { throw new MeetLinkException(creation ? "Verbindungsfehler: Ausgang der Erstellung unklar. Bitte serverseitig prüfen." : "Verbindung zu Meet Link fehlgeschlagen.", creation); }
            }
        }
        public void Dispose() { client.Dispose(); }
    }
}
