using System;
using System.Net.Http;
using System.Threading.Tasks;
using Bodiless.Middleware;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ApplicationBuilderExtensions = Bodiless.Extensions.ApplicationBuilderExtensions;

namespace Bodiless.Tests
{
    public class BodilessTestFixture
    {
        private bool _useResponseCompression;
        private string _header;
        private string _value;
        private BodilessOptions _options;

        public BodilessTestFixture(Action<BodilessTestFixture> conditions)
        {
            conditions.Invoke(this);
        }

        public void GivenBodilessOptions(BodilessOptions options)
        {
            _options = options;
        }

        public void GivenResponseCompression()
        {
            _useResponseCompression = true;
        }

        public void GivenHttpRequestHeaders(string header, string value)
        {
            _header = header;
            _value = value;
        }

        public async Task<HttpClient> GetTestClient()
        {
            var host = await new HostBuilder()
                .ConfigureWebHost(webBuilder =>
                {
                    webBuilder
                        .UseTestServer()
                        .ConfigureServices(services =>
                        {
                            services
                                .AddMvcCore()
                                .AddControllersAsServices();

                            if (_useResponseCompression)
                            {
                                services.AddResponseCompression();
                            }
                        })
                        .Configure(app =>
                        {
                            if (_useResponseCompression)
                            {
                                ResponseCompressionBuilderExtensions.UseResponseCompression(app);
                            }

                            if (_options == null)
                            {
                                ApplicationBuilderExtensions.UseBodilessResponses(app);
                            }
                            else
                            {
                                ApplicationBuilderExtensions.UseBodilessResponses(app, _options);
                            }

                            EndpointRoutingApplicationBuilderExtensions.UseRouting(app);
                            EndpointRoutingApplicationBuilderExtensions.UseEndpoints(app, endpoints =>
                            {
                                ControllerEndpointRouteBuilderExtensions.MapControllers(endpoints);
                            });
                        });
                })
                .StartAsync();

            var httpClient = host.GetTestClient();

            if (_header != null)
            {
                httpClient.DefaultRequestHeaders.Add(_header, _value);
            }

            return httpClient;
        }
    }
}