using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.EntityFrameworkCore;
using Sub.Api.Data;
using Sub.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ======================================================
// Controllers
// ======================================================

builder.Services.AddControllers();

// ======================================================
// Swagger / OpenAPI
// ======================================================

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ======================================================
// SQLite Database
// ======================================================

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection")
    )
);

// ======================================================
// Authentication
// ======================================================

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme =
            CookieAuthenticationDefaults.AuthenticationScheme;

        options.DefaultSignInScheme =
            CookieAuthenticationDefaults.AuthenticationScheme;
    })

    // Cookie authentication
    .AddCookie(
        CookieAuthenticationDefaults.AuthenticationScheme
    )

    // Google OAuth
    .AddGoogle(
        GoogleDefaults.AuthenticationScheme,
        options =>
        {
            var clientId =
                builder.Configuration[
                    "Authentication:Google:ClientId"
                ];

            var clientSecret =
                builder.Configuration[
                    "Authentication:Google:ClientSecret"
                ];

            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new InvalidOperationException(
                    "Google ClientId is missing."
                );
            }

            if (string.IsNullOrWhiteSpace(clientSecret))
            {
                throw new InvalidOperationException(
                    "Google ClientSecret is missing."
                );
            }

            options.ClientId = clientId;
            options.ClientSecret = clientSecret;

            // Αποθηκεύει access token / refresh token
            options.SaveTokens = true;

            // Ζητά refresh token από Google
            options.AccessType = "offline";

            // Gmail read-only permission
            options.Scope.Add(
                "https://www.googleapis.com/auth/gmail.readonly"
            );

            // Ζητά ξανά consent από τον χρήστη
            options.AdditionalAuthorizationParameters[
                "prompt"
            ] = "consent";
        }
    );

// ======================================================
// Gmail Service
// ======================================================

builder.Services.AddScoped<GoogleGmailService>();

// ======================================================
// Subscription Detection Engine
// ======================================================

builder.Services.AddScoped<
    ISubscriptionDetectionService,
    SubscriptionDetectionService>();

// ======================================================
// Build App
// ======================================================

var app = builder.Build();

// ======================================================
// Swagger
// ======================================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ======================================================
// Middleware
// ======================================================

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();

// ======================================================
// Controllers
// ======================================================

app.MapControllers();

app.Run();