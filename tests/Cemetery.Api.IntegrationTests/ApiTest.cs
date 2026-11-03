namespace Cemetery.Api.IntegrationTests;

public abstract class ApiTest(ApiFixture api) : IAsyncLifetime
{
    protected ApiFixture Api { get; } = api;

    public async ValueTask InitializeAsync()
    {
        if (!Api.IsReady)
            Assert.Skip("Docker is not available.");

        await Api.ResetAsync();
    }

    public ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        return ValueTask.CompletedTask;
    }
}
