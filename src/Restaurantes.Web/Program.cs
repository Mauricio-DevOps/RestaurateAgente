using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Restaurantes.Web.Data;
using Restaurantes.Web.Models;
using Restaurantes.Web.Options;
using Restaurantes.Web.Services;

var builder = WebApplication.CreateBuilder(args);
AddLocalSupabaseEnvironment(builder.Configuration, builder.Environment.ContentRootPath);

var connectionString = FirstNonEmpty(
    builder.Configuration["SUPABASE_DB_URL"],
    builder.Configuration.GetConnectionString("DefaultConnection"))
    ?? throw new InvalidOperationException("Configure SUPABASE_DB_URL with the Supabase/PostgreSQL connection string.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(BuildNpgsqlConnectionString(connectionString)));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/login";
    options.AccessDeniedPath = "/login";
    options.ReturnUrlParameter = "next";
});

builder.Services.AddHttpContextAccessor();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedHost |
        ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});
builder.Services.Configure<SsoOptions>(builder.Configuration.GetSection(SsoOptions.SectionName));
builder.Services.Configure<InternalApiOptions>(builder.Configuration.GetSection(InternalApiOptions.SectionName));
builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, RestaurantClaimsPrincipalFactory>();
builder.Services.AddScoped<MasterService>();
builder.Services.AddScoped<RestaurantService>();
builder.Services.AddScoped<ExternalUrlResolver>();
builder.Services.AddScoped<WaiterLoginService>();
builder.Services.AddSingleton<RestaurantSsoTokenService>();
builder.Services.AddHttpClient<InternalWhatsAppApiClient>(client =>
{
    var baseUrl = builder.Configuration["InternalApi:BaseUrl"] ?? "http://localhost:5253";
    client.BaseAddress = new Uri(baseUrl);
});
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddRazorPages();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    if (!IsPostgresProvider(db))
    {
        await db.Database.MigrateAsync();
    }

    await DatabaseSeeder.SeedAsync(scope.ServiceProvider);
}

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();

static string? FirstNonEmpty(params string?[] values)
{
    return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}

static string BuildNpgsqlConnectionString(string value)
{
    if (Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (string.Equals(uri.Scheme, "postgresql", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(uri.Scheme, "postgres", StringComparison.OrdinalIgnoreCase)))
    {
        var userInfo = uri.UserInfo.Split(':', 2);
        if (userInfo.Length != 2)
        {
            throw new InvalidOperationException("The PostgreSQL URI must include user and password.");
        }

        return new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.Port > 0 ? uri.Port : 5432,
            Database = string.IsNullOrWhiteSpace(uri.AbsolutePath.TrimStart('/'))
                ? "postgres"
                : Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = Uri.UnescapeDataString(userInfo[1]),
            SslMode = SslMode.Require,
            Timeout = 30,
            CommandTimeout = 120,
            Pooling = false
        }.ConnectionString;
    }

    var builder = new NpgsqlConnectionStringBuilder(value);
    if (builder.SslMode == SslMode.Disable)
    {
        builder.SslMode = SslMode.Require;
    }

    if (builder.CommandTimeout == 30)
    {
        builder.CommandTimeout = 120;
    }

    builder.Pooling = false;
    return builder.ConnectionString;
}

static bool IsPostgresProvider(ApplicationDbContext db)
{
    return db.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;
}

static void AddLocalSupabaseEnvironment(ConfigurationManager configuration, string contentRootPath)
{
    var directory = new DirectoryInfo(contentRootPath);
    while (directory is not null)
    {
        var envPath = Path.Combine(directory.FullName, ".env.supabase.local");
        if (File.Exists(envPath))
        {
            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            foreach (var rawLine in File.ReadAllLines(envPath))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                {
                    continue;
                }

                var separatorIndex = line.IndexOf('=');
                if (separatorIndex <= 0)
                {
                    continue;
                }

                var key = line[..separatorIndex].Trim();
                if (!string.IsNullOrWhiteSpace(configuration[key]))
                {
                    continue;
                }

                values[key] = line[(separatorIndex + 1)..].Trim();
            }

            if (values.Count > 0)
            {
                configuration.AddInMemoryCollection(values);
            }

            return;
        }

        directory = directory.Parent;
    }
}
