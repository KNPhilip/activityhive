global using Microsoft.EntityFrameworkCore;
global using Persistence;
global using Domain;
global using Microsoft.AspNetCore.Mvc;
using API.Extensions;
using API.Middleware;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using API.SignalR;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers(options => {
    AuthorizationPolicy policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser().Build();
    options.Filters.Add(new AuthorizeFilter(policy));
});
builder.Services.AddApplicationServices(builder.Configuration);
builder.Services.AddIdentityServices(builder.Configuration);

WebApplication app = builder.Build();

// Configure the HTTP request pipeline.
app.UseMiddleware<ExceptionMiddleware>();

// Referrer Policy Header - Controls included information on navigation.
app.UseReferrerPolicy(options => options.SameOrigin());
// X-Content-Type-Options Header - Prevents MIME-sniffing of the content type.
app.UseXContentTypeOptions();
// X-Frame-Options Header - Defends against attacks like clickjacking by banning framing on the site.
app.UseXfo(options => options.Deny());
// X-Xss Protection Header (Old) - Protection from XSS attacks by analyzing the page and blocking seemingly malicious stuff.
app.UseXXssProtection(options => options.EnabledWithBlockMode());
// Content-Security-Policy Header - Whitelists certain content and prevents other malicious assets (New XSS Protection).
app.UseCsp(options => options
    .BlockAllMixedContent()
    .StyleSources(s => s.Self()
        .CustomSources("https://fonts.googleapis.com", "sha256-DpOoqibK/BsYhobWHnU38Pyzt5SjDZuR/mFsAiVN7kk="))
    .FontSources(s => s.Self()
        .CustomSources("https://fonts.gstatic.com", "data:"))
    .FormActions(s => s.Self())
    // Frame Ancestors makes X-Frame-Options absolete.
    .FrameAncestors(s => s.Self())
    .ImageSources(s => s.Self()
        .CustomSources("blob:", "data:", "https://res.cloudinary.com", "https://platform-lookaside.fbsbx.com"))
    .ScriptSources(s => s.Self()
        .CustomSources("https://connect.facebook.net"))
);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
else
{
    // HTTP Strict-Transport-Security Header:
    // Strengthens implementation of TLS by getting the User Agent to enforce the use of HTTPS
    app.Use(async (context, next) => 
    {
        context.Response.Headers.Add("Strict-Transport-Security", "max-age=31536000; includeSubDomains; preload");
        await next.Invoke();
    });
}

app.UseCors("CorsPolicy");

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapHub<ChatHub>("/chat");
app.MapFallbackToController("Index", "Fallback");

using IServiceScope scope = app.Services.CreateScope();
IServiceProvider services = scope.ServiceProvider;

try 
{
    DataContext context = services.GetRequiredService<DataContext>();
    UserManager<User> userManager = services.GetRequiredService<UserManager<User>>();
    if (app.Environment.IsDevelopment())
    {
        await context.Database.EnsureCreatedAsync();
    }
    else
    {
        await context.Database.MigrateAsync();
    }
    await Seed.SeedData(context, userManager);
}
catch(Exception e) 
{
    ILogger<Program> logger = services.GetRequiredService<ILogger<Program>>();
    logger.LogError(e, "An error occured during migration");
}

app.Run();
