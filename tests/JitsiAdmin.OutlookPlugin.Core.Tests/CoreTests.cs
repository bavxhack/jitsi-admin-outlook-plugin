using System.Net;
using System.Text;
using JitsiAdmin.OutlookPlugin.Core;
using Newtonsoft.Json;
using Xunit;

namespace JitsiAdmin.OutlookPlugin.Core.Tests;

public class CoreTests
{
    internal static Configuration Settings(bool mock = false) => new Configuration {
        BaseUrl = "https://meetlink.example.invalid/root/", ServersEndpoint = "api/servers", CreateConferenceEndpoint = "api/conferences/instant",
        ServersHttpMethod = "GET", CreateConferenceHttpMethod = "POST", TimeoutSeconds = 1, ServerCacheSeconds = 60,
        Auth = new AuthConfiguration { Mode = "BearerTokenEnvironment", TokenEnvironmentVariable = "TEST_TOKEN", AllowedOrigins = new[] { "https://meetlink.example.invalid" } },
        Body = new BodyConfiguration { Heading = "Meet-Link-Konferenz", LinkLabel = "Teilnehmen:" }, MockMode = mock, LogLevel = "None"
    };
    [Theory]
    [InlineData("https://meetlink.example.invalid/root/", "api/servers", "https://meetlink.example.invalid/root/api/servers")]
    [InlineData("https://meetlink.example.invalid/root", "api/servers", "https://meetlink.example.invalid/api/servers")]
    [InlineData("https://meetlink.example.invalid/root/", "/api/servers", "https://meetlink.example.invalid/api/servers")]
    [InlineData("https://meetlink.example.invalid/root/", "api/servers/", "https://meetlink.example.invalid/root/api/servers/")]
    [InlineData("https://meetlink.example.invalid/root/", "https://other.example.invalid/servers", "https://other.example.invalid/servers")]
    public void UriResolutionUsesDotNetRules(string b, string endpoint, string expected) =>
        Assert.Equal(expected, Configuration.Resolve(new Uri(b), endpoint).AbsoluteUri);

