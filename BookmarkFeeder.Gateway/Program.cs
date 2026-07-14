var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// YARP reverse proxy: routes come from configuration; destination addresses like
// "http://api" / "http://web" are resolved via Aspire service discovery.
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddServiceDiscoveryDestinationResolver();

var app = builder.Build();

app.MapDefaultEndpoints();
app.MapReverseProxy();

app.Run();
