using Yarp.ReverseProxy.Transforms;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// YARP reverse proxy: routes come from configuration; destination addresses like
// "http://api" / "http://web" are resolved via Aspire service discovery.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver()
    .AddTransforms(context => context.AddRequestTransform(request =>
    {
        // Drop Tailscale's identity headers before forwarding.
        //
        // `tailscale serve` stamps every proxied request with Tailscale-User-Login, -Name and
        // -Profile-Pic. A display name is free text, and the moment it holds a non-ASCII
        // character HttpClient refuses to write the header at all: YARP then answers 502 to
        // EVERY request, on every route, with "Request headers must contain only ASCII
        // characters" as the only clue - the services behind it are healthy and look it.
        //
        // Nothing downstream reads them. The API authenticates with Auth0 tokens and minted
        // keys, so forwarding a header that says who Tailscale thinks you are would be an
        // identity signal nobody should start trusting by accident.
        foreach (var header in TailscaleIdentityHeaders)
        {
            request.ProxyRequest.Headers.Remove(header);
        }

        return ValueTask.CompletedTask;
    }));

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapReverseProxy();

app.Run();

// Set by `tailscale serve` on every request it proxies; see the transform above.
static partial class Program
{
    private static readonly string[] TailscaleIdentityHeaders =
    [
        "Tailscale-User-Login",
        "Tailscale-User-Name",
        "Tailscale-User-Profile-Pic",
    ];
}
