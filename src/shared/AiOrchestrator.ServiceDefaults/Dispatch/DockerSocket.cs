using Docker.DotNet;

namespace AiOrchestrator.ServiceDefaults.Dispatch;

/// <summary>
/// Where the pod machinery finds the docker daemon (#257): the socket spoken directly through
/// <c>Docker.DotNet</c>, replacing the docker CLI binary the old Dockerfile copied into the
/// image. The endpoint is <c>DOCKER_HOST</c> when the operator set one, otherwise the platform
/// default — the same resolution the CLI performed, minus the CLI.
/// </summary>
static class DockerSocket
{
    public static Uri Endpoint()
    {
        var host = Environment.GetEnvironmentVariable("DOCKER_HOST");
        if (!string.IsNullOrWhiteSpace(host) && Uri.TryCreate(host, UriKind.Absolute, out var uri))
        {
            return uri;
        }

        return OperatingSystem.IsWindows()
            ? new Uri("npipe://./pipe/docker_engine")
            : new Uri("unix:///var/run/docker.sock");
    }

    /// <summary>Construction never touches the socket; the first call does.</summary>
    public static DockerClient CreateClient() =>
        new DockerClientConfiguration(Endpoint()).CreateClient();
}
