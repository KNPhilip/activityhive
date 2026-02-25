namespace Application.Activities

open System
open System.Linq
open System.Collections.Generic
open Application.Core
open Application.Interfaces
open Domain
open Persistence
open AutoMapper
open AutoMapper.QueryableExtensions
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

// --- Modules with Query/Command types and Handlers ---

module List =
    type Query() =
        member val Params: ActivityParams = ActivityParams() with get, set
        interface IRequest<ServiceResponse<PagedList<ActivityDto>>>

    type Handler(context: DataContext, mapper: IMapper, userAccessor: IUserAccessor) =
        interface IRequestHandler<Query, ServiceResponse<PagedList<ActivityDto>>> with
            member _.Handle(request, _ct) =
                task {
                    let username = userAccessor.GetUsername()
                    let mutable query =
                        context.Activities
                            .Where(fun d -> d.Date >= request.Params.StartDate)
                            .OrderBy(fun a -> a.Date)
                            .ProjectTo<ActivityDto>(mapper.ConfigurationProvider, dict ["currentUsername", box username])
                            .AsQueryable()

                    if request.Params.IsGoing && not request.Params.IsHost then
                        query <- query.Where(fun x -> x.Attendees.Any(fun a -> a.Username = username))

                    if request.Params.IsHost && not request.Params.IsGoing then
                        query <- query.Where(fun x -> x.HostUsername = username)

                    let! pagedList = PagedList<ActivityDto>.CreateAsync(query, request.Params.PageNumber, request.Params.PageSize)
                    return ServiceResponse.success pagedList
                }

module Details =
    type Query() =
        member val Id: Guid = Guid.Empty with get, set
        interface IRequest<ServiceResponse<ActivityDto>>

    type Handler(context: DataContext, mapper: IMapper, userAccessor: IUserAccessor) =
        interface IRequestHandler<Query, ServiceResponse<ActivityDto>> with
            member _.Handle(request, _ct) =
                task {
                    let username = userAccessor.GetUsername()
                    let! activity =
                        context.Activities
                            .ProjectTo<ActivityDto>(mapper.ConfigurationProvider, dict ["currentUsername", box username])
                            .FirstOrDefaultAsync(fun x -> x.Id = request.Id)
                    return ServiceResponse.success activity
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
