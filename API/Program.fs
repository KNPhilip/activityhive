module Program

open System
open API
open API.Extensions
open API.SignalR
open Domain
open Microsoft.AspNetCore.Authorization
open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Identity
open Microsoft.AspNetCore.Mvc.Authorization
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging
open Persistence

[<EntryPoint>]
let main argv =
    let builder = WebApplication.CreateBuilder(argv)

    builder.Services.AddControllers(fun options ->
        let policy = AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()
        options.Filters.Add(AuthorizeFilter(policy)))
    |> ignore

    addApplicationServices builder.Services builder.Configuration |> ignore
    addIdentityServices builder.Services builder.Configuration |> ignore

    let app = builder.Build()

    app.UseMiddleware<ExceptionMiddleware>() |> ignore

    app.UseReferrerPolicy(fun options -> options.SameOrigin()) |> ignore
    app.UseXContentTypeOptions() |> ignore
    app.UseXfo(fun options -> options.Deny()) |> ignore
    app.UseXXssProtection(fun options -> options.EnabledWithBlockMode()) |> ignore

    app.UseCsp(fun options ->
        options
            .BlockAllMixedContent()
            .StyleSources(fun s ->
                s
                    .Self()
                    .CustomSources(
                        "https://fonts.googleapis.com",
                        "sha256-DpOoqibK/BsYhobWHnU38Pyzt5SjDZuR/mFsAiVN7kk="
                    )
                |> ignore)
            .FontSources(fun s ->
                s.Self().CustomSources("https://fonts.gstatic.com", "data:") |> ignore)
            .FormActions(fun s -> s.Self() |> ignore)
            .FrameAncestors(fun s -> s.Self() |> ignore)
            .ImageSources(fun s ->
                s
                    .Self()
                    .CustomSources(
                        "blob:",
                        "data:",
                        "https://res.cloudinary.com",
                        "https://platform-lookaside.fbsbx.com"
                    )
                |> ignore)
            .ScriptSources(fun s ->
                s.Self().CustomSources("https://connect.facebook.net") |> ignore)
        |> ignore)
    |> ignore

    if app.Environment.IsDevelopment() then
        app.UseSwagger() |> ignore
        app.UseSwaggerUI() |> ignore
    else
        app.Use(fun (context: Microsoft.AspNetCore.Http.HttpContext) (next: Func<System.Threading.Tasks.Task>) ->
            context.Response.Headers["Strict-Transport-Security"] <-
                "max-age=31536000; includeSubDomains; preload"

            next.Invoke())
        |> ignore

    app.UseCors("CorsPolicy") |> ignore
    app.UseHttpsRedirection() |> ignore
    app.UseAuthentication() |> ignore
    app.UseAuthorization() |> ignore
    app.UseDefaultFiles() |> ignore
    app.UseStaticFiles() |> ignore

    app.MapControllers() |> ignore
    app.MapHub<ChatHub>("/chat") |> ignore
    app.MapFallbackToController("Index", "Fallback") |> ignore

    task {
        use scope = app.Services.CreateScope()
        let services = scope.ServiceProvider

        try
            let context = services.GetRequiredService<DataContext>()
            let userManager = services.GetRequiredService<UserManager<User>>()
            let! _ = context.Database.EnsureCreatedAsync()
            do! Seed.seedData context userManager
        with ex ->
            let logger =
                services
                    .GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Startup")

            logger.LogError(ex, "An error occurred during migration")
    }
    |> fun t -> t.GetAwaiter().GetResult()

    app.Run()
    0
