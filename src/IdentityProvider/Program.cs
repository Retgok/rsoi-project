using BCrypt.Net;
using IdentityProvider;
using Microsoft.EntityFrameworkCore;
using SharedAuth;
using SharedEvents;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEnvironmentVariables();
var connectionString = Environment.GetEnvironmentVariable("DB_CONN_STR")
                       ?? builder.Configuration.GetConnectionString("DefaultConnection")
                       ?? "Host=postgres;Port=5432;Database=identity;Username=program;Password=test";

builder.Services.AddRequestMetrics("identity-provider");
builder.Services.AddDbContext<IdentityDb>((sp, options) =>
    options.UseNpgsql(connectionString).AddInterceptors(sp.GetRequiredService<DbMetricsInterceptor>()));
builder.Services.AddSingleton<TokenService>();
builder.Services.AddKafkaEventPublisher();
builder.Services.AddServiceJwtAuth(builder.Configuration, preloadJwks: false);
builder.Services.AddSingleton<SharedAuth.IJwksProvider, LocalJwksProvider>();
builder.Services.AddControllers();
builder.Services.AddOpenApiDocument(config =>
{
    config.DocumentName = "IdentityProviderAPI";
    config.Title = "Identity Provider";
    config.Version = "v1";
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<IdentityDb>();
    await SeedDataAsync(db, app.Configuration);
}

app.UseOpenApi();
app.UseSwaggerUi();
app.UseServiceJwtAuth();
app.UseRequestMetrics();
app.MapControllers();
app.Run();

static async Task SeedDataAsync(IdentityDb db, IConfiguration config)
{
    const string defaultRedirects =
        "http://localhost:3000/callback,http://localhost:8080/api/v1/callback,http://localhost/api/v1/callback,http://flight.local/callback,http://flight.local/api/v1/callback";

    var issuer = (config["Auth:Issuer"] ?? "").Trim().TrimEnd('/');
    var extras = config["Auth:ExtraRedirectUris"] ?? "";
    var fromIssuer = string.IsNullOrEmpty(issuer)
        ? ""
        : $"{issuer}/api/v1/callback,{issuer}/callback";

    static IEnumerable<string> SplitUris(string value) =>
        value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    var redirectUris = string.Join(",",
        SplitUris(defaultRedirects)
            .Concat(SplitUris(extras))
            .Concat(SplitUris(fromIssuer))
            .Distinct(StringComparer.OrdinalIgnoreCase));

    var client = await db.Clients.FirstOrDefaultAsync(c => c.ClientId == "flight-ui");
    if (client == null)
    {
        db.Clients.Add(new OauthClient
        {
            ClientId = "flight-ui",
            ClientSecret = "flight-ui-secret",
            RedirectUris = redirectUris
        });
    }
    else
    {
        var merged = string.Join(",",
            SplitUris(client.RedirectUris)
                .Concat(SplitUris(redirectUris))
                .Distinct(StringComparer.OrdinalIgnoreCase));
        if (!string.Equals(client.RedirectUris, merged, StringComparison.Ordinal))
            client.RedirectUris = merged;
    }

    if (!await db.Users.AnyAsync(u => u.Username == "admin"))
    {
        db.Users.Add(new UserAccount
        {
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
            Email = "admin@example.com",
            FirstName = "Admin",
            LastName = "User",
            Role = "Admin"
        });
    }

    if (!await db.Users.AnyAsync(u => u.Username == "Test Max"))
    {
        db.Users.Add(new UserAccount
        {
            Username = "Test Max",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("test123"),
            Email = "testmax@example.com",
            FirstName = "Test",
            LastName = "Max",
            Role = "User"
        });
    }

    await db.SaveChangesAsync();
}
