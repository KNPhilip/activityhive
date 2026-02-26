module FollowerHandlerTests

open System.Threading
open Application.Core
open Application.Followers
open Domain
open Microsoft.EntityFrameworkCore
open TestHelpers
open Testcontainers.PostgreSql
open Xunit
open FsUnit.Xunit

[<CollectionDefinition("FollowerDb")>]
type FollowerDbCollection() =
    interface ICollectionFixture<FollowerDbFixture>

and FollowerDbFixture() =
    let container = buildPostgresContainer()

    do
        container.StartAsync().GetAwaiter().GetResult()

    member _.ConnectionString = container.GetConnectionString()

    interface System.IDisposable with
        member _.Dispose() =
            container.StopAsync().GetAwaiter().GetResult()
            container.DisposeAsync().AsTask().GetAwaiter().GetResult()

[<Collection("FollowerDb")>]
type FollowToggleTests(fixture: FollowerDbFixture) =

    let newCtx () = buildDataContext fixture.ConnectionString
    let newUm ctx = buildUserManager ctx

    [<Fact>]
    member _.``FollowToggle creates following when not yet following`` () =
        task {
            use ctx = newCtx()
            let um = newUm ctx
            let! observer = createUser um "ftobserver1" "ftobserver1@test.com" "Observer" "Pa$$w0rd"
            let! target = createUser um "fttarget1" "fttarget1@test.com" "Target" "Pa$$w0rd"

            let handler = FollowToggle.Handler(ctx, FakeUserAccessor(observer.UserName))
            let cmd = FollowToggle.Command()
            cmd.TargetUsername <- target.UserName
            let! result = (handler :> MediatR.IRequestHandler<FollowToggle.Command, ServiceResponse<unit>>).Handle(cmd, CancellationToken.None)

            result.Success |> should equal true

            use verifyCtx = buildDataContext fixture.ConnectionString
            let! following = verifyCtx.UserFollowings.FindAsync(observer.Id, target.Id)
            following |> should not' (be Null)
        }

    [<Fact>]
    member _.``FollowToggle removes following when already following`` () =
        task {
            use ctx = newCtx()
            let um = newUm ctx
            let! observer = createUser um "ftobserver2" "ftobserver2@test.com" "Observer2" "Pa$$w0rd"
            let! target = createUser um "fttarget2" "fttarget2@test.com" "Target2" "Pa$$w0rd"

            // First follow
            use followCtx = buildDataContext fixture.ConnectionString
            let followHandler = FollowToggle.Handler(followCtx, FakeUserAccessor(observer.UserName))
            let followCmd = FollowToggle.Command()
            followCmd.TargetUsername <- target.UserName
            let! _ = (followHandler :> MediatR.IRequestHandler<FollowToggle.Command, ServiceResponse<unit>>).Handle(followCmd, CancellationToken.None)

            // Then unfollow
            use unfollowCtx = buildDataContext fixture.ConnectionString
            let unfollowHandler = FollowToggle.Handler(unfollowCtx, FakeUserAccessor(observer.UserName))
            let unfollowCmd = FollowToggle.Command()
            unfollowCmd.TargetUsername <- target.UserName
            let! result = (unfollowHandler :> MediatR.IRequestHandler<FollowToggle.Command, ServiceResponse<unit>>).Handle(unfollowCmd, CancellationToken.None)

            result.Success |> should equal true

            use verifyCtx = buildDataContext fixture.ConnectionString
            let! following = verifyCtx.UserFollowings.FindAsync(observer.Id, target.Id)
            following |> should be Null
        }

    [<Fact>]
    member _.``FollowToggle fails when observer user does not exist`` () =
        task {
            use ctx = newCtx()
            let um = newUm ctx
            let! target = createUser um "fttarget3" "fttarget3@test.com" "Target3" "Pa$$w0rd"

            let handler = FollowToggle.Handler(ctx, FakeUserAccessor("ghostuser"))
            let cmd = FollowToggle.Command()
            cmd.TargetUsername <- target.UserName
            let! result = (handler :> MediatR.IRequestHandler<FollowToggle.Command, ServiceResponse<unit>>).Handle(cmd, CancellationToken.None)

            result.Success |> should equal false
            result.Error |> should equal "Observer user not found."
        }

    [<Fact>]
    member _.``FollowToggle fails when target user does not exist`` () =
        task {
            use ctx = newCtx()
            let um = newUm ctx
            let! observer = createUser um "ftobserver4" "ftobserver4@test.com" "Observer4" "Pa$$w0rd"

            let handler = FollowToggle.Handler(ctx, FakeUserAccessor(observer.UserName))
            let cmd = FollowToggle.Command()
            cmd.TargetUsername <- "ghosttarget"
            let! result = (handler :> MediatR.IRequestHandler<FollowToggle.Command, ServiceResponse<unit>>).Handle(cmd, CancellationToken.None)

            result.Success |> should equal false
            result.Error |> should equal "Target user not found."
        }