    [Theory]
    [InlineData("http://meetlink.example.invalid/")]
    [InlineData("https://user:password@meetlink.example.invalid/")]
    [InlineData("file:///tmp/config")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://meetlink.example.invalid/#fragment")]
    public void UnsafeUrlsAreRejected(string value)
    { var c = Settings(); c.BaseUrl = value; Assert.Throws<MeetLinkException>(() => c.Validate()); }
    [Fact] public void UnsupportedMethodsAreRejected()
    {
        var c = Settings(); c.ServersHttpMethod = "POST"; Assert.Throws<MeetLinkException>(() => c.Validate());
        c = Settings(); c.CreateConferenceHttpMethod = "GET"; Assert.Throws<MeetLinkException>(() => c.Validate());
    }
    [Fact] public void ValuesAndRequiredFieldsAreValidated()
    {
        foreach (int invalid in new[] { -1, 0, 301 }) { var c = Settings(); c.TimeoutSeconds = invalid; Assert.Throws<MeetLinkException>(() => c.Validate()); }
        var other = Settings(); other.Body.Heading = ""; Assert.Throws<MeetLinkException>(() => other.Validate());
        other = Settings(); other.ServersEndpoint = null; Assert.Throws<MeetLinkException>(() => other.Validate());
        other = Settings(); other.ServerCacheSeconds = -1; Assert.Throws<MeetLinkException>(() => other.Validate());
        other = Settings(); other.Auth = null; Assert.Throws<MeetLinkException>(() => other.Validate());
    }
    [Fact] public void SnapshotDoesNotChangeWithMutableSource()
    { var c = Settings(); var snapshot = c.Validate(); c.Body.Heading = "changed"; c.Auth.AllowedOrigins[0] = "https://evil.invalid"; Assert.Equal("Meet-Link-Konferenz", snapshot.Heading); Assert.Equal("meetlink.example.invalid", snapshot.AllowedOrigins[0].Host); }
    [Fact] public void DeliveredConfigurationsLoad()
    {
        Assert.False(Configuration.Load(Path.Combine(AppContext.BaseDirectory, "config/config.example.json")).MockMode);
        Assert.True(Configuration.Load(Path.Combine(AppContext.BaseDirectory, "config/config.mock.json")).MockMode);
    }
    [Fact] public void MissingAndInvalidConfigurationHaveGermanErrors()
    {
        var file = Path.GetTempFileName();
        try { File.WriteAllText(file, "{invalid"); Assert.Contains("Konfiguration", Assert.Throws<MeetLinkException>(() => Configuration.Load(file)).Message); }
        finally { File.Delete(file); }
        Assert.Throws<MeetLinkException>(() => Configuration.Load(file));
    }
    [Theory] [InlineData("mockMode")] [InlineData("timeoutSeconds")] [InlineData("auth")] [InlineData("body")]
    public void MissingJsonFieldsAreRejected(string field)
    {
        var obj = Newtonsoft.Json.Linq.JObject.Parse(JsonConvert.SerializeObject(Settings(), new JsonSerializerSettings {
            ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver() }));
        obj.Remove(field); var path = Path.GetTempFileName();
        try { File.WriteAllText(path, obj.ToString()); Assert.Throws<MeetLinkException>(() => Configuration.Load(path)); }
        finally { File.Delete(path); }
    }
    [Fact] public async Task MissingTokenProducesNoHttpRequest()
    {
        var handler = new Handler((r, ct) => throw new Exception("No send expected"));
        using var api = new MeetLinkApi(auth: new EnvironmentBearerAuthentication(_ => null), handler: handler);
        await Assert.ThrowsAsync<MeetLinkException>(() => api.CreateAsync(Settings().Validate(), "x", default));
        Assert.Equal(0, handler.Count);
    }
    [Fact] public async Task ServerGetHasNoBodyAndDoesNotCreate()
    {
        var handler = new Handler((r, ct) => {
            Assert.Equal(HttpMethod.Get, r.Method); Assert.Null(r.Content);
            return Task.FromResult(Response(HttpStatusCode.OK, "{\"servers\":[{\"id\":\"id-7\",\"name\":\"Name\"}]}"));
        });
        using var api = new MeetLinkApi(auth: new EnvironmentBearerAuthentication(_ => "test"), handler: handler);
        Assert.Equal("id-7", Assert.Single(await api.GetServersAsync(Settings().Validate(), default)).Id);
        Assert.Equal(1, handler.Count);
    }
    [Theory]
    [InlineData("https://meetlink.example.invalid.evil.invalid/api")]
    [InlineData("https://meetlink.example.invalid:444/api")]
    [InlineData("http://meetlink.example.invalid/api")]
    public void CredentialsNeverReachUnapprovedOrigins(string url)
    {
        bool read = false; var auth = new EnvironmentBearerAuthentication(_ => { read = true; return "secret"; });
        using var message = new HttpRequestMessage(HttpMethod.Get, url);
        Assert.Throws<MeetLinkException>(() => auth.Authorize(message, Settings().Validate()));
        Assert.False(read); Assert.Null(message.Headers.Authorization);
    }
    [Fact] public void EffectiveDefaultPortIsAnOriginMatch()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "https://meetlink.example.invalid:443/api");
        new EnvironmentBearerAuthentication(_ => "test").Authorize(request, Settings().Validate());
        Assert.Equal("Bearer", request.Headers.Authorization.Scheme);
    }
    [Theory] [InlineData(null)] [InlineData("")] [InlineData("bad\r\ntoken")]
    public void MissingOrUnsafeTokenFailsBeforeSend(string value)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Settings().Validate().CreateUri);
        Assert.Throws<MeetLinkException>(() => new EnvironmentBearerAuthentication(_ => value).Authorize(request, Settings().Validate()));
    }
    [Fact] public void AbsoluteEndpointsRequireTheirOwnOriginApproval()
    { var c = Settings(); c.CreateConferenceEndpoint = "https://other.invalid/create"; Assert.Throws<MeetLinkException>(() => c.Validate()); c.Auth.AllowedOrigins = new[] { "https://meetlink.example.invalid", "https://other.invalid" }; Assert.Equal("other.invalid", c.Validate().CreateUri.Host); }
    [Theory]
    [InlineData("{}")] [InlineData("{invalid")]
    [InlineData("{\"servers\":[{\"name\":\"Berlin\"}]}")]
    [InlineData("{\"servers\":[{\"id\":\"a\",\"name\":\"Berlin\"},{\"id\":\"a\",\"name\":\"Hamburg\"}]}")]
    [InlineData("{\"servers\":[{\"id\":12,\"name\":\"Berlin\"}]}")]
    [InlineData("{\"servers\":[{\"id\":\"  \",\"name\":\"Berlin\"}]}")]
    public void InvalidServerResponsesFail(string json) => Assert.Throws<MeetLinkException>(() => new ExampleApiAdapter().ReadServers(json));
    [Fact] public void EmptyListAndSeparateNameAndIdAreSupported()
    {
        var adapter = new ExampleApiAdapter(); Assert.Empty(adapter.ReadServers("{\"servers\":[]}"));
        var server = Assert.Single(adapter.ReadServers("{\"servers\":[{\"id\":\"technical-id\",\"name\":\"Berlin & <Gäste>\"}]}"));
        Assert.Equal("technical-id", server.Id); Assert.Equal("Berlin & <Gäste>", server.Name);
    }
    [Theory]
    [InlineData("{}")] [InlineData("{invalid")]
    [InlineData("{\"joinUrl\":\"javascript:alert(1)\"}")]
    [InlineData("{\"joinUrl\":\"file:///tmp/test\"}")]
    [InlineData("{\"joinUrl\":\"http://example.invalid/join\"}")]
    [InlineData("{\"joinUrl\":10}")]
    public void InvalidParticipantLinksFail(string json) => Assert.Throws<MeetLinkException>(() => new ExampleApiAdapter().ReadConference(json));
    [Fact] public void ModeratorUrlIsNeverUsedAsParticipantUrl()
    {
        Assert.Throws<MeetLinkException>(() => new ExampleApiAdapter().ReadConference("{\"hostUrl\":\"https://example.invalid/host\"}"));
        Assert.Null(new ExampleApiAdapter().ReadConference("{\"joinUrl\":\"https://example.invalid/join\"}").Id);
    }
    [Fact] public async Task SelectedTechnicalIdIsTheOnlyCreationPayload()
    {
        var handler = new Handler(async (request, ct) => {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("application/json", request.Content.Headers.ContentType.MediaType);
            Assert.Equal("{\"serverId\":\"chosen-42\"}", await request.Content.ReadAsStringAsync(ct));
            Assert.Equal("Bearer", request.Headers.Authorization.Scheme);
            return Response(HttpStatusCode.Created, "{\"joinUrl\":\"https://example.invalid/join/42\"}");
        });
        using var api = new MeetLinkApi(auth: new EnvironmentBearerAuthentication(_ => "test-token"), handler: handler);
        await api.CreateAsync(Settings().Validate(), "chosen-42", default);
        Assert.Equal(1, handler.Count);
    }
    [Fact] public async Task RedirectsAreNotFollowedAndProductionHandlerDisablesRedirects()
    {
        using var policy = MeetLinkApi.CreateHandler(); Assert.False(policy.AllowAutoRedirect); Assert.False(policy.UseCookies); Assert.False(policy.UseDefaultCredentials);
        var handler = new Handler((r, ct) => {
            var response = Response(HttpStatusCode.Redirect, ""); response.Headers.Location = new Uri("https://evil.invalid/steal"); return Task.FromResult(response);
        });
        using var api = new MeetLinkApi(auth: new EnvironmentBearerAuthentication(_ => "test"), handler: handler);
        Assert.Contains("Weiterleitung", (await Assert.ThrowsAsync<MeetLinkException>(() => api.CreateAsync(Settings().Validate(), "x", default))).Message);
        Assert.Equal(1, handler.Count);
    }
    [Theory] [InlineData(401, "Anmeldung")] [InlineData(403, "Berechtigung")] [InlineData(429, "Zu viele")] [InlineData(500, "Server")]
    public async Task HttpErrorsHaveGermanMessagesAndNoRetry(int status, string expected)
    {
        var handler = new Handler((r, ct) => Task.FromResult(Response((HttpStatusCode)status, "sensitive raw response")));
        using var api = new MeetLinkApi(auth: new EnvironmentBearerAuthentication(_ => "test"), handler: handler);
        var error = await Assert.ThrowsAsync<MeetLinkException>(() => api.CreateAsync(Settings().Validate(), "x", default));
        Assert.Contains(expected, error.Message); Assert.DoesNotContain("sensitive", error.Message); Assert.Equal(1, handler.Count);
    }
    [Fact] public async Task TimeoutIsUncertainAndIsNotRetried()
    {
        var handler = new Handler(async (r, ct) => { await Task.Delay(Timeout.Infinite, ct); return Response(HttpStatusCode.OK, ""); });
        using var api = new MeetLinkApi(auth: new EnvironmentBearerAuthentication(_ => "test"), handler: handler);
        Assert.True((await Assert.ThrowsAsync<MeetLinkException>(() => api.CreateAsync(Settings().Validate(), "x", default))).Uncertain);
        Assert.Equal(1, handler.Count);
    }
    [Fact] public async Task NetworkFailureIsUncertain()
    {
        var handler = new Handler((r, ct) => throw new HttpRequestException("sensitive"));
        using var api = new MeetLinkApi(auth: new EnvironmentBearerAuthentication(_ => "test"), handler: handler);
        var error = await Assert.ThrowsAsync<MeetLinkException>(() => api.CreateAsync(Settings().Validate(), "x", default));
        Assert.True(error.Uncertain); Assert.DoesNotContain("sensitive", error.Message); Assert.Equal(1, handler.Count);
    }
    [Fact] public async Task MockUsesNeitherNetworkNorCredentials()
    {
        var handler = new Handler((r, ct) => throw new Exception("No network permitted"));
        using var api = new MeetLinkApi(auth: new EnvironmentBearerAuthentication(_ => throw new Exception("No token permitted")), handler: handler);
        var c = Settings(true).Validate(); var servers = await api.GetServersAsync(c, default);
        Assert.True(servers.Count >= 2); var conference = await api.CreateAsync(c, servers[1].Id, default);
        Assert.Equal("meetlink.example.invalid", conference.JoinUrl.Host); Assert.Equal(0, handler.Count);
        var ui = new FakeUi(); var target = new Target(ui, "mock");
        Assert.True((await new CreationCoordinator(api, ui).CreateAsync(c, servers[1], target, default)).Inserted);
        Assert.True(target.Inserted); Assert.Equal(0, handler.Count);
    }
    internal static HttpResponseMessage Response(HttpStatusCode status, string json) => new(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    internal sealed class Handler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send;
        public int Count;
        public Handler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) { this.send = send; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) { Interlocked.Increment(ref Count); return send(request, ct); }
    }
}
