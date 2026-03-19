using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Bodiless.Tests.Controllers;

[ApiController]
[Route("echo")]
public class EchoController : ControllerBase
{
    [HttpGet("{text}")]
    public ActionResult<string> Echo(string text)
    {
        return Ok(text);
    }

    [HttpGet("headers/{text}")]
    public IActionResult EchoWithHeaders(string text)
    {
        Response.Headers.Append("X-Test-Header", "expected");
        Response.Headers.Append("X-Test-Multi", "first");
        Response.Headers.Append("X-Test-Multi", "second");

        return new ContentResult
        {
            Content = text,
            ContentType = "text/plain",
            StatusCode = 200
        };
    }

    [HttpGet("status/{statusCode:int}/{text}")]
    public IActionResult EchoWithStatusCode(int statusCode, string text)
    {
        Response.Headers.Append("X-Test-Header", "expected");

        return new ContentResult
        {
            Content = text,
            ContentType = "text/plain",
            StatusCode = statusCode
        };
    }
}
