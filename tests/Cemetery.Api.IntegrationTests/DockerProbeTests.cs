namespace Cemetery.Api.IntegrationTests;

public sealed class DockerProbeTests
{
    [Fact]
    public void Missing_docker_fails_in_ci()
    {
        var environment = new DockerEnvironment(false, true);
        var error = Assert.Throws<InvalidOperationException>(() => DockerProbe.EnsureAvailable(environment));
        Assert.Equal("Docker is required for integration tests in CI.", error.Message);
    }

    [Fact]
    public void Missing_docker_skips_only_locally() =>
        Assert.False(DockerProbe.EnsureAvailable(new DockerEnvironment(false, false)));

    [Fact]
    public void Present_docker_is_ready() =>
        Assert.True(DockerProbe.EnsureAvailable(new DockerEnvironment(true, false)));
}
