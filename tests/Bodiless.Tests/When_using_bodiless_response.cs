using System.Net;
using System.Threading.Tasks;
using Bodiless.Middleware;
using FluentAssertions;
using Xunit;

namespace Bodiless.Tests
{
    public class When_using_bodiless_response
    {
        [Theory]
        [InlineData("header", "value", "header", "value", "")]
        [InlineData("header", "value", "header", "otherValue", "body")]
        [InlineData("header", "value", "header", null, "body")]
        [InlineData(null, null, "Discard-Body", null, "body")]
        [InlineData(null, null, "Discard-Body", "true", "")]
        [InlineData(null, null, "header", "value", "body")]
        public async Task Given_required_header_and_value_Should_discard_body(
            string requiredHeader,
            string requiredValue,
            string actualHeader,
            string actualValue,
            string expectedBody)
        {
            var fixture = new BodilessTestFixture(conditions =>
            {
                if (requiredHeader != null)
                {
                    conditions.GivenBodilessOptions(new BodilessOptions
                    {
                        RequiredHeader = requiredHeader,
                        RequiredValue = requiredValue
                    });
                }

                conditions.GivenHttpRequestHeaders(actualHeader, actualValue);
            });
            
            var client = await fixture.GetTestClient().ConfigureAwait(false);
            var response = await client.GetAsync("echo/body").ConfigureAwait(false);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            content.Should().Be(expectedBody);
        }

        [Theory]
        [InlineData("header", "value", "header", "value", "")]
        [InlineData("header", "value", "header", "otherValue", "body")]
        [InlineData("header", "value", "header", null, "body")]
        [InlineData(null, null, "Discard-Body", null, "body")]
        [InlineData(null, null, "Discard-Body", "true", "")]
        [InlineData(null, null, "header", "value", "body")]
        public async Task Given_required_header_and_value_With_response_compression_Should_discard_body(
            string requiredHeader,
            string requiredValue,
            string actualHeader,
            string actualValue,
            string expectedBody)
        {
            var fixture = new BodilessTestFixture(conditions =>
            {
                if (requiredHeader != null)
                {
                    conditions.GivenResponseCompression();
                    conditions.GivenBodilessOptions(new BodilessOptions
                    {
                        RequiredHeader = requiredHeader,
                        RequiredValue = requiredValue
                    });
                }

                conditions.GivenHttpRequestHeaders(actualHeader, actualValue);
            });

            var client = await fixture.GetTestClient().ConfigureAwait(false);
            var response = await client.GetAsync("echo/body").ConfigureAwait(false);

            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            content.Should().Be(expectedBody);
        }
    }
}
