using System.Net;
using Bodiless.Middleware;
using Xunit;

namespace Bodiless.Tests;

public class When_using_bodiless_response
{
    [Theory]
    [InlineData("header", "value", "header", "value", "")]
    [InlineData("header", "value", "header", "otherValue", "body")]
    [InlineData("header", "value", "header", null, "body")]
    [InlineData(null, null, "Discard-Body", null, "body")]
    [InlineData(null, null, "Discard-Body", "true", "")]
    [InlineData(null, null, "header", "value", "body")]
    public async Task Should_discard_body_when_the_required_header_matches(
        string? requiredHeader,
        string? requiredValue,
        string actualHeader,
        string? actualValue,
        string expectedBody)
    {
        await using var fixture = new BodilessTestFixture(conditions =>
        {
            if (requiredHeader is not null)
            {
                conditions.GivenBodilessOptions(new BodilessOptions
                {
                    RequiredHeader = requiredHeader,
                    RequiredValue = requiredValue
                });
            }

            conditions.GivenHttpRequestHeaders(actualHeader, actualValue);
        });

        var client = await fixture.GetTestClient();
        var response = await client.GetAsync("echo/body");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(expectedBody, content);
    }

    [Theory]
    [InlineData("header", "value", "header", "value", "")]
    [InlineData("header", "value", "header", "otherValue", "body")]
    [InlineData("header", "value", "header", null, "body")]
    [InlineData(null, null, "Discard-Body", null, "body")]
    [InlineData(null, null, "Discard-Body", "true", "")]
    [InlineData(null, null, "header", "value", "body")]
    public async Task Should_discard_body_when_response_compression_runs_before_bodiless(
        string? requiredHeader,
        string? requiredValue,
        string actualHeader,
        string? actualValue,
        string expectedBody)
    {
        await using var fixture = new BodilessTestFixture(conditions =>
        {
            conditions.GivenResponseCompression();

            if (requiredHeader is not null)
            {
                conditions.GivenBodilessOptions(new BodilessOptions
                {
                    RequiredHeader = requiredHeader,
                    RequiredValue = requiredValue
                });
            }

            conditions.GivenHttpRequestHeaders(actualHeader, actualValue);
        });

        var client = await fixture.GetTestClient();
        var response = await client.GetAsync("echo/body");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();

        Assert.Equal(expectedBody, content);
    }

    [Theory]
    [InlineData(null, null, "Discard-Body", "true")]
    [InlineData("X-Bodiless", "enabled", "X-Bodiless", "enabled")]
    public async Task Should_preserve_response_headers_and_set_content_length_to_zero_when_discarding_the_body(
        string? requiredHeader,
        string? requiredValue,
        string actualHeader,
        string actualValue)
    {
        await using var fixture = new BodilessTestFixture(conditions =>
        {
            if (requiredHeader is not null)
            {
                conditions.GivenBodilessOptions(new BodilessOptions
                {
                    RequiredHeader = requiredHeader,
                    RequiredValue = requiredValue
                });
            }
        });

        var client = await fixture.GetTestClient();
        using var response = await SendGetRequest(client, "echo/headers/body", (actualHeader, [actualValue]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
        Assert.Equal(0, response.Content.Headers.ContentLength);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        AssertHeaderValues(response, "X-Test-Header", "expected");
        AssertHeaderValues(response, "X-Test-Multi", "first", "second");
    }

    [Fact]
    public async Task Should_preserve_status_code_when_discarding_the_body()
    {
        await using var fixture = new BodilessTestFixture(conditions =>
        {
            conditions.GivenHttpRequestHeaders("Discard-Body", "true");
        });

        var client = await fixture.GetTestClient();
        using var response = await client.GetAsync("echo/status/202/accepted");

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
        Assert.Equal(0, response.Content.Headers.ContentLength);
        AssertHeaderValues(response, "X-Test-Header", "expected");
    }

    [Fact]
    public async Task Should_discard_the_body_when_any_of_multiple_header_values_matches()
    {
        await using var fixture = new BodilessTestFixture(_ => { });
        var client = await fixture.GetTestClient();
        using var response = await SendGetRequest(client, "echo/body", ("Discard-Body", ["false", "true"]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
        Assert.Equal(0, response.Content.Headers.ContentLength);
    }

    [Fact]
    public async Task Should_preserve_the_body_when_none_of_multiple_header_values_matches()
    {
        await using var fixture = new BodilessTestFixture(_ => { });
        var client = await fixture.GetTestClient();
        using var response = await SendGetRequest(client, "echo/body", ("Discard-Body", ["false", "TRUE"]));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("body", await response.Content.ReadAsStringAsync());
    }

    private static void AssertHeaderValues(HttpResponseMessage response, string headerName, params string[] expectedValues)
    {
        Assert.True(response.Headers.TryGetValues(headerName, out var actualValues));
        Assert.Equal(expectedValues, actualValues);
    }

    private static Task<HttpResponseMessage> SendGetRequest(
        HttpClient client,
        string requestUri,
        params (string HeaderName, IEnumerable<string> Values)[] headers)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

        foreach (var (headerName, values) in headers)
        {
            request.Headers.Add(headerName, values);
        }

        return client.SendAsync(request);
    }
}
