using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Xml.Linq;
using Xunit;

namespace Bodiless.Tests;

public class When_consuming_the_packed_nuget_package
{
    [Fact]
    public async Task Should_allow_a_minimal_application_to_use_bodiless_responses()
    {
        await using var application = await PackagedBodilessApplication.Create();

        using var client = new HttpClient
        {
            BaseAddress = application.BaseAddress
        };

        using var regularResponse = await client.GetAsync("echo/body");
        var regularBody = await regularResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, regularResponse.StatusCode);
        Assert.Equal("body", regularBody);

        using var bodilessRequest = new HttpRequestMessage(HttpMethod.Get, "echo/body");
        bodilessRequest.Headers.Add("Discard-Body", "true");

        using var bodilessResponse = await client.SendAsync(bodilessRequest);
        var bodilessBody = await bodilessResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, bodilessResponse.StatusCode);
        Assert.Equal(string.Empty, bodilessBody);
        Assert.Equal(0, bodilessResponse.Content.Headers.ContentLength);
    }
}

internal sealed class PackagedBodilessApplication : IAsyncDisposable
{
    private const string ApplicationFileName = "SmokeTestApp.csproj";
    private const string PackageIdElementName = "PackageId";
    private const string PrepackedPackagePathEnvironmentVariable = "BODILESS_NUPKG_PATH";
    private const int StartupAttemptCount = 5;
    private const string VersionElementName = "Version";
    private readonly Task<string> errorOutput;
    private readonly Task<string> standardOutput;
    private readonly string workspaceDirectory;

    private PackagedBodilessApplication(Uri baseAddress, Process process, Task<string> standardOutput, Task<string> errorOutput, string workspaceDirectory)
    {
        BaseAddress = baseAddress;
        this.process = process;
        this.standardOutput = standardOutput;
        this.errorOutput = errorOutput;
        this.workspaceDirectory = workspaceDirectory;
    }

    public Uri BaseAddress { get; }

    private Process process { get; }

    public static async Task<PackagedBodilessApplication> Create()
    {
        AddressInUseException? lastAddressInUseException = null;

        for (var attempt = 0; attempt < StartupAttemptCount; attempt++)
        {
            try
            {
                return await CreateOnce();
            }
            catch (AddressInUseException exception) when (attempt < StartupAttemptCount - 1)
            {
                lastAddressInUseException = exception;
            }
        }

        throw new InvalidOperationException("Failed to start the packaged smoke test application after retrying address allocation.", lastAddressInUseException);
    }

