# Bodiless
Bodiless is a small aspnetcore middleware that will let you conditionally discard the body of your responses based on your request headers.

[![Bodiless](https://img.shields.io/nuget/v/Bodiless.svg)](https://www.nuget.org/packages/Bodiless/) 

## Why would I use Bodiless?
**TLDR;** Poor man's load testing with a single computer on a limited network.

### Network capacity limiting load test throughput
When generating load from a single computer your network can quickly become the bottleneck. While your network is swamped with all the response data, you might still not be able to stress the target API as much as you want.

Assuming that you are not interested in testing your network infrastructure, but rather your API internals, you can use Bodiless to discard the response bodies and get a lot more througput.

### Results remain valid
- The requests will still perform the same internal operations, calling dependencies etc.
- HTTP response status is intact.
- HTTP response headers are intact, except `Content-Length: 0`.
- Request time taken will be lacking most of the content download time, but will still give you a very good indication of how your endpoints perform.

### API contracts does not change
As this is middleware there is no need to change any of your API endpoints in any way. It simply works for all of your HTTP GET requests.

## How do I use Bodiless?
Add Bodiless as early as possible in your ASP.NET Core pipeline configuration:

```csharp
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseBodilessResponses();
```

Or with options defining the header you want to use, and optionally the value of this header:
```csharp
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

app.UseBodilessResponses(new BodilessOptions
{
    RequiredHeader = "Discard-Body", 
    RequiredValue = "true"
});
```

If no `RequiredValue` is defined the response body will be discarded based on the presence of the `RequiredHeader`.

When this is in place you simply add the header and value when making your request, e.g. in your scripts for K6, Locust, or other load testing tool of choice.
Regular clients, not using this header, will not notice any difference.

### Combining Bodiless with response compression
Given that you need this, it seems very likely that you should already be using response compression.
When combining these middlewares it is important that the response compression is configured before Bodiless:

```csharp
app.UseResponseCompression();
app.UseBodilessResponses();
...
```

## Can I leave Bodiless installed?
The overhead of this middleware is hardly noticeable, and it can be very convenient to have it permanently installed.
I recommend defining your own custom header, making malicious use less likely.

Any malicious user can only remove responses for their own requests, but in the same way that this lets you put a lot of load on your API from a single computer, it can let malicious users do the same. If you are worried this might happen you can either feature toggle the middleware, or simply add it temporarily while testing, and then remove it after use.

## Testing roadmap
### Current coverage
- `tests/Bodiless.Tests` exercises the middleware through `Microsoft.AspNetCore.TestHost`.
- The test project references `src/Bodiless` directly, which keeps feedback fast while developing inside this repository.
- The current suite already covers header matching and the response compression ordering described above.

### Gaps for NuGet consumers
- The repository does not currently pack Bodiless into a local `.nupkg` and restore that package in a sample application, so it does not verify the exact artifact that consumers restore.
- Consumer-visible response details still need explicit regression coverage, especially the parts users rely on when enabling Bodiless in front of existing endpoints.
- The GitHub Actions workflow publishes the package without first proving that the packaged artifact can be restored and exercised from a clean consumer app.

### Planned follow-up work
1. #4 Add a packed-package smoke test that restores Bodiless from a locally produced `.nupkg` in a sample ASP.NET Core app. This will catch packaging and public API regressions before release.
2. #5 Expand regression coverage for the HTTP contract that consumers observe, including response headers, `Content-Length`, and header matching edge cases. This will protect the externally visible behavior that makes the middleware safe to adopt.
3. #6 Update the CI and publishing workflow to pack Bodiless, run the consumer smoke test, and only then continue toward publication. This will make the release pipeline validate what is actually shipped to NuGet.
