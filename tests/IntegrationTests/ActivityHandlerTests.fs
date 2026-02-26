module ActivityHandlerTests

open System
open System.Threading
open Application.Activities
open Application.Core
open Domain
open Microsoft.EntityFrameworkCore
open Persistence
open Testcontainers.PostgreSql
open TestHelpers
open Xunit
open FsUnit.Xunit

// ---------------------------------------------------------------------------
// Collection fixture: one PostgreSQL container shared across all activity tests
// ---------------------------------------------------------------------------

[<CollectionDefinition("ActivityDb")>]
type ActivityDbCollection() =
    interface ICollectionFixture<ActivityDbFixture>

and ActivityDbFixture() =
    let container = buildPostgresContainer()

    do
        container.StartAsync().GetAwaiter().GetResult()

    member _.ConnectionString = container.GetConnectionString()

    interface IDisposable with
        member _.Dispose() =
            container.StopAsync().GetAwaiter().GetResult()
            container.DisposeAsync().AsTask().GetAwaiter().GetResult()

// ---------------------------------------------------------------------------
// Create handler tests
// ---------------------------------------------------------------------------

[<Collection("ActivityDb")>]
type CreateHandlerTests(fixture: ActivityDbFixture) =

    let newCtx () = buildDataContext fixture.ConnectionString
    let newUm ctx = buildUserManager ctx

    [<Fact>]
    member _.``Create handler persists a new activity to the database`` () =
        task {
            use ctx = newCtx()
            let um = newUm ctx
            let! host = createUser um "createhost" "createhost@test.com" "Host" "Pa$$w0rd"

            let activity: Activity =
                { Id = Guid.NewGuid()
                  Title = "Integration Test Activity"
                  Date = DateTime.UtcNow.AddDays(7.0)
                  Description = "Created in integration test"
                  Category = "music"
                  City = "London"
                  Venue = "Wembley"
                  IsCancelled = false
                  Attendees = ResizeArray()
                  Comments = ResizeArray() }

            let handler = Create.Handler(ctx, FakeUserAccessor(host.UserName))
            let command: Create.Command = { Activity = activity }
            let! result = (handler :> MediatR.IRequestHandler<Create.Command, ServiceResponse<unit>>).Handle(command, CancellationToken.None)

            result.Success |> should equal true

            let! saved = ctx.Activities.FindAsync([| box activity.Id |])
            saved |> should not' (be Null)
            saved.Title |> should equal "Integration Test Activity"
        }

    [<Fact>]
    member _.``Create handler adds host as attendee with IsHost=true`` () =
        task {
            use ctx = newCtx()
            let um = newUm ctx
            let! host = createUser um "hostattend" "hostattend@test.com" "Host" "Pa$$w0rd"

            let activity: Activity =
                { Id = Guid.NewGuid()
                  Title = "Host Attendance Test"
                  Date = DateTime.UtcNow.AddDays(3.0)
                  Description = "desc"
                  Category = "drinks"
                  City = "Paris"
                  Venue = "Cafe"
                  IsCancelled = false
                  Attendees = ResizeArray()
                  Comments = ResizeArray() }

            let handler = Create.Handler(ctx, FakeUserAccessor(host.UserName))
            let! _ = (handler :> MediatR.IRequestHandler<Create.Command, ServiceResponse<unit>>).Handle({ Activity = activity }, CancellationToken.None)

            let! attendee =
                ctx.ActivityAttendees
                    .FirstOrDefaultAsync(fun aa -> aa.ActivityId = activity.Id && aa.IsHost)
            attendee |> should not' (be Null)
            attendee.UserId |> should equal host.Id
        }

    [<Fact>]
    member _.``Create handler fails when user does not exist`` () =
        task {
            use ctx = newCtx()

            let activity: Activity =
                { Id = Guid.NewGuid()
                  Title = "No-user Activity"
                  Date = DateTime.UtcNow.AddDays(1.0)
                  Description = "desc"
                  Category = "drinks"
                  City = "London"
                  Venue = "Pub"
                  IsCancelled = false
                  Attendees = ResizeArray()
                  Comments = ResizeArray() }

            let handler = Create.Handler(ctx, FakeUserAccessor("ghost"))
            let! result = (handler :> MediatR.IRequestHandler<Create.Command, ServiceResponse<unit>>).Handle({ Activity = activity }, CancellationToken.None)

            result.Success |> should equal false
            result.Error |> should equal "User not found."
        }

