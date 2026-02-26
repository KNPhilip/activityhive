module CommentHandlerTests

open System
open System.Threading
open Application.Comments
open Application.Core
open Domain
open TestHelpers
open Testcontainers.PostgreSql
open Xunit
open FsUnit.Xunit

[<CollectionDefinition("CommentDb")>]
type CommentDbCollection() =
    interface ICollectionFixture<CommentDbFixture>

and CommentDbFixture() =
    let container = buildPostgresContainer()

    do
        container.StartAsync().GetAwaiter().GetResult()

    member _.ConnectionString = container.GetConnectionString()

    interface IDisposable with
        member _.Dispose() =
            container.StopAsync().GetAwaiter().GetResult()
            container.DisposeAsync().AsTask().GetAwaiter().GetResult()

[<Collection("CommentDb")>]
type CommentCreateHandlerTests(fixture: CommentDbFixture) =

    let newCtx () = buildDataContext fixture.ConnectionString
    let newUm ctx = buildUserManager ctx

    [<Fact>]
    member _.``Comment create adds a comment to the activity`` () =
        task {
            use ctx = newCtx()
            let um = newUm ctx
            let! host = createUser um "cmnthost1" "cmnthost1@test.com" "CommentHost" "Pa$$w0rd"
            let! activity = createActivity ctx host "Comment Activity" (DateTime.UtcNow.AddDays(3.0)) "culture" "London" "Museum"

            use commentCtx = buildDataContext fixture.ConnectionString
            let handler = Create.Handler(commentCtx, FakeUserAccessor(host.UserName))
            let cmd: Create.Command = { Body = "Great activity!"; ActivityId = activity.Id }
            let! result = (handler :> MediatR.IRequestHandler<Create.Command, ServiceResponse<CommentDto>>).Handle(cmd, CancellationToken.None)

            result.Success |> should equal true
            result.Data.Body |> should equal "Great activity!"
            result.Data.Username |> should equal host.UserName
        }

    [<Fact>]
    member _.``Comment create fails when activity not found`` () =
        task {
            use ctx = newCtx()
            let um = newUm ctx
            let! user = createUser um "cmntnoact1" "cmntnoact1@test.com" "NoActivity" "Pa$$w0rd"

            let handler = Create.Handler(ctx, FakeUserAccessor(user.UserName))
            let cmd: Create.Command = { Body = "Hello"; ActivityId = Guid.NewGuid() }
            let! result = (handler :> MediatR.IRequestHandler<Create.Command, ServiceResponse<CommentDto>>).Handle(cmd, CancellationToken.None)

            result.Success |> should equal false
            result.Error |> should equal "Activity not found."
        }

[<Collection("CommentDb")>]
type CommentListHandlerTests(fixture: CommentDbFixture) =

    let newCtx () = buildDataContext fixture.ConnectionString
    let newUm ctx = buildUserManager ctx

    [<Fact>]
    member _.``Comment list returns comments for an activity`` () =
        task {
            use ctx = newCtx()
            let um = newUm ctx
            let! host = createUser um "cmntlisthost1" "cmntlisthost1@test.com" "ListHost" "Pa$$w0rd"
            let! activity = createActivity ctx host "List Comments Activity" (DateTime.UtcNow.AddDays(4.0)) "music" "London" "O2"

            // Add two comments
            use c1Ctx = buildDataContext fixture.ConnectionString
            let c1Handler = Create.Handler(c1Ctx, FakeUserAccessor(host.UserName))
            let! _ = (c1Handler :> MediatR.IRequestHandler<Create.Command, ServiceResponse<CommentDto>>).Handle({ Body = "First comment"; ActivityId = activity.Id }, CancellationToken.None)

            use c2Ctx = buildDataContext fixture.ConnectionString
            let c2Handler = Create.Handler(c2Ctx, FakeUserAccessor(host.UserName))
            let! _ = (c2Handler :> MediatR.IRequestHandler<Create.Command, ServiceResponse<CommentDto>>).Handle({ Body = "Second comment"; ActivityId = activity.Id }, CancellationToken.None)

            use listCtx = buildDataContext fixture.ConnectionString
            let listHandler = List.Handler(listCtx)
            let query = List.Query()
            query.ActivityId <- activity.Id
            let! result = (listHandler :> MediatR.IRequestHandler<List.Query, ServiceResponse<CommentDto list>>).Handle(query, CancellationToken.None)

            result.Success |> should equal true
            result.Data |> List.length |> should equal 2
        }

    [<Fact>]
    member _.``Comment list returns empty for activity with no comments`` () =
        task {
            use ctx = newCtx()
            let um = newUm ctx
            let! host = createUser um "cmntlisthost2" "cmntlisthost2@test.com" "ListHost2" "Pa$$w0rd"
            let! activity = createActivity ctx host "Empty Comments Activity" (DateTime.UtcNow.AddDays(5.0)) "drinks" "London" "Pub"

            use listCtx = buildDataContext fixture.ConnectionString
            let listHandler = List.Handler(listCtx)
            let query = List.Query()
            query.ActivityId <- activity.Id
            let! result = (listHandler :> MediatR.IRequestHandler<List.Query, ServiceResponse<CommentDto list>>).Handle(query, CancellationToken.None)

            result.Success |> should equal true
            result.Data |> List.isEmpty |> should equal true
        }

    [<Fact>]
    member _.``Comment list includes author display name`` () =
        task {
            use ctx = newCtx()
            let um = newUm ctx
            let! host = createUser um "cmntlisthost3" "cmntlisthost3@test.com" "DisplayHost" "Pa$$w0rd"
            let! activity = createActivity ctx host "Display Name Activity" (DateTime.UtcNow.AddDays(6.0)) "travel" "Berlin" "All"

            use addCtx = buildDataContext fixture.ConnectionString
            let addHandler = Create.Handler(addCtx, FakeUserAccessor(host.UserName))
            let! _ = (addHandler :> MediatR.IRequestHandler<Create.Command, ServiceResponse<CommentDto>>).Handle({ Body = "Nice!"; ActivityId = activity.Id }, CancellationToken.None)

            use listCtx = buildDataContext fixture.ConnectionString
            let listHandler = List.Handler(listCtx)
            let query = List.Query()
            query.ActivityId <- activity.Id
            let! result = (listHandler :> MediatR.IRequestHandler<List.Query, ServiceResponse<CommentDto list>>).Handle(query, CancellationToken.None)

            let comment = result.Data |> List.head
            comment.DisplayName |> should equal "DisplayHost"
            comment.Username |> should equal host.UserName
        }
