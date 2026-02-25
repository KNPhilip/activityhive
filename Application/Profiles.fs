namespace Application.Profiles

open System
open System.Linq
open System.Collections.Generic
open Application.Core
open Application.Interfaces
open Domain
open Persistence
open AutoMapper
open AutoMapper.QueryableExtensions
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

module Details =
    type Query() =
        member val Username: string = "" with get, set
        interface IRequest<ServiceResponse<UserProfile>>

    type Handler(context: DataContext, mapper: IMapper, userAccessor: IUserAccessor) =
        interface IRequestHandler<Query, ServiceResponse<UserProfile>> with
            member _.Handle(request, _ct) =
                task {
                    let username = userAccessor.GetUsername()
                    let! profile =
                        context.Users
                            .ProjectTo<UserProfile>(mapper.ConfigurationProvider, dict ["currentUsername", box username])
                            .FirstOrDefaultAsync(fun x -> x.Username = request.Username)
                    if isNull (box profile) then
                        return ServiceResponse.failure "Profile not found."
                    else
                        return ServiceResponse.success profile
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

    type Handler(context: DataContext, mapper: IMapper) =
        interface IRequestHandler<Query, ServiceResponse<UserActivityDto list>> with
            member _.Handle(request, _ct) =
                task {
                    let mutable query =
                        context.ActivityAttendees
                            .Where(fun u -> u.User.UserName = request.Username)
                            .OrderBy(fun a -> a.Activity.Date)
                            .ProjectTo<UserActivityDto>(mapper.ConfigurationProvider)
                            .AsQueryable()

                    query <-
                        match request.Predicate with
                        | "past"    -> query.Where(fun a -> a.Date <= DateTime.UtcNow)
                        | "hosting" -> query.Where(fun a -> a.HostUsername = request.Username)
                        | _         -> query.Where(fun a -> a.Date >= DateTime.UtcNow)

                    let! activities = query.ToListAsync()
                    return ServiceResponse.success (activities |> Seq.toList)
                }