// ---------------------------------------------------------------------------
// Details handler tests
// ---------------------------------------------------------------------------

[<Collection("ActivityDb")>]
type DetailsHandlerTests(fixture: ActivityDbFixture) =

    let newCtx () = buildDataContext fixture.ConnectionString
    let newUm ctx = buildUserManager ctx

    [<Fact>]
    member _.``Details handler returns activity when found`` () =
        task {
            use ctx = newCtx()
            let um = newUm ctx
            let! host = createUser um "detailshost" "detailshost@test.com" "Host" "Pa$$w0rd"
            let! activity = createActivity ctx host "Details Test" (DateTime.UtcNow.AddDays(1.0)) "culture" "Berlin" "Museum"

            let handler = Details.Handler(ctx, FakeUserAccessor(host.UserName))
            let query = Details.Query()
            query.Id <- activity.Id
            let! result = (handler :> MediatR.IRequestHandler<Details.Query, ServiceResponse<ActivityDto>>).Handle(query, CancellationToken.None)

            result.Success |> should equal true
            result.Data.Title |> should equal "Details Test"
            result.Data.City |> should equal "Berlin"
        }

    [<Fact>]
    member _.``Details handler returns failure when activity not found`` () =
        task {
            use ctx = newCtx()

            let handler = Details.Handler(ctx, FakeUserAccessor("anyone"))
            let query = Details.Query()
            query.Id <- Guid.NewGuid()
            let! result = (handler :> MediatR.IRequestHandler<Details.Query, ServiceResponse<ActivityDto>>).Handle(query, CancellationToken.None)

            result.Success |> should equal false
            result.Error |> should equal "Activity not found."
        }

    [<Fact>]
    member _.``Details handler populates host username`` () =
        task {
            use ctx = newCtx()
            let um = newUm ctx
            let! host = createUser um "detailshost2" "detailshost2@test.com" "Host2" "Pa$$w0rd"
            let! activity = createActivity ctx host "Host Test" (DateTime.UtcNow.AddDays(2.0)) "food" "Rome" "Restaurant"

            let handler = Details.Handler(ctx, FakeUserAccessor(host.UserName))
            let query = Details.Query()
            query.Id <- activity.Id
            let! result = (handler :> MediatR.IRequestHandler<Details.Query, ServiceResponse<ActivityDto>>).Handle(query, CancellationToken.None)

            result.Data.HostUsername |> should equal host.UserName
        }

// ---------------------------------------------------------------------------
// Edit handler tests
// ---------------------------------------------------------------------------

[<Collection("ActivityDb")>]
type EditHandlerTests(fixture: ActivityDbFixture) =

    let newCtx () = buildDataContext fixture.ConnectionString
    let newUm ctx = buildUserManager ctx

    [<Fact>]
    member _.``Edit handler updates an existing activity`` () =
        task {
            use ctx = newCtx()
            let um = newUm ctx
            let! host = createUser um "edithost" "edithost@test.com" "EditHost" "Pa$$w0rd"
            let! activity = createActivity ctx host "Original Title" (DateTime.UtcNow.AddDays(5.0)) "music" "London" "O2"

            let updated: Activity =
                { Id = activity.Id
                  Title = "Updated Title"
                  Date = activity.Date
                  Description = "Updated desc"
                  Category = "culture"
                  City = "London"
                  Venue = "O2"
                  IsCancelled = false
                  Attendees = ResizeArray()
                  Comments = ResizeArray() }

            use editCtx = buildDataContext fixture.ConnectionString
            let mapper =
                let cfg = AutoMapper.MapperConfiguration(System.Action<AutoMapper.IMapperConfigurationExpression>(fun c -> c.CreateMap<Activity, Activity>() |> ignore))
                cfg.CreateMapper()
            let handler = Edit.Handler(editCtx, mapper)
            let! result = (handler :> MediatR.IRequestHandler<Edit.Command, ServiceResponse<unit>>).Handle({ Activity = updated }, CancellationToken.None)

            result.Success |> should equal true

            use verifyCtx = buildDataContext fixture.ConnectionString
            let! saved = verifyCtx.Activities.FindAsync([| box activity.Id |])
            saved.Title |> should equal "Updated Title"
            saved.Description |> should equal "Updated desc"
        }

    [<Fact>]
    member _.``Edit handler returns failure when activity not found`` () =
        task {
            use ctx = newCtx()
            let mapper =
                let cfg = AutoMapper.MapperConfiguration(System.Action<AutoMapper.IMapperConfigurationExpression>(fun c -> c.CreateMap<Activity, Activity>() |> ignore))
                cfg.CreateMapper()

            let handler = Edit.Handler(ctx, mapper)
            let notExisting: Activity =
                { Id = Guid.NewGuid()
                  Title = "Ghost"
                  Date = DateTime.UtcNow
                  Description = "desc"
                  Category = "drinks"
                  City = "London"
                  Venue = "Pub"
                  IsCancelled = false
                  Attendees = ResizeArray()
                  Comments = ResizeArray() }
            let! result = (handler :> MediatR.IRequestHandler<Edit.Command, ServiceResponse<unit>>).Handle({ Activity = notExisting }, CancellationToken.None)

            result.Success |> should equal false
            result.Error |> should equal "Activity not found."
        }

