using System.Net.Http;
using Bodiless.Extensions;
using Bodiless.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Bodiless.Tests;

public sealed class BodilessTestFixture(Action<BodilessTestFixture> configure) : IAsyncDisposable
{
    private readonly Action<BodilessTestFixture> configure = configure ?? throw new ArgumentNullException(nameof(configure));
    private HttpClient? client;
    private string? header;
    private IHost? host;
    private BodilessOptions? options;
    private bool useResponseCompression;
    private string? value;

    public void GivenBodilessOptions(BodilessOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        this.options = options;
    }

    public void GivenResponseCompression()
    {
        useResponseCompression = true;
    }

    public void GivenHttpRequestHeaders(string header, string? value)
    {
        ArgumentNullException.ThrowIfNull(header);

        this.header = header;
        this.value = value;
    }

    public async Task<HttpClient> GetTestClient()
    {
        if (client is not null)
        {
            return client;
        }

        configure(this);

        host = await new HostBuilder()
            .ConfigureWebHost(webBuilder =>
            {
                webBuilder
                    .UseTestServer()
                    .ConfigureServices(services =>
                    {
                        services.AddControllers();

                        if (useResponseCompression)
                        {
                            services.AddResponseCompression();
                        }
                    })
                    .Configure(app =>
                    {
                        if (useResponseCompression)
                        {
                            app.UseResponseCompression();
                        }

                        if (options is null)
                        {
                            app.UseBodilessResponses();
                        }
                        else
                        {
                            app.UseBodilessResponses(options);
                        }

                        app.UseRouting();
                        app.UseEndpoints(endpoints => endpoints.MapControllers());
                    });
            })
            .StartAsync();

        client = host.GetTestClient();

        if (header is not null)
        {
            if (value is null)
            {
                client.DefaultRequestHeaders.Add(header, [""]);
            }
            else
            {
                client.DefaultRequestHeaders.Add(header, value);
            }
        }

        return client;
    }

    public async ValueTask DisposeAsync()
    {
        client?.Dispose();

        if (host is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
            return;
        }

        host?.Dispose();
    }
}
