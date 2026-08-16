using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Toolbc.Api.Data;
using Toolbc.Api.Domain;
using Toolbc.Api.Endpoints;
using Toolbc.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? "Data Source=toolbc.db";

builder.Services.AddDbContext<ToolbcDbContext>(options =>
{
    if (connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase) ||
        connectionString.EndsWith(".db", StringComparison.OrdinalIgnoreCase))
    {
        options.UseSqlite(connectionString);
    }
    else
    {
        options.UseNpgsql(
            connectionString,
            npgsql => npgsql
                .CommandTimeout(60)
                .EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorCodesToAdd: null));
    }
});

builder.Services.AddCors(options =>
{
    options.AddPolicy("FlutterDev", policy =>
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .SetIsOriginAllowed(_ => true));
});

builder.Services.AddHttpClient<IGeminiChatService, GeminiChatService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddSingleton<IPasswordHasher, PasswordHasher>();
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? "development-only-change-this-toolbc-secret-key-32";
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "ToolBC",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "ToolBC.Mobile",
            IssuerSigningKey = signingKey,
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole(UserRole.Admin.ToString()));
    options.AddPolicy("DoctorOnly", policy => policy.RequireRole(UserRole.Doctor.ToString()));
    options.AddPolicy("PatientOnly", policy => policy.RequireRole(UserRole.Patient.ToString()));
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ToolbcDbContext>();
    if (db.Database.IsSqlite())
    {
        await db.Database.EnsureCreatedAsync();
    }
    else if (app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
    {
        await db.Database.MigrateAsync();
    }

    if (app.Configuration.GetValue<bool>("Database:SeedDemoData", true))
    {
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();
        await ToolbcSeedData.SeedAsync(db, passwordHasher);
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("FlutterDev");
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapToolbcEndpoints();

app.Run();
