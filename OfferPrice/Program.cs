using OfferPrice.Application.ExternalServices;
using OfferPrice.Application.Interfaces;
using OfferPrice.Application.Services;
using OfferPrice.Infrastructure.ExternalServices;
using OfferPrice.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register memory cache
builder.Services.AddMemoryCache();

// Get HMAC configuration
var appId = builder.Configuration["Hmac:AppId"];
var apiKey = builder.Configuration["Hmac:ApiKey"];

if (string.IsNullOrEmpty(appId) || string.IsNullOrEmpty(apiKey))
{
    throw new InvalidOperationException("HMAC AppId and ApiKey must be configured in appsettings.json");
}

builder.Services.AddScoped<ICacheService, CacheService>();
builder.Services.AddScoped<IFlightOrchestrationService, FlightOrchestrationService>();


var flightSearchUrl = builder.Configuration["ExternalApis:FlightSearch"];
if (string.IsNullOrEmpty(flightSearchUrl))
    throw new InvalidOperationException("FlightSearch URL must be configured");

builder.Services.AddHttpClient<IFlightSearchService, FlightSearchApiClient>(client =>
{
    client.BaseAddress = new Uri(flightSearchUrl);
    client.Timeout = TimeSpan.FromMinutes(1);
})
.AddHttpMessageHandler(() => new HmacAuthenticationHandler(appId, apiKey));

var offerPriceUrl = builder.Configuration["ExternalApis:OfferPrice"];
if (string.IsNullOrEmpty(offerPriceUrl))
    throw new InvalidOperationException("OfferPrice URL must be configured");

builder.Services.AddHttpClient<IOfferPriceService, OfferPriceApiClient>(client =>
{
    client.BaseAddress = new Uri(offerPriceUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler(() => new HmacAuthenticationHandler(appId, apiKey));

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();