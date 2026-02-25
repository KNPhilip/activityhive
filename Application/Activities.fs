namespace Application.Activities

open System
open System.Linq
open System.Collections.Generic
open Application.Core
open Application.Interfaces
open Domain
open Persistence
open AutoMapper
open FluentValidation
open MediatR
open Microsoft.EntityFrameworkCore

// --- DTOs ---

[<CLIMutable>]
type AttendeeDto =
    { mutable Username: string
      mutable DisplayName: string
      mutable Bio: string
      mutable Image: string
      mutable Following: bool
      mutable FollowersCount: int
      mutable FollowingCount: int }

[<CLIMutable>]
type ActivityDto =
    { mutable Id: Guid
      mutable Title: string
      mutable Date: DateTime
      mutable Description: string
      mutable Category: string
      mutable City: string
      mutable Venue: string
      mutable HostUsername: string
      mutable IsCancelled: bool
      mutable Attendees: ICollection<AttendeeDto> }

// --- Params ---

type ActivityParams() =
    inherit PagingParams()
    member val IsGoing: bool = false with get, set
    member val IsHost: bool = false with get, set
    member val StartDate: DateTime = DateTime.UtcNow with get, set

// --- Validator ---

type ActivityValidator() =
    inherit AbstractValidator<Activity>()
    do
        base.RuleFor(fun a -> a.Title).NotEmpty() |> ignore
        base.RuleFor(fun a -> a.Description).NotEmpty() |> ignore
        base.RuleFor(fun a -> a.Category).NotEmpty() |> ignore
        base.RuleFor(fun a -> a.City).NotEmpty() |> ignore
        base.RuleFor(fun a -> a.Venue).NotEmpty() |> ignore
        base.RuleFor(fun a -> a.Date).NotEqual(DateTime.MinValue) |> ignore

// --- Private mapping helpers ---

[<AutoOpen>]
module private ActivityMapping =

    let mainPhoto (photos: System.Collections.Generic.ICollection<Photo>) =
        photos |> Seq.tryFind (fun p -> p.IsMain) |> Option.map (fun p -> p.Url) |> Option.defaultValue ""

    let mapAttendeeToDto (myFollowingIds: Set<string>) (aa: ActivityAttendee) : AttendeeDto =
        { Username      = if isNull (box aa.User) then "" else aa.User.UserName
          DisplayName   = if isNull (box aa.User) then "" else aa.User.DisplayName
          Bio           = if isNull (box aa.User) then null else aa.User.Bio
          Image         = if isNull (box aa.User) then "" else mainPhoto aa.User.Photos
          Following     = myFollowingIds.Contains(if isNull (box aa.User) then "" else aa.UserId)
          FollowersCount = if isNull (box aa.User) then 0 else aa.User.Followers |> Seq.length
          FollowingCount = if isNull (box aa.User) then 0 else aa.User.Followings |> Seq.length }

    let mapActivityToDto (myFollowingIds: Set<string>) (a: Activity) : ActivityDto =
        let host = a.Attendees |> Seq.tryFind (fun x -> x.IsHost)
        { Id          = a.Id
          Title       = a.Title
          Date        = a.Date
          Description = a.Description
          Category    = a.Category
          City        = a.City
          Venue       = a.Venue
          HostUsername =
              host
              |> Option.bind (fun h -> if isNull (box h.User) then None else Some h.User.UserName)
              |> Option.defaultValue ""
          IsCancelled = a.IsCancelled
          Attendees   = a.Attendees |> Seq.map (mapAttendeeToDto myFollowingIds) |> ResizeArray }

// --- Modules with Query/Command types and Handlers ---

