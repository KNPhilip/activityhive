module API.Extensions

open System
open System.Text
open System.Text.Json
open System.Threading.Tasks
open API
open Application.Activities
open Application.Core
open Application.Interfaces
open Domain
open FluentValidation
open FluentValidation.AspNetCore
open Infrastructure
open MediatR
open Microsoft.AspNetCore.Authentication.JwtBearer
open Microsoft.AspNetCore.Authorization
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Identity
open Microsoft.EntityFrameworkCore
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Microsoft.IdentityModel.Tokens
open Persistence

let addPaginationHeader
    (response: HttpResponse)
    currentPage
    itemsPerPage
    totalItems
    totalPages
    =
    let header =
        {| currentPage = currentPage
           itemsPerPage = itemsPerPage
           totalItems = totalItems
           totalPages = totalPages |}

    response.Headers.Append("Pagination", JsonSerializer.Serialize(header))

let addApplicationServices (services: IServiceCollection) (config: IConfiguration) =
    services.AddEndpointsApiExplorer() |> ignore
    services.AddSwaggerGen() |> ignore

    services.AddDbContext<DataContext>(fun options ->
        let dbUrl = Environment.GetEnvironmentVariable("DATABASE_URL")
        let defaultConn = config.GetConnectionString("DefaultConnection")

        if not (isNull dbUrl) && dbUrl <> "" then
            // Parse DATABASE_URL (Fly.io / Docker env var format)
            let url = dbUrl.Replace("postgres://", "")
            let parts = url.Split("@")
            let userPass = parts.[0]
            let hostPortDb = parts.[1]
            let hostPort = hostPortDb.Split("/").[0]
            let db = hostPortDb.Split("/").[1]
            let user = userPass.Split(":").[0]
            let pass = userPass.Split(":").[1]
            let host = hostPort.Split(":").[0]
            let port = hostPort.Split(":").[1]
            let updatedHost = host.Replace("flycast", "internal")
            let connStr = $"Server={updatedHost};Port={port};User Id={user};Password={pass};Database={db};"
            options.UseNpgsql(connStr) |> ignore
        elif not (isNull defaultConn) && defaultConn <> "" then
            // Use the connection string from appsettings (development PostgreSQL)
            options.UseNpgsql(defaultConn) |> ignore
        else
            // No database credentials provided — use SQLite in-memory
            options.UseSqlite("Data Source=activityhive;Mode=Memory;Cache=Shared") |> ignore)
    |> ignore

    services.AddCors(fun opts ->
        opts.AddPolicy(
            "CorsPolicy",
            fun policy ->
                policy
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials()
                    .WithExposedHeaders("WWW-Authenticate", "Pagination")
                    .WithOrigins("http://localhost:3000")
                |> ignore
        ))
    |> ignore

    services.AddMediatR(typeof<List.Handler>) |> ignore
    services.AddAutoMapper(typeof<MappingProfiles>.Assembly) |> ignore
    services.AddFluentValidationAutoValidation() |> ignore
    services.AddValidatorsFromAssemblyContaining<ActivityValidator>() |> ignore
    services.AddHttpContextAccessor() |> ignore
    services.Configure<CloudinarySettings>(config.GetSection("Cloudinary")) |> ignore
    services.AddSignalR() |> ignore
    services.AddScoped<IUserAccessor, UserAccessor>() |> ignore
    services.AddScoped<IPhotoAccessor, PhotoAccessor>() |> ignore
    services.AddScoped<EmailSender>() |> ignore
    services

let addIdentityServices (services: IServiceCollection) (config: IConfiguration) =
    services
        .AddIdentityCore<User>(fun options ->
            options.Password.RequireNonAlphanumeric <- false
            options.Password.RequireDigit <- false
            options.User.RequireUniqueEmail <- true)
        .AddEntityFrameworkStores<DataContext>()
        .AddSignInManager<SignInManager<User>>()
        .AddDefaultTokenProviders()
    |> ignore

    services
        .AddAuthentication()
        .AddJwtBearer(fun options ->
            options.TokenValidationParameters <-
                TokenValidationParameters(
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey =
                        SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["TokenKey"])),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                )

            options.Events <-
                JwtBearerEvents(
                    OnMessageReceived =
                        fun context ->
                            let accessToken = context.Request.Query["access_token"]
                            let path = context.HttpContext.Request.Path

                            if
                                not (String.IsNullOrEmpty(accessToken.ToString()))
                                && path.StartsWithSegments("/chat")
                            then
                                context.Token <- accessToken.ToString()

                            Task.CompletedTask
                ))
    |> ignore

    services
        .AddAuthorizationBuilder()
        .AddPolicy(
            "IsActivityHost",
            fun policy -> policy.Requirements.Add(IsHostRequirement())
        )
    |> ignore

    services.AddTransient<IAuthorizationHandler, IsHostRequirementHandler>() |> ignore
    services.AddScoped<IAuthService, AuthService>() |> ignore
    services
