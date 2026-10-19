using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace JitsiAdmin.OutlookPlugin.Core
{
    public sealed class MeetLinkException : Exception
    {
        public bool Uncertain { get; }
        public MeetLinkException(string message, bool uncertain = false) : base(message) { Uncertain = uncertain; }
    }
    public sealed class Configuration
    {
        public string BaseUrl { get; set; }
        public string ServersEndpoint { get; set; }
        public string CreateConferenceEndpoint { get; set; }
        public string ServersHttpMethod { get; set; }
        public string CreateConferenceHttpMethod { get; set; }
        public int TimeoutSeconds { get; set; }
        public int ServerCacheSeconds { get; set; }
        public AuthConfiguration Auth { get; set; }
        public BodyConfiguration Body { get; set; }
        public bool MockMode { get; set; }
        public string LogLevel { get; set; }
        public static string DefaultPath => Environment.GetEnvironmentVariable("MEETLINK_OUTLOOK_CONFIG") ??
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MeetLinkOutlookAddin", "config.json");
        public static ConfigurationSnapshot Load(string path)
        {
            try
            {
                if (new FileInfo(path).Length > 65536) throw new MeetLinkException("Die Konfigurationsdatei ist zu groß.");
                Configuration value;
                using (var reader = new JsonTextReader(new StringReader(File.ReadAllText(path))) { MaxDepth = 16, DateParseHandling = DateParseHandling.None })
                {
                    var obj = JObject.Load(reader, new JsonLoadSettings { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    Require(obj, "baseUrl", JTokenType.String); Require(obj, "serversEndpoint", JTokenType.String);
                    Require(obj, "createConferenceEndpoint", JTokenType.String); Require(obj, "serversHttpMethod", JTokenType.String);
                    Require(obj, "createConferenceHttpMethod", JTokenType.String); Require(obj, "timeoutSeconds", JTokenType.Integer);
                    Require(obj, "serverCacheSeconds", JTokenType.Integer); Require(obj, "mockMode", JTokenType.Boolean);
                    Require(obj, "logLevel", JTokenType.String); Require(obj, "auth", JTokenType.Object); Require(obj, "body", JTokenType.Object);
                    Require((JObject)obj["auth"], "mode", JTokenType.String); Require((JObject)obj["auth"], "tokenEnvironmentVariable", JTokenType.String);
                    Require((JObject)obj["auth"], "allowedOrigins", JTokenType.Array);
                    if (((JArray)obj["auth"]["allowedOrigins"]).Any(x => x.Type != JTokenType.String)) throw new MeetLinkException("allowedOrigins enthält einen ungültigen Wert.");
                    Require((JObject)obj["body"], "heading", JTokenType.String); Require((JObject)obj["body"], "linkLabel", JTokenType.String);
                    if (reader.Read()) throw new MeetLinkException("Nach dem Konfigurationsobjekt stehen unerwartete Daten.");
                    value = obj.ToObject<Configuration>();
                }
                if (value == null) throw new MeetLinkException("Die Konfiguration fehlt.");
                return value.Validate();
            }
            catch (MeetLinkException) { throw; }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is JsonException || e is ArgumentException)
            { throw new MeetLinkException("Konfiguration fehlt oder ist ungültig. Bitte config.json prüfen und neu laden."); }
        }
        private static void Require(JObject obj, string key, JTokenType type)
        {
            if (obj[key] == null || obj[key].Type != type) throw new MeetLinkException("Konfigurationsfeld fehlt oder hat einen ungültigen Typ: " + key);
        }
        public ConfigurationSnapshot Validate()
        {
            Uri baseUri;
            if (!Uri.TryCreate(BaseUrl, UriKind.Absolute, out baseUri)) throw new MeetLinkException("Base-URL ist ungültig.");
            CheckHttps(baseUri);
            var servers = Resolve(baseUri, ServersEndpoint);
            var create = Resolve(baseUri, CreateConferenceEndpoint);
            if (ServersHttpMethod != "GET" || CreateConferenceHttpMethod != "POST")
                throw new MeetLinkException("Der Beispieladapter unterstützt ausschließlich GET für Server und POST mit JSON für die Erstellung.");
            if (TimeoutSeconds < 1 || TimeoutSeconds > 300 || ServerCacheSeconds < 0 || ServerCacheSeconds > 86400)
                throw new MeetLinkException("Timeout muss 1–300 Sekunden, Cache 0–86400 Sekunden betragen.");
            if (Body == null || string.IsNullOrWhiteSpace(Body.Heading) || string.IsNullOrWhiteSpace(Body.LinkLabel) ||
                Body.Heading.Length > 200 || Body.LinkLabel.Length > 200 ||
                Body.Heading.Any(char.IsControl) || Body.LinkLabel.Any(char.IsControl))
                throw new MeetLinkException("Body-Überschrift und Linkbeschriftung müssen einzeilig und höchstens 200 Zeichen lang sein.");
            if (LogLevel != "Information" && LogLevel != "Error" && LogLevel != "None") throw new MeetLinkException("Ungültiger LogLevel.");
            if (Auth == null || Auth.Mode != "BearerTokenEnvironment" || string.IsNullOrWhiteSpace(Auth.TokenEnvironmentVariable))
                throw new MeetLinkException("Authentifizierung benötigt BearerTokenEnvironment und einen Variablennamen.");
            var origins = (Auth.AllowedOrigins ?? new string[0]).Select(ParseOrigin).ToArray();
            if (!MockMode && (!origins.Any(x => SameOrigin(x, servers)) || !origins.Any(x => SameOrigin(x, create))))
                throw new MeetLinkException("Die API-Ziele sind nicht durch allowedOrigins freigegeben.");
            return new ConfigurationSnapshot(servers, create, TimeoutSeconds, ServerCacheSeconds, Auth.TokenEnvironmentVariable,
                origins, Body.Heading, Body.LinkLabel, MockMode, LogLevel);
        }
        public static Uri Resolve(Uri baseUri, string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint)) throw new MeetLinkException("Ein API-Endpunkt fehlt.");
            Uri result;
            if (!Uri.TryCreate(baseUri, endpoint, out result)) throw new MeetLinkException("Ein API-Endpunkt ist ungültig.");
            CheckHttps(result);
            return result;
        }
        private static Uri ParseOrigin(string value)
        {
            Uri result;
            if (!Uri.TryCreate(value, UriKind.Absolute, out result)) throw new MeetLinkException("Ungültiger allowedOrigins-Eintrag.");
            CheckHttps(result);
            if (result.AbsolutePath != "/" || result.Query.Length != 0) throw new MeetLinkException("allowedOrigins darf nur Scheme, Host und Port enthalten.");
            return result;
        }
        public static bool SameOrigin(Uri a, Uri b) => a.Scheme == b.Scheme &&
            string.Equals(a.IdnHost, b.IdnHost, StringComparison.OrdinalIgnoreCase) && a.Port == b.Port;
        public static void CheckHttps(Uri uri)
        {
            if (!uri.IsAbsoluteUri || uri.Scheme != "https" || string.IsNullOrEmpty(uri.Host) || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0)
                throw new MeetLinkException("Nur absolute HTTPS-URLs ohne Zugangsdaten oder Fragment sind erlaubt.");
        }
    }
    public sealed class AuthConfiguration
    {
        public string Mode { get; set; }
        public string TokenEnvironmentVariable { get; set; }
        public string[] AllowedOrigins { get; set; }
    }
    public sealed class BodyConfiguration
    {
        public string Heading { get; set; }
        public string LinkLabel { get; set; }
    }
    public sealed class ConfigurationSnapshot
    {
        public Uri ServersUri { get; }
        public Uri CreateUri { get; }
        public int TimeoutSeconds { get; }
        public int ServerCacheSeconds { get; }
        public string TokenVariable { get; }
        public IReadOnlyList<Uri> AllowedOrigins { get; }
        public string Heading { get; }
        public string LinkLabel { get; }
        public bool MockMode { get; }
        public string LogLevel { get; }
        internal ConfigurationSnapshot(Uri servers, Uri create, int timeout, int cache, string tokenVariable, Uri[] origins,
            string heading, string linkLabel, bool mockMode, string logLevel)
        {
            ServersUri = servers; CreateUri = create; TimeoutSeconds = timeout; ServerCacheSeconds = cache;
            TokenVariable = tokenVariable; AllowedOrigins = new ReadOnlyCollection<Uri>(origins);
            Heading = heading; LinkLabel = linkLabel; MockMode = mockMode; LogLevel = logLevel;
        }
    }
}