// ---------------------------------------------------------------------------
// Delete handler tests
// ---------------------------------------------------------------------------

[<Collection("ActivityDb")>]
type DeleteHandlerTests(fixture: ActivityDbFixture) =

    let newCtx () = buildDataContext fixture.ConnectionString
    let newUm ctx = buildUserManager ctx

    [<Fact>]
    member _.``Delete handler removes activity from the database`` () =
        task {
            use ctx = newCtx()
            let um = newUm ctx
            let! host = createUser um "deletehost" "deletehost@test.com" "DeleteHost" "Pa$$w0rd"
            let! activity = createActivity ctx host "To Delete" (DateTime.UtcNow.AddDays(1.0)) "drinks" "London" "Pub"

            use deleteCtx = buildDataContext fixture.ConnectionString
            let handler = Delete.Handler(deleteCtx)
            let cmd = Delete.Command()
            cmd.Id <- activity.Id
            let! result = (handler :> MediatR.IRequestHandler<Delete.Command, ServiceResponse<unit>>).Handle(cmd, CancellationToken.None)

            result.Success |> should equal true

            use verifyCtx = buildDataContext fixture.ConnectionString
            let! found = verifyCtx.Activities.FindAsync([| box activity.Id |])
            found |> should be Null
        }

    [<Fact>]
    member _.``Delete handler returns failure when activity not found`` () =
        task {
            use ctx = newCtx()

            let handler = Delete.Handler(ctx)
            let cmd = Delete.Command()
            cmd.Id <- Guid.NewGuid()
            let! result = (handler :> MediatR.IRequestHandler<Delete.Command, ServiceResponse<unit>>).Handle(cmd, CancellationToken.None)

            result.Success |> should equal false
            result.Error |> should equal "Activity not found."
        }

// ---------------------------------------------------------------------------
// UpdateAttendance handler tests
// ---------------------------------------------------------------------------