    private static async Task<PackagedBodilessApplication> CreateOnce()
    {
        var repositoryRoot = FindRepositoryRoot();
        var bodilessProjectPath = Path.Combine(repositoryRoot, "src", "Bodiless", "Bodiless.csproj");
        var prepackedPackagePath = Environment.GetEnvironmentVariable(PrepackedPackagePathEnvironmentVariable);
        var workspaceDirectory = Path.Combine(Path.GetTempPath(), $"bodiless-smoke-{Guid.NewGuid():N}");
        var localFeedDirectory = Path.Combine(workspaceDirectory, "feed");
        var packagesDirectory = Path.Combine(workspaceDirectory, "packages");
        var applicationDirectory = Path.Combine(workspaceDirectory, "app");
        var packageIdentity = ReadPackageIdentity(bodilessProjectPath);
        Process? process = null;
        Task<string>? standardOutput = null;
        Task<string>? errorOutput = null;

        try
        {
            Directory.CreateDirectory(localFeedDirectory);
            Directory.CreateDirectory(packagesDirectory);
            Directory.CreateDirectory(applicationDirectory);

            var environmentVariables = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
                ["DOTNET_NOLOGO"] = "1",
                ["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1",
                ["NUGET_PACKAGES"] = packagesDirectory
            };

            if (string.IsNullOrWhiteSpace(prepackedPackagePath))
            {
                await RunDotnet(
                    repositoryRoot,
                    environmentVariables,
                    "pack",
                    bodilessProjectPath,
                    "--configuration",
                    "Release",
                    "--output",
                    localFeedDirectory);
            }
            else
            {
                var prepackedPackageFullPath = Path.GetFullPath(prepackedPackagePath);

                if (!File.Exists(prepackedPackageFullPath))
                {
                    throw new FileNotFoundException(
                        $"The prepacked Bodiless package specified by '{PrepackedPackagePathEnvironmentVariable}' was not found.",
                        prepackedPackageFullPath);
                }

                File.Copy(
                    prepackedPackageFullPath,
                    Path.Combine(localFeedDirectory, Path.GetFileName(prepackedPackageFullPath)),
                    overwrite: true);
            }

            await File.WriteAllTextAsync(
                Path.Combine(applicationDirectory, ApplicationFileName),
                $$"""
                <Project Sdk="Microsoft.NET.Sdk.Web">
                  <PropertyGroup>
                    <TargetFramework>net10.0</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                  </PropertyGroup>

                  <ItemGroup>
                    <PackageReference Include="{{packageIdentity.Id}}" Version="{{packageIdentity.Version}}" />
                  </ItemGroup>
                </Project>
                """);

            await File.WriteAllTextAsync(
                Path.Combine(applicationDirectory, "Program.cs"),
                """
                using Bodiless.Extensions;

                var builder = WebApplication.CreateBuilder(args);
                var app = builder.Build();

                app.UseBodilessResponses();
                app.MapGet("/echo/{text}", (string text) => Results.Text(text));

                app.Run();
                """);

            var nugetConfiguration = new XDocument(
                new XElement(
                    "configuration",
                    new XElement(
                        "packageSources",
                        new XElement("clear"),
                        new XElement(
                            "add",
                            new XAttribute("key", "local"),
                            new XAttribute("value", localFeedDirectory)))));

            await File.WriteAllTextAsync(Path.Combine(applicationDirectory, "NuGet.Config"), nugetConfiguration.ToString());

            await RunDotnet(
                applicationDirectory,
                environmentVariables,
                "restore",
                ApplicationFileName,
                "--configfile",
                Path.Combine(applicationDirectory, "NuGet.Config"));

            await RunDotnet(
                applicationDirectory,
                environmentVariables,
                "build",
                ApplicationFileName,
                "--no-restore");

            var port = ReservePort();
            var baseAddress = new Uri($"http://127.0.0.1:{port}/", UriKind.Absolute);
            var startInfo = new ProcessStartInfo("dotnet")
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                WorkingDirectory = applicationDirectory
            };

            foreach (var argument in new[] { "run", "--project", ApplicationFileName, "--no-build", "--urls", baseAddress.ToString() })
            {
                startInfo.ArgumentList.Add(argument);
            }

            foreach (var (key, value) in environmentVariables)
            {
                startInfo.Environment[key] = value;
            }

            process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start the packaged smoke test application.");
            standardOutput = process.StandardOutput.ReadToEndAsync();
            errorOutput = process.StandardError.ReadToEndAsync();
            var application = new PackagedBodilessApplication(baseAddress, process, standardOutput, errorOutput, workspaceDirectory);

            await application.WaitUntilReady();

            return application;
        }
        catch (Exception exception)
        {
            if (process is not null)
            {
                await StopProcessAsync(process);
                process.Dispose();
            }

            await DeleteWorkspaceDirectory(workspaceDirectory);

            if (standardOutput is not null && errorOutput is not null && await HasAddressInUseFailure(standardOutput, errorOutput))
            {
                throw new AddressInUseException("Failed to start the packaged smoke test application because the selected port was taken before the child process bound it.", exception);
            }

            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopProcessAsync(process);
        process.Dispose();

        await DeleteWorkspaceDirectory(workspaceDirectory);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Bodiless.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the repository root from the test output directory.");
    }

