namespace Cemetery.Api.IntegrationTests;

internal readonly record struct DockerEnvironment(bool Present, bool ContinuousIntegration);

internal static class DockerProbe
{
    public static bool EnsureAvailable(DockerEnvironment? environment = null)
    {
        var current = environment ?? Detect();
        if (current.Present)
            return true;
        if (current.ContinuousIntegration)
            throw new InvalidOperationException("Docker is required for integration tests in CI.");

        return false;
    }

    private static DockerEnvironment Detect() =>
        new(IsPresent(), IsContinuousIntegration());

    private static bool IsPresent() =>
        File.Exists(@"\\.\pipe\docker_engine") || File.Exists("/var/run/docker.sock");

    private static bool IsContinuousIntegration() =>
        Environment.GetEnvironmentVariable("CI") == "true"
        || Environment.GetEnvironmentVariable("GITHUB_ACTIONS") == "true";
}
