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
Add Bodiless as early as possible in your `Startup.cs` Configure-method:

```
app.UseBodilessResponses();
```

Or with options defining the header you want to use, and optionally the value of this header:
```
app.UseBodilessResponses(new BodilessOptions
{
    RequiredHeader = "Discard-Body", 
    RequiredValue = "true"
});
```

If no `RequiredValue` is defined the response body will be discarded based on the presence of the `RequiredHeader`.

### Combining Bodiless with response compression
Given that you need this, it seems very likely that you should already be using response compression.
When combining this middleware with dkbajl it is important that the response compression is configured before Bodiless:

```
app.UseResponseCompression();
app.UseBodilessResponses();
...
```

## Can I leave Bodiless installed?
The overhead of this middleware is hardly noticable, and it can be very convenient to have it permanently installed.
I recommend defining your own custom header, making malicious use less likely.

Any malicious user can only remove responses for own rquests, but in the same way that this let's you put a lot of load on your API from a single computer, it can let malicious users do the same. If you are worried this might happen you can either feature toggle the middleware, or simply add it temporary while testing, and then remove it after.
