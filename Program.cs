using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.EntityFrameworkCore;
using Sub.Api.Data;
using Sub.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// ----------------------------------------------------
// Controllers
// ----------------------------------------------------

builder.Services.AddControllers();


// ----------------------------------------------------
// Swagger
// ----------------------------------------------------

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


// ----------------------------------------------------
// Database - SQLite
// ----------------------------------------------------

builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString =
        builder.Configuration.GetConnectionString("DefaultConnection");

    options.UseSqlite(connectionString);
});


// ----------------------------------------------------
// Google Authentication / Gmail OAuth
// ----------------------------------------------------

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme =
            CookieAuthenticationDefaults.AuthenticationScheme;

        options.DefaultChallengeScheme =
            GoogleDefaults.AuthenticationScheme;
    })
    .AddCookie()
    .AddGoogle(options =>
    {
        var clientId =
            builder.Configuration["Authentication:Google:ClientId"];

        var clientSecret =
            builder.Configuration["Authentication:Google:ClientSecret"];

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

        // Κρατάμε access / refresh tokens
        options.SaveTokens = true;

        // Read-only πρόσβαση στο Gmail
        options.Scope.Add(
            "https://www.googleapis.com/auth/gmail.readonly"
        );

        // Offline access ώστε να μπορούμε αργότερα
        // να ανανεώνουμε το access token
        options.AccessType = "offline";
    });


// ----------------------------------------------------
// Authorization
// ----------------------------------------------------

builder.Services.AddAuthorization();


// ----------------------------------------------------
// Application Services
// ----------------------------------------------------

// Επικοινωνία με Gmail API
builder.Services.AddScoped<GoogleGmailService>();

// Ανίχνευση subscriptions από emails
builder.Services.AddScoped<SubscriptionDetectionService>();


// ----------------------------------------------------
// Build application
// ----------------------------------------------------

var app = builder.Build();


// ----------------------------------------------------
// Development
// ----------------------------------------------------

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


// ----------------------------------------------------
// Middleware
// ----------------------------------------------------

app.UseHttpsRedirection();

app.UseAuthentication();

app.UseAuthorization();


// ----------------------------------------------------
// Controllers
// ----------------------------------------------------

app.MapControllers();


// ----------------------------------------------------
// Run
// ----------------------------------------------------

app.Run();