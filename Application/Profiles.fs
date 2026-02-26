namespace Application.Profiles

open System
open System.Linq
open System.Collections.Generic
open Application.Core
open Application.Interfaces
open Domain
open Persistence
open MediatR
open Microsoft.EntityFrameworkCore

[<CLIMutable>]
type UserActivityDto =
    { mutable Id: Guid
      mutable Title: string
      mutable Date: DateTime
      mutable Category: string
      mutable HostUsername: string }

[<CLIMutable>]
type UserProfile =
    { mutable Username: string
      mutable DisplayName: string
      mutable Bio: string
      mutable Image: string
      mutable Following: bool
      mutable FollowersCount: int
      mutable FollowingCount: int
      mutable Photos: ICollection<Photo> }

[<AutoOpen>]
module ProfileMapping =
    let mainPhoto (photos: ICollection<Photo>) =
        photos |> Seq.tryFind (fun p -> p.IsMain) |> Option.map (fun p -> p.Url) |> Option.defaultValue ""

    let mapUserToProfile (myFollowingIds: Set<string>) (user: User) : UserProfile =
        { Username      = user.UserName
          DisplayName   = user.DisplayName
          Bio           = user.Bio
          Image         = mainPhoto user.Photos
          Following     = myFollowingIds.Contains(user.Id)
          FollowersCount = user.Followers |> Seq.length
          FollowingCount = user.Followings |> Seq.length
          Photos        = user.Photos }

    let mapUserActivityDto (aa: ActivityAttendee) : UserActivityDto =
        { Id       = aa.Activity.Id
          Title    = aa.Activity.Title
          Date     = aa.Activity.Date
          Category = aa.Activity.Category
          HostUsername =
              aa.Activity.Attendees
              |> Seq.tryFind (fun x -> x.IsHost)
              |> Option.bind (fun x -> if isNull (box x.User) then None else Some x.User.UserName)
              |> Option.defaultValue "" }

module Details =
    type Query() =
        member val Username: string = "" with get, set
        interface IRequest<ServiceResponse<UserProfile>>

    type Handler(context: DataContext, userAccessor: IUserAccessor) =
        interface IRequestHandler<Query, ServiceResponse<UserProfile>> with
            member _.Handle(request, _ct) =
                task {
                    let username = userAccessor.GetUsername()

                    let! user =
                        context.Users
                            .Include(fun u -> u.Photos :> IEnumerable<Photo>)
                            .Include(fun u -> u.Followers :> IEnumerable<UserFollowing>)
                            .Include(fun u -> u.Followings :> IEnumerable<UserFollowing>)
                            .FirstOrDefaultAsync(fun u -> u.UserName = request.Username)

                    if isNull (box user) then
                        return ServiceResponse.failure "Profile not found."
                    else
                        let! myFollowingIds =
                            context.UserFollowings
                                .Where(fun f -> f.Observer.UserName = username)
                                .Select(fun f -> f.TargetId)
                                .ToListAsync()
                        let followingSet = Set.ofSeq myFollowingIds
                        return ServiceResponse.success (mapUserToProfile followingSet user)
                }

module Edit =
    [<CLIMutable>]
    type Command =
        { mutable DisplayName: string
          mutable Bio: string }
        interface IRequest<ServiceResponse<unit>>

    type Handler(context: DataContext, userAccessor: IUserAccessor) =
        interface IRequestHandler<Command, ServiceResponse<unit>> with
            member _.Handle(request, _ct) =
                task {
                    let! user = context.Users.FirstOrDefaultAsync(fun x -> x.UserName = userAccessor.GetUsername())
                    if isNull (box user) then
                        return ServiceResponse.failure "User not found."
                    else
                        if not (isNull request.Bio) then user.Bio <- request.Bio
                        if not (isNull request.DisplayName) then user.DisplayName <- request.DisplayName
                        context.Entry(user).State <- EntityState.Modified
                        let! success = context.SaveChangesAsync()
                        return
                            if success > 0 then ServiceResponse.success ()
                            else ServiceResponse.failure "Problem updating profile."
                }

module ListActivities =
    type Query() =
        member val Username: string = "" with get, set
        member val Predicate: string = "" with get, set
        interface IRequest<ServiceResponse<UserActivityDto list>>

    type Handler(context: DataContext) =
        interface IRequestHandler<Query, ServiceResponse<UserActivityDto list>> with
            member _.Handle(request, _ct) =
                task {
                    let mutable query =
                        context.ActivityAttendees
                            .Where(fun u -> u.User.UserName = request.Username)
                            .OrderBy(fun a -> a.Activity.Date)
                            :> IQueryable<ActivityAttendee>

                    query <-
                        match request.Predicate with
                        | "past"    -> query.Where(fun a -> a.Activity.Date <= DateTime.UtcNow)
                        | "hosting" -> query.Where(fun a -> a.IsHost)
                        | _         -> query.Where(fun a -> a.Activity.Date >= DateTime.UtcNow)

                    let! attendees =
                        query
                            .Include(fun aa -> aa.Activity)
                            .ThenInclude(fun (a: Activity) -> a.Attendees :> IEnumerable<ActivityAttendee>)
                            .ThenInclude(fun (aa: ActivityAttendee) -> aa.User)
                            .ToListAsync()

                    return ServiceResponse.success (attendees |> Seq.map mapUserActivityDto |> Seq.toList)
                }