    private static (string Id, string Version) ReadPackageIdentity(string projectPath)
    {
        var project = XDocument.Load(projectPath);
        var root = project.Root ?? throw new InvalidOperationException($"Project file '{projectPath}' is missing a root element.");
        var id = root.Descendants().FirstOrDefault(element => element.Name.LocalName == PackageIdElementName)?.Value;
        var version = root.Descendants().FirstOrDefault(element => element.Name.LocalName == VersionElementName)?.Value;

        return (
            string.IsNullOrWhiteSpace(id) ? Path.GetFileNameWithoutExtension(projectPath) : id,
            string.IsNullOrWhiteSpace(version)
                ? throw new InvalidOperationException($"Project file '{projectPath}' does not define a version.")
                : version);
    }

    private static async Task RunDotnet(string workingDirectory, IReadOnlyDictionary<string, string> environmentVariables, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            WorkingDirectory = workingDirectory
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        foreach (var (key, value) in environmentVariables)
        {
            startInfo.Environment[key] = value;
        }

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start dotnet.");
        var standardOutput = await process.StandardOutput.ReadToEndAsync();
        var errorOutput = await process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync();

        if (process.ExitCode == 0)
        {
            return;
        }

        var command = string.Join(" ", arguments.Select(argument => argument.Contains(' ', StringComparison.Ordinal) ? $"\"{argument}\"" : argument));

        throw new InvalidOperationException(
            $$"""
            dotnet {{command}} failed with exit code {{process.ExitCode}}.
            Standard output:
            {{standardOutput}}
            Standard error:
            {{errorOutput}}
            """);
    }

    private static int ReservePort()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();

        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static async Task DeleteWorkspaceDirectory(string workspaceDirectory)
    {
        if (!Directory.Exists(workspaceDirectory))
        {
            return;
        }

        Exception? lastException = null;

        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                Directory.Delete(workspaceDirectory, recursive: true);
                return;
            }
            catch (IOException exception)
            {
                lastException = exception;
            }
            catch (UnauthorizedAccessException exception)
            {
                lastException = exception;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        throw new InvalidOperationException($"Failed to clean up packaged smoke test workspace '{workspaceDirectory}'.", lastException);
    }

    private static async Task<bool> HasAddressInUseFailure(Task<string> standardOutput, Task<string> errorOutput)
    {
        var combinedOutput = $"{await standardOutput}\n{await errorOutput}";

        return combinedOutput.Contains("address already in use", StringComparison.OrdinalIgnoreCase)
            || combinedOutput.Contains("only one usage of each socket address", StringComparison.OrdinalIgnoreCase)
            || combinedOutput.Contains("failed to bind to address", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task StopProcessAsync(Process process)
    {
        if (!process.HasExited)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException) when (process.HasExited)
            {
            }
        }

        await process.WaitForExitAsync();
    }

    private async Task WaitUntilReady()
    {
        using var client = new HttpClient
        {
            BaseAddress = BaseAddress
        };

        for (var attempt = 0; attempt < 40; attempt++)
        {
            if (process.HasExited)
            {
                throw new InvalidOperationException(await BuildStartupFailureMessage("The packaged smoke test application exited before it became ready."));
            }

            try
            {
                using var response = await client.GetAsync("echo/ready", HttpCompletionOption.ResponseHeadersRead);

                if (response.StatusCode == HttpStatusCode.OK)
                {
                    return;
                }
            }
            catch (HttpRequestException)
            {
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        throw new TimeoutException(await BuildStartupFailureMessage("Timed out while waiting for the packaged smoke test application to start."));
    }

    private async Task<string> BuildStartupFailureMessage(string message)
    {
        var standardOutputValue = await standardOutput;
        var errorOutputValue = await errorOutput;

        return $$"""
            {{message}}
            Standard output:
            {{standardOutputValue}}
            Standard error:
            {{errorOutputValue}}
            """;
    }

    private sealed class AddressInUseException(string message, Exception innerException) : Exception(message, innerException);
}