[<Collection("FollowerDb")>]
type ListFollowersTests(fixture: FollowerDbFixture) =

    let newCtx () = buildDataContext fixture.ConnectionString
    let newUm ctx = buildUserManager ctx

    [<Fact>]
    member _.``List with followers predicate returns users following the target`` () =
        task {
            use ctx = newCtx()
            let um = newUm ctx
            let! observer = createUser um "listobs1" "listobs1@test.com" "Observer1" "Pa$$w0rd"
            let! target = createUser um "listtgt1" "listtgt1@test.com" "Target1" "Pa$$w0rd"

            // Follow the target
            use followCtx = buildDataContext fixture.ConnectionString
            let followHandler = FollowToggle.Handler(followCtx, FakeUserAccessor(observer.UserName))
            let followCmd = FollowToggle.Command()
            followCmd.TargetUsername <- target.UserName
            let! _ = (followHandler :> MediatR.IRequestHandler<FollowToggle.Command, ServiceResponse<unit>>).Handle(followCmd, CancellationToken.None)

            use listCtx = buildDataContext fixture.ConnectionString
            let listHandler = List.Handler(listCtx, FakeUserAccessor(observer.UserName))
            let query = List.Query()
            query.Username <- target.UserName
            query.Predicate <- "followers"
            let! result = (listHandler :> MediatR.IRequestHandler<List.Query, ServiceResponse<Application.Profiles.UserProfile list>>).Handle(query, CancellationToken.None)

            result.Success |> should equal true
            result.Data |> Seq.exists (fun p -> p.Username = observer.UserName) |> should equal true
        }

    [<Fact>]
    member _.``List with following predicate returns users the observer follows`` () =
        task {
            use ctx = newCtx()
            let um = newUm ctx
            let! observer = createUser um "listobs2" "listobs2@test.com" "Observer2" "Pa$$w0rd"
            let! target = createUser um "listtgt2" "listtgt2@test.com" "Target2" "Pa$$w0rd"

            // Observer follows target
            use followCtx = buildDataContext fixture.ConnectionString
            let followHandler = FollowToggle.Handler(followCtx, FakeUserAccessor(observer.UserName))
            let followCmd = FollowToggle.Command()
            followCmd.TargetUsername <- target.UserName
            let! _ = (followHandler :> MediatR.IRequestHandler<FollowToggle.Command, ServiceResponse<unit>>).Handle(followCmd, CancellationToken.None)

            use listCtx = buildDataContext fixture.ConnectionString
            let listHandler = List.Handler(listCtx, FakeUserAccessor(observer.UserName))
            let query = List.Query()
            query.Username <- observer.UserName
            query.Predicate <- "following"
            let! result = (listHandler :> MediatR.IRequestHandler<List.Query, ServiceResponse<Application.Profiles.UserProfile list>>).Handle(query, CancellationToken.None)

            result.Success |> should equal true
            result.Data |> Seq.exists (fun p -> p.Username = target.UserName) |> should equal true
        }
