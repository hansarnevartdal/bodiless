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
}