module List =
    type Query() =
        member val Params: ActivityParams = ActivityParams() with get, set
        interface IRequest<ServiceResponse<PagedList<ActivityDto>>>

    type Handler(context: DataContext, userAccessor: IUserAccessor) =
        interface IRequestHandler<Query, ServiceResponse<PagedList<ActivityDto>>> with
            member _.Handle(request, _ct) =
                task {
                    let username = userAccessor.GetUsername()

                    // Build SQL-translatable filter query (no Includes yet)
                    let baseQuery =
                        context.Activities
                            .Where(fun d -> d.Date >= request.Params.StartDate)
                            .OrderBy(fun a -> a.Date)
                            :> IQueryable<Activity>

                    let filteredQuery =
                        if request.Params.IsGoing && not request.Params.IsHost then
                            baseQuery.Where(fun a -> a.Attendees.Any(fun aa -> aa.User.UserName = username))
                        elif request.Params.IsHost && not request.Params.IsGoing then
                            baseQuery.Where(fun a -> a.Attendees.Any(fun aa -> aa.IsHost && aa.User.UserName = username))
                        else
                            baseQuery

                    let! totalCount = filteredQuery.CountAsync()

                    let! activities =
                        (filteredQuery
                            .Include(fun a -> a.Attendees :> IEnumerable<ActivityAttendee>)
                            .ThenInclude(fun (aa: ActivityAttendee) -> aa.User)
                            .ThenInclude(fun (u: User) -> u.Photos :> IEnumerable<Photo>)
                            .Include(fun a -> a.Attendees :> IEnumerable<ActivityAttendee>)
                            .ThenInclude(fun (aa: ActivityAttendee) -> aa.User)
                            .ThenInclude(fun (u: User) -> u.Followers :> IEnumerable<UserFollowing>)
                            .Include(fun a -> a.Attendees :> IEnumerable<ActivityAttendee>)
                            .ThenInclude(fun (aa: ActivityAttendee) -> aa.User)
                            .ThenInclude(fun (u: User) -> u.Followings :> IEnumerable<UserFollowing>))
                            .Skip((request.Params.PageNumber - 1) * request.Params.PageSize)
                            .Take(request.Params.PageSize)
                            .ToListAsync()

                    let! myFollowingIds =
                        context.UserFollowings
                            .Where(fun f -> f.Observer.UserName = username)
                            .Select(fun f -> f.TargetId)
                            .ToListAsync()
                    let followingSet = Set.ofSeq myFollowingIds

                    let dtos = activities |> Seq.map (mapActivityToDto followingSet) |> Seq.toList
                    let pagedList = PagedList<ActivityDto>(dtos, totalCount, request.Params.PageNumber, request.Params.PageSize)
                    return ServiceResponse.success pagedList
                }

module Details =
    type Query() =
        member val Id: Guid = Guid.Empty with get, set
        interface IRequest<ServiceResponse<ActivityDto>>

    type Handler(context: DataContext, userAccessor: IUserAccessor) =
        interface IRequestHandler<Query, ServiceResponse<ActivityDto>> with
            member _.Handle(request, _ct) =
                task {
                    let username = userAccessor.GetUsername()

                    let! activity =
                        (context.Activities
                            .Include(fun a -> a.Attendees :> IEnumerable<ActivityAttendee>)
                            .ThenInclude(fun (aa: ActivityAttendee) -> aa.User)
                            .ThenInclude(fun (u: User) -> u.Photos :> IEnumerable<Photo>)
                            .Include(fun a -> a.Attendees :> IEnumerable<ActivityAttendee>)
                            .ThenInclude(fun (aa: ActivityAttendee) -> aa.User)
                            .ThenInclude(fun (u: User) -> u.Followers :> IEnumerable<UserFollowing>)
                            .Include(fun a -> a.Attendees :> IEnumerable<ActivityAttendee>)
                            .ThenInclude(fun (aa: ActivityAttendee) -> aa.User)
                            .ThenInclude(fun (u: User) -> u.Followings :> IEnumerable<UserFollowing>))
                            .FirstOrDefaultAsync(fun x -> x.Id = request.Id)

                    if isNull (box activity) then
                        return ServiceResponse.failure "Activity not found."
                    else
                        let! myFollowingIds =
                            context.UserFollowings
                                .Where(fun f -> f.Observer.UserName = username)
                                .Select(fun f -> f.TargetId)
                                .ToListAsync()
                        let followingSet = Set.ofSeq myFollowingIds
                        return ServiceResponse.success (mapActivityToDto followingSet activity)
                }

module Create =
    [<CLIMutable>]
    type Command =
        { mutable Activity: Activity }
        interface IRequest<ServiceResponse<unit>>

    type Handler(context: DataContext, userAccessor: IUserAccessor) =
        interface IRequestHandler<Command, ServiceResponse<unit>> with
            member _.Handle(request, ct) =
                task {
                    let! user = context.Users.FirstOrDefaultAsync((fun u -> u.UserName = userAccessor.GetUsername()), ct)
                    let attendee: ActivityAttendee =
                        { UserId = user.Id
                          User = user
                          ActivityId = request.Activity.Id
                          Activity = request.Activity
                          IsHost = true }
                    request.Activity.Attendees.Add(attendee)
                    context.Activities.Add(request.Activity) |> ignore
                    let! result = context.SaveChangesAsync(ct)
                    return
                        if result > 0 then ServiceResponse.success ()
                        else ServiceResponse.failure "Failed to create activity."
                }

module Edit =
    [<CLIMutable>]
    type Command =
        { mutable Activity: Activity }
        interface IRequest<ServiceResponse<unit>>

    type Handler(context: DataContext, mapper: IMapper) =
        interface IRequestHandler<Command, ServiceResponse<unit>> with
            member _.Handle(request, ct) =
                task {
                    let! activity = context.Activities.FindAsync([| box request.Activity.Id |], ct)
                    if isNull (box activity) then
                        return ServiceResponse.failure "Activity not found."
                    else
                        mapper.Map(request.Activity, activity) |> ignore
                        let! result = context.SaveChangesAsync(ct)
                        return
                            if result > 0 then ServiceResponse.success ()
                            else ServiceResponse.failure "Failed to update activity."
                }

module Delete =
    type Command() =
        member val Id: Guid = Guid.Empty with get, set
        interface IRequest<ServiceResponse<unit>>

    type Handler(context: DataContext) =
        interface IRequestHandler<Command, ServiceResponse<unit>> with
            member _.Handle(request, ct) =
                task {
                    let! activity = context.Activities.FindAsync([| box request.Id |], ct)
                    if isNull (box activity) then
                        return ServiceResponse.failure "Activity not found."
                    else
                        context.Remove(activity) |> ignore
                        let! result = context.SaveChangesAsync(ct)
                        return
                            if result > 0 then ServiceResponse.success ()
                            else ServiceResponse.failure "Failed to delete activity."
                }

module UpdateAttendance =
    type Command() =
        member val Id: Guid = Guid.Empty with get, set
        interface IRequest<ServiceResponse<unit>>

    type Handler(context: DataContext, userAccessor: IUserAccessor) =
        interface IRequestHandler<Command, ServiceResponse<unit>> with
            member _.Handle(request, _ct) =
                task {
                    let! activity =
                        context.Activities
                            .Include(fun a -> a.Attendees :> IEnumerable<ActivityAttendee>)
                            .ThenInclude(fun (aa: ActivityAttendee) -> aa.User :> obj)
                            .FirstOrDefaultAsync(fun x -> x.Id = request.Id)

                    if isNull (box activity) then
                        return ServiceResponse.failure "Activity not found."
                    else
                        let! user = context.Users.FirstOrDefaultAsync(fun x -> x.UserName = userAccessor.GetUsername())
                        if isNull (box user) then
                            return ServiceResponse.failure "User not found."
                        else
                            let hostUsername =
                                activity.Attendees
                                |> Seq.tryFind (fun x -> x.IsHost)
                                |> Option.map (fun x -> x.User.UserName)
                            let attendant = activity.Attendees |> Seq.tryFind (fun x -> x.User.UserName = user.UserName)
                            match attendant, hostUsername with
                            | Some _, Some h when h = user.UserName ->
                                activity.IsCancelled <- not activity.IsCancelled
                            | Some a, _ ->
                                activity.Attendees.Remove(a) |> ignore
                            | None, _ ->
                                let newAttendee: ActivityAttendee =
                                    { UserId = user.Id; User = user
                                      ActivityId = activity.Id; Activity = activity
                                      IsHost = false }
                                activity.Attendees.Add(newAttendee)
                            let! result = context.SaveChangesAsync()
                            return
                                if result > 0 then ServiceResponse.success ()
                                else ServiceResponse.failure "Problem updating attendance."
                }
