using JitsiAdmin.OutlookPlugin.Core;
using Xunit;
namespace JitsiAdmin.OutlookPlugin.Core.Tests;

internal sealed class FakeUi : IUiDispatcher
{
    private readonly AsyncLocal<bool> inside = new();
    public void Verify() { Assert.True(inside.Value, "Target accessed outside explicit UI dispatcher"); }
    public Task<T> InvokeAsync<T>(Func<T> action)
    {
        bool previous = inside.Value; inside.Value = true;
        try { return Task.FromResult(action()); } catch (Exception e) { return Task.FromException<T>(e); }
        finally { inside.Value = previous; }
    }
    public async Task InvokeAsync(Func<Task> action)
    { bool previous = inside.Value; inside.Value = true; try { await action(); } finally { inside.Value = previous; } }
}
internal sealed class Target : IConferenceTarget
{
    private readonly FakeUi ui; private readonly string key;
    public bool Editable = true, Marked, Inserted, FailInsert, FailMetadata;
    public Target(FakeUi ui, string key) { this.ui = ui; this.key = key; }
    public string Key { get { ui.Verify(); return key; } }
    public bool IsEditable { get { ui.Verify(); return Editable; } }
    public bool HasConference { get { ui.Verify(); return Marked; } }
    public void MarkPending(string serverId) { ui.Verify(); Marked = true; }
    public void MarkCreated(string serverId, Conference conference) { ui.Verify(); if (FailMetadata) throw new Exception("metadata"); Marked = true; }
    public Task InsertAsync(ConfigurationSnapshot config, Conference conference, CancellationToken cancellation)
    { ui.Verify(); if (FailInsert) throw new Exception("insert"); Inserted = true; return Task.CompletedTask; }
}
internal sealed class FakeApi : IMeetLinkApi
{
    public int Creates, Loads;
    public Func<ConfigurationSnapshot, string, CancellationToken, Task<Conference>> Create = (c, id, ct) => Task.FromResult(WorkflowTests.Conference());
    public Func<ConfigurationSnapshot, CancellationToken, Task<IReadOnlyList<Server>>> Load = (c, ct) => Task.FromResult<IReadOnlyList<Server>>(new[] { new Server("a", "A") });
    public Task<Conference> CreateAsync(ConfigurationSnapshot c, string id, CancellationToken ct) { Interlocked.Increment(ref Creates); return Create(c, id, ct); }
    public Task<IReadOnlyList<Server>> GetServersAsync(ConfigurationSnapshot c, CancellationToken ct) { Interlocked.Increment(ref Loads); return Load(c, ct); }
}
public class WorkflowTests
{
    internal static Conference Conference() => new("conference-1", new Uri("https://example.invalid/join/1"));
    private static Server Server => new("technical-id", "Display name");
    private static TaskCompletionSource<T> Pending<T>() => new(TaskCreationOptions.RunContinuationsAsynchronously);
    [Fact] public async Task ConcurrentWindowsForSameElementSendExactlyOneRequest()
    {
        var ui = new FakeUi(); var api = new FakeApi(); var response = Pending<Conference>(); api.Create = (c, id, ct) => response.Task;
        var coordinator = new CreationCoordinator(api, ui); var a = new Target(ui, "same-unsaved-guid"); var b = new Target(ui, "same-unsaved-guid");
        var first = coordinator.CreateAsync(CoreTests.Settings().Validate(), Server, a, default);
        var second = await coordinator.CreateAsync(CoreTests.Settings().Validate(), Server, b, default);
        Assert.Contains("bereits", second.Message); Assert.Equal(1, api.Creates);
        response.SetResult(Conference()); Assert.True((await first).Inserted); Assert.True(a.Inserted); Assert.False(b.Inserted);
        await coordinator.CreateAsync(CoreTests.Settings().Validate(), Server, a, default); Assert.Equal(1, api.Creates);
    }
    [Fact] public async Task ParallelNewAppointmentFlowIsGuarded()
    {
        var ui = new FakeUi(); var api = new FakeApi(); var response = Pending<Conference>(); api.Create = (c, id, ct) => response.Task;
        var coordinator = new CreationCoordinator(api, ui);
        var first = coordinator.CreateAsync(CoreTests.Settings().Validate(), Server, new Target(ui, "explorer:1"), default);
        await coordinator.CreateAsync(CoreTests.Settings().Validate(), Server, new Target(ui, "explorer:1"), default);
        Assert.Equal(1, api.Creates); response.SetResult(Conference()); await first;
        coordinator.CompleteNewAppointmentFlow("explorer:1");
        await coordinator.CreateAsync(CoreTests.Settings().Validate(), Server, new Target(ui, "explorer:1"), default);
        Assert.Equal(2, api.Creates);
    }
    [Fact] public async Task ActiveWindowChangesCannotRetargetTheOperation()
    {
        var ui = new FakeUi(); var api = new FakeApi(); var response = Pending<Conference>(); api.Create = (c, id, ct) => response.Task;
        var original = new Target(ui, "original"); var newActive = new Target(ui, "new-active");
        var operation = new CreationCoordinator(api, ui).CreateAsync(CoreTests.Settings().Validate(), Server, original, default);
        response.SetResult(Conference()); await operation;
        Assert.True(original.Inserted); Assert.False(newActive.Inserted);
    }
    [Fact] public async Task ClosedOriginalTargetReturnsManualRecoveryLink()
    {
        var ui = new FakeUi(); var api = new FakeApi(); var response = Pending<Conference>(); api.Create = (c, id, ct) => response.Task;
        var target = new Target(ui, "original");
        var operation = new CreationCoordinator(api, ui).CreateAsync(CoreTests.Settings().Validate(), Server, target, default);
        target.Editable = false; response.SetResult(Conference());
        var outcome = await operation; Assert.Equal(Conference().JoinUrl, outcome.RecoveryUrl); Assert.False(target.Inserted); Assert.True(target.Marked);
    }
    [Theory] [InlineData(true, false)] [InlineData(false, true)]
    public async Task InsertionOrMetadataFailureRetainsGuardAndLink(bool insertion, bool metadata)
    {
        var ui = new FakeUi(); var api = new FakeApi(); var target = new Target(ui, "original") { FailInsert = insertion, FailMetadata = metadata };
        var coordinator = new CreationCoordinator(api, ui);
        var outcome = await coordinator.CreateAsync(CoreTests.Settings().Validate(), Server, target, default);
        Assert.NotNull(outcome.RecoveryUrl); Assert.True(target.Marked);
        await new CreationCoordinator(api, ui).CreateAsync(CoreTests.Settings().Validate(), Server, target, default); Assert.Equal(1, api.Creates);
    }
    [Fact] public async Task PersistedMetadataPreventsCreationEvenWithNewCoordinator()
    {
        var ui = new FakeUi(); var api = new FakeApi(); var target = new Target(ui, "reopened") { Marked = true };
        var result = await new CreationCoordinator(api, ui).CreateAsync(CoreTests.Settings().Validate(), Server, target, default);
        Assert.Contains("Konferenzmetadaten", result.Message); Assert.Equal(0, api.Creates);
    }
    [Fact] public async Task UncertainFailureNeverRetriesEvenAfterVisibleBodyDeletion()
    {
        var ui = new FakeUi(); var api = new FakeApi { Create = (c, id, ct) => throw new MeetLinkException("Ausgang unklar", true) };
        var target = new Target(ui, "original"); var coordinator = new CreationCoordinator(api, ui);
        Assert.Contains("unklar", (await coordinator.CreateAsync(CoreTests.Settings().Validate(), Server, target, default)).Message);
        await coordinator.CreateAsync(CoreTests.Settings().Validate(), Server, target, default);
        await new CreationCoordinator(api, ui).CreateAsync(CoreTests.Settings().Validate(), Server, target, default);
        Assert.Equal(1, api.Creates); Assert.True(target.Marked);
    }
    [Fact] public async Task ClosedOrInvalidContextDoesNotSend()
    {
        var ui = new FakeUi(); var api = new FakeApi(); var coordinator = new CreationCoordinator(api, ui);
        var target = new Target(ui, "closed") { Editable = false };
        await coordinator.CreateAsync(CoreTests.Settings().Validate(), Server, target, default);
        await coordinator.CreateAsync(null, Server, target, default);
        await coordinator.CreateAsync(CoreTests.Settings().Validate(), new Server("", "bad"), target, default);
        Assert.Equal(0, api.Creates);
    }
    [Fact] public async Task ConfigurationChangeDoesNotAffectRunningCreation()
    {
        var ui = new FakeUi(); var api = new FakeApi(); var response = Pending<Conference>(); ConfigurationSnapshot captured = null;
        api.Create = (c, id, ct) => { captured = c; Assert.Equal("technical-id", id); return response.Task; };
        var old = CoreTests.Settings().Validate(); var cache = new ServerCache(api); cache.Reload(old);
        var operation = new CreationCoordinator(api, ui).CreateAsync(old, Server, new Target(ui, "original"), default);
        var changed = CoreTests.Settings(); changed.Body.Heading = "new"; cache.Reload(changed.Validate());
        response.SetResult(Conference()); await operation; Assert.Same(old, captured); Assert.Equal("Meet-Link-Konferenz", captured.Heading);
    }
    [Fact] public async Task OldServerLoadCannotOverwriteNewConfiguration()
    {
        var api = new FakeApi(); var oldResponse = Pending<IReadOnlyList<Server>>();
        api.Load = (c, ct) => oldResponse.Task;
        var cache = new ServerCache(api); cache.Reload(CoreTests.Settings().Validate());
        var oldLoad = cache.RefreshAsync(false, default);
        var changed = CoreTests.Settings(); changed.Body.Heading = "new"; cache.Reload(changed.Validate());
        api.Load = (c, ct) => Task.FromResult<IReadOnlyList<Server>>(new[] { new Server("new", "new") });
        await cache.RefreshAsync(false, default);
        oldResponse.SetResult(new[] { new Server("old", "old") }); await oldLoad;
        Assert.Equal("new", Assert.Single(cache.View.Servers).Id); Assert.False(cache.View.Loading);
    }
    [Fact] public async Task ReloadClearsCacheAndBadConfigurationDisablesRequests()
    {
        var api = new FakeApi(); var cache = new ServerCache(api); cache.Reload(CoreTests.Settings().Validate());
        await cache.RefreshAsync(false, default); await cache.RefreshAsync(false, default); Assert.Equal(1, api.Loads);
        var oldGeneration = cache.View.Generation; await cache.RefreshAsync(true, default); Assert.True(cache.View.Generation > oldGeneration);
        cache.Reload(null, "Konfiguration ungültig"); Assert.Empty(cache.View.Servers); await cache.RefreshAsync(true, default);
        Assert.Equal(2, api.Loads); Assert.NotNull(cache.View.Error); Assert.Equal(0, api.Creates);
    }
    [Fact] public async Task ServerLoadingAndErrorsAreVisibleAndRetryable()
    {
        var api = new FakeApi(); var response = Pending<IReadOnlyList<Server>>(); api.Load = (c, ct) => response.Task;
        var cache = new ServerCache(api); cache.Reload(CoreTests.Settings().Validate());
        var load = cache.RefreshAsync(false, default); Assert.True(cache.View.Loading);
        response.SetException(new MeetLinkException("Serverfehler")); await load; Assert.Equal("Serverfehler", cache.View.Error);
        api.Load = (c, ct) => Task.FromResult<IReadOnlyList<Server>>(Array.Empty<Server>());
        await cache.RefreshAsync(true, default); Assert.Null(cache.View.Error); Assert.Empty(cache.View.Servers); Assert.Equal(0, api.Creates);
    }
}
