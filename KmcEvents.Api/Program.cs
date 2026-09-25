using KmcEvents.Api.Data;
using KmcEvents.Api.Models;
using KmcEvents.Api.Services;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

using QuestPDF.Infrastructure;

using System.Text;

var builder = WebApplication.CreateBuilder(args);


// ============================================================
// QUESTPDF LICENSE
// ============================================================

QuestPDF.Settings.License =
    LicenseType.Community;


// ============================================================
// CONTROLLERS
// ============================================================

builder.Services.AddControllers();


// ============================================================
// SWAGGER
// ============================================================

builder.Services.AddSwaggerGen(
    options =>
    {
        options.SwaggerDoc(
            "v1",
            new OpenApiInfo
            {
                Title = "KmcEvents.Api",
                Version = "v1"
            }
        );

        options.AddSecurityDefinition(
            "Bearer",
            new OpenApiSecurityScheme
            {
                Name = "Authorization",

                Type = SecuritySchemeType.Http,

                Scheme = "bearer",

                BearerFormat = "JWT",

                In = ParameterLocation.Header,

                Description =
                    "Enter your JWT token."
            }
        );

        options.AddSecurityRequirement(
            new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference =
                            new OpenApiReference
                            {
                                Type =
                                    ReferenceType.SecurityScheme,

                                Id =
                                    "Bearer"
                            }
                    },

                    Array.Empty<string>()
                }
            }
        );
    }
);


// ============================================================
// DATABASE
// ============================================================

builder.Services.AddDbContext<AppDbContext>(
    options =>
    {
        options.UseSqlServer(
            builder.Configuration
                .GetConnectionString(
                    "DefaultConnection"
                )
        );
    }
);


// ============================================================
// PASSWORD HASHER
// ============================================================

builder.Services.AddScoped<
    IPasswordHasher<AppUser>,
    PasswordHasher<AppUser>
>();


// ============================================================
// APPLICATION SERVICES
// ============================================================

builder.Services.AddScoped<JwtTokenService>();

builder.Services.AddScoped<
    IEmailService,
    EmailService
>();

builder.Services.AddSingleton<QrCodeService>();

builder.Services.AddScoped<AdminReportPdfService>();


// ============================================================
// JWT AUTHENTICATION
// ============================================================

var key =
    Encoding.UTF8.GetBytes(
        builder.Configuration["Jwt:Key"]!
    );

builder.Services
    .AddAuthentication(
        JwtBearerDefaults.AuthenticationScheme
    )
    .AddJwtBearer(
        options =>
        {
            options.TokenValidationParameters =
                new TokenValidationParameters
                {
                    ValidateIssuer = true,

                    ValidateAudience = true,

                    ValidateLifetime = true,

                    ValidateIssuerSigningKey = true,

                    ValidIssuer =
                        builder.Configuration[
                            "Jwt:Issuer"
                        ],

                    ValidAudience =
                        builder.Configuration[
                            "Jwt:Audience"
                        ],

                    IssuerSigningKey =
                        new SymmetricSecurityKey(
                            key
                        ),

                    ClockSkew =
                        TimeSpan.Zero
                };
        }
    );


// ============================================================
// AUTHORIZATION
// ============================================================

builder.Services.AddAuthorization();


// ============================================================
// CORS
// Allows MVC Client to communicate with API.
// ============================================================

builder.Services.AddCors(
    options =>
    {
        options.AddPolicy(
            "client",
            policy =>
            {
                policy
                    .WithOrigins(
                        "https://localhost:7101",
                        "http://localhost:5101"
                    )
                    .AllowAnyHeader()
                    .AllowAnyMethod();
            }
        );
    }
);


// ============================================================
// BUILD APPLICATION
// ============================================================

var app =
    builder.Build();


// ============================================================
// DEVELOPMENT
// ============================================================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();

    app.UseSwaggerUI();
}


// ============================================================
// HTTP PIPELINE
// ============================================================

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseCors(
    "client"
);

app.UseAuthentication();

app.UseAuthorization();


// ============================================================
// API CONTROLLERS
// ============================================================

app.MapControllers();


// ============================================================
// DATABASE SEEDING
// ============================================================

await DbSeeder.SeedAsync(
    app.Services
);


// ============================================================
// RUN APPLICATION
// ============================================================

app.Run();