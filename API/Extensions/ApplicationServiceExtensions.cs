using Application.Activities;
using Application.Core;
using Application.Interfaces;
using FluentValidation;
using FluentValidation.AspNetCore;
using Infrastructure.Email;
using Infrastructure.Photos;
using Infrastructure.Security;
using MediatR;

namespace API.Extensions;

public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration config)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        services.AddDbContext<DataContext>(options =>
        {
            string? env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

            // Depending on if in development or production, use either SQLite
            // (development) or FlyIO PostgreSQL connection string (production).
            if (env == "Development") 
            {
                // Use SQLite for local development.
                string? connStr = config.GetConnectionString("DefaultConnection");
                options.UseSqlite(connStr);
            }
            else
            {
                // Use connection string provided at runtime by FlyIO.
                string? connUrl = Environment.GetEnvironmentVariable("DATABASE_URL");

                // Parse connection URL to connection string for Npgsql
                connUrl = connUrl!.Replace("postgres://", string.Empty);
                string pgUserPass = connUrl.Split("@")[0];
                string pgHostPortDb = connUrl.Split("@")[1];
                string pgHostPort = pgHostPortDb.Split("/")[0];
                string pgDb = pgHostPortDb.Split("/")[1];
                string pgUser = pgUserPass.Split(":")[0];
                string pgPass = pgUserPass.Split(":")[1];
                string pgHost = pgHostPort.Split(":")[0];
                string pgPort = pgHostPort.Split(":")[1];

                string updatedHost = pgHost.Replace("flycast", "internal");
                string connStr = $"Server={updatedHost};Port={pgPort};User Id={pgUser};Password={pgPass};Database={pgDb};";
                options.UseNpgsql(connStr);
            }
        });
        services.AddCors(Options => {
            Options.AddPolicy("CorsPolicy", policy => {
                policy
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials()
                    .WithExposedHeaders("WWW-Authenticate", "Pagination")
                    .WithOrigins("http://localhost:3000");
            });
        });
        services.AddMediatR(typeof(List.Handler));
        services.AddAutoMapper(typeof(MappingProfiles).Assembly);
        services.AddFluentValidationAutoValidation();
        services.AddValidatorsFromAssemblyContaining<Create>();
        services.AddHttpContextAccessor();
        services.Configure<CloudinarySettings>(config.GetSection("Cloudinary"));
        services.AddSignalR();
        
        services.AddScoped<IUserAccessor, UserAccessor>();
        services.AddScoped<IPhotoAccessor, PhotoAccessor>();
        services.AddScoped<EmailSender>();

        return services;
    }
}