[<Collection("ActivityDb")>]
type UpdateAttendanceHandlerTests(fixture: ActivityDbFixture) =

    let newCtx () = buildDataContext fixture.ConnectionString
    let newUm ctx = buildUserManager ctx

    [<Fact>]
    member _.``UpdateAttendance adds attendee when not already attending`` () =
        task {
            use ctx = newCtx()
            let um = newUm ctx
            let! host = createUser um "attendhost" "attendhost@test.com" "AttendHost" "Pa$$w0rd"
            let! guest = createUser um "attendguest" "attendguest@test.com" "AttendGuest" "Pa$$w0rd"
            let! activity = createActivity ctx host "Attend Test" (DateTime.UtcNow.AddDays(2.0)) "music" "London" "Wembley"

            use attendCtx = buildDataContext fixture.ConnectionString
            let handler = UpdateAttendance.Handler(attendCtx, FakeUserAccessor(guest.UserName))
            let cmd = UpdateAttendance.Command()
            cmd.Id <- activity.Id
            let! result = (handler :> MediatR.IRequestHandler<UpdateAttendance.Command, ServiceResponse<unit>>).Handle(cmd, CancellationToken.None)

            result.Success |> should equal true

            use verifyCtx = buildDataContext fixture.ConnectionString
            let! attendee = verifyCtx.ActivityAttendees.FirstOrDefaultAsync(fun aa -> aa.ActivityId = activity.Id && aa.UserId = guest.Id)
            attendee |> should not' (be Null)
        }

    [<Fact>]
    member _.``UpdateAttendance removes attendee when already attending`` () =
        task {
            use ctx = newCtx()
            let um = newUm ctx
            let! host = createUser um "removehost" "removehost@test.com" "RemoveHost" "Pa$$w0rd"
            let! guest = createUser um "removeguest" "removeguest@test.com" "RemoveGuest" "Pa$$w0rd"
            let! activity = createActivity ctx host "Remove Attend Test" (DateTime.UtcNow.AddDays(2.0)) "food" "Paris" "Cafe"

            // First, add the guest as attendee
            use joinCtx = buildDataContext fixture.ConnectionString
            let joinHandler = UpdateAttendance.Handler(joinCtx, FakeUserAccessor(guest.UserName))
            let joinCmd = UpdateAttendance.Command()
            joinCmd.Id <- activity.Id
            let! _ = (joinHandler :> MediatR.IRequestHandler<UpdateAttendance.Command, ServiceResponse<unit>>).Handle(joinCmd, CancellationToken.None)

            // Now remove them
            use leaveCtx = buildDataContext fixture.ConnectionString
            let leaveHandler = UpdateAttendance.Handler(leaveCtx, FakeUserAccessor(guest.UserName))
            let leaveCmd = UpdateAttendance.Command()
            leaveCmd.Id <- activity.Id
            let! result = (leaveHandler :> MediatR.IRequestHandler<UpdateAttendance.Command, ServiceResponse<unit>>).Handle(leaveCmd, CancellationToken.None)

            result.Success |> should equal true

            use verifyCtx = buildDataContext fixture.ConnectionString
            let! attendee = verifyCtx.ActivityAttendees.FirstOrDefaultAsync(fun aa -> aa.ActivityId = activity.Id && aa.UserId = guest.Id)
            attendee |> should be Null
        }

    [<Fact>]
    member _.``UpdateAttendance toggles IsCancelled when host calls it`` () =
        task {
            use ctx = newCtx()
            let um = newUm ctx
            let! host = createUser um "cancelhost" "cancelhost@test.com" "CancelHost" "Pa$$w0rd"
            let! activity = createActivity ctx host "Cancel Test" (DateTime.UtcNow.AddDays(5.0)) "drinks" "London" "Pub"

            use toggleCtx = buildDataContext fixture.ConnectionString
            let handler = UpdateAttendance.Handler(toggleCtx, FakeUserAccessor(host.UserName))
            let cmd = UpdateAttendance.Command()
            cmd.Id <- activity.Id
            let! result = (handler :> MediatR.IRequestHandler<UpdateAttendance.Command, ServiceResponse<unit>>).Handle(cmd, CancellationToken.None)

            result.Success |> should equal true

            use verifyCtx = buildDataContext fixture.ConnectionString
            let! updated = verifyCtx.Activities.FindAsync([| box activity.Id |])
            updated.IsCancelled |> should equal true
        }

    [<Fact>]
    member _.``UpdateAttendance fails when activity not found`` () =
        task {
            use ctx = newCtx()
            let um = newUm ctx
            let! user = createUser um "attendnotfound" "attendnotfound@test.com" "NotFound" "Pa$$w0rd"

            let handler = UpdateAttendance.Handler(ctx, FakeUserAccessor(user.UserName))
            let cmd = UpdateAttendance.Command()
            cmd.Id <- Guid.NewGuid()
            let! result = (handler :> MediatR.IRequestHandler<UpdateAttendance.Command, ServiceResponse<unit>>).Handle(cmd, CancellationToken.None)

            result.Success |> should equal false
            result.Error |> should equal "Activity not found."
        }
