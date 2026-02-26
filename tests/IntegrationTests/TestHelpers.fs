module TestHelpers

open System
open System.Threading.Tasks
open Application.Interfaces
open Domain
open Microsoft.AspNetCore.DataProtection
open Microsoft.AspNetCore.Identity
open Microsoft.AspNetCore.Identity.EntityFrameworkCore
open Microsoft.EntityFrameworkCore
open Microsoft.Extensions.DependencyInjection
open Persistence
open Testcontainers.PostgreSql

// ---------------------------------------------------------------------------
// Fake IUserAccessor for tests
// ---------------------------------------------------------------------------

type FakeUserAccessor(username: string) =
    interface IUserAccessor with
        member _.GetUsername() = username

// ---------------------------------------------------------------------------
// Database factory using Testcontainers
// ---------------------------------------------------------------------------

/// Builds and returns a started PostgreSQL container.
let buildPostgresContainer () =
    PostgreSqlBuilder("postgres:16-alpine")
        .Build()

/// Creates a DataContext wired to the given Testcontainers connection string,
/// creates the schema, and returns the context.
let buildDataContext (connectionString: string) =
    let options =
        DbContextOptionsBuilder<DataContext>()
            .UseNpgsql(connectionString)
            .Options
    let ctx = new DataContext(options)
    ctx.Database.EnsureCreated() |> ignore
    ctx

/// Creates an ASP.NET Identity UserManager for the given DataContext.
let buildUserManager (ctx: DataContext) =
    let services = ServiceCollection()
    services.AddLogging() |> ignore
    services.AddDataProtection() |> ignore
    services.AddDbContext<DataContext>(fun opts ->
        opts.UseNpgsql(ctx.Database.GetConnectionString()) |> ignore)
    |> ignore
    services
        .AddIdentityCore<User>(fun opts ->
            opts.Password.RequireNonAlphanumeric <- false
            opts.Password.RequireDigit <- false
            opts.User.RequireUniqueEmail <- true)
        .AddEntityFrameworkStores<DataContext>()
        .AddDefaultTokenProviders()
    |> ignore
    let sp = services.BuildServiceProvider()
    sp.GetRequiredService<UserManager<User>>()

/// Creates a User and saves it via Identity, returning the created user.
let createUser (userManager: UserManager<User>) username email displayName password =
    task {
        let user = User(UserName = username, Email = email, DisplayName = displayName)
        let! result = userManager.CreateAsync(user, password)
        if not result.Succeeded then
            let errors = result.Errors |> Seq.map (fun e -> e.Description) |> String.concat ", "
            failwith $"Failed to create test user '{username}': {errors}"
        return user
    }

/// Creates an Activity and wires it to the host user's attendance.
let createActivity (ctx: DataContext) (host: User) title date category city venue =
    task {
        let id = Guid.NewGuid()
        let activity: Activity =
            { Id = id
              Title = title
              Date = date
              Description = "Test description"
              Category = category
              City = city
              Venue = venue
              IsCancelled = false
              Attendees = ResizeArray()
              Comments = ResizeArray() }

        // Use only the foreign key (UserId) so EF does not attempt to re-INSERT the already-persisted user.
        let attendee: ActivityAttendee =
            { UserId = host.Id
              User = Unchecked.defaultof<User>
              ActivityId = id
              Activity = activity
              IsHost = true }
        activity.Attendees.Add(attendee)

        ctx.Activities.Add(activity) |> ignore
        let! _ = ctx.SaveChangesAsync()
        return activity
    }
