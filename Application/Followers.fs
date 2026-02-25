namespace Application.Followers

open System.Linq
open Application.Core
open Application.Interfaces
open Application.Profiles
open Domain
open Persistence
open AutoMapper
open AutoMapper.QueryableExtensions
open MediatR
open Microsoft.EntityFrameworkCore

module FollowToggle =
    type Command() =
        member val TargetUsername: string = "" with get, set
        interface IRequest<ServiceResponse<unit>>

    type Handler(context: DataContext, userAccessor: IUserAccessor) =
        interface IRequestHandler<Command, ServiceResponse<unit>> with
            member _.Handle(request, _ct) =
                task {
                    let! observer = context.Users.FirstOrDefaultAsync(fun x -> x.UserName = userAccessor.GetUsername())
                    let! target = context.Users.FirstOrDefaultAsync(fun x -> x.UserName = request.TargetUsername)
                    if isNull (box target) then
                        return ServiceResponse.failure "Target user not found."
                    else
                        let! existing = context.UserFollowings.FindAsync(observer.Id, target.Id)
                        if isNull (box existing) then
                            let following: UserFollowing =
                                { ObserverId = observer.Id; Observer = observer
                                  TargetId = target.Id; Target = target }
                            context.UserFollowings.Add(following) |> ignore
                        else
                            context.UserFollowings.Remove(existing) |> ignore
                        let! success = context.SaveChangesAsync()
                        return
                            if success > 0 then ServiceResponse.success ()
                            else ServiceResponse.failure "Failed to update following."
                }

module List =
    type Query() =
        member val Username: string = "" with get, set
        member val Predicate: string = "" with get, set
        interface IRequest<ServiceResponse<UserProfile list>>

    type Handler(context: DataContext, mapper: IMapper, userAccessor: IUserAccessor) =
        interface IRequestHandler<Query, ServiceResponse<UserProfile list>> with
            member _.Handle(request, _ct) =
                task {
                    let username = userAccessor.GetUsername()
                    let! profiles =
                        match request.Predicate with
                        | "followers" ->
                            context.UserFollowings
                                .Where(fun x -> x.Target.UserName = request.Username)
                                .Select(fun u -> u.Observer)
                                .ProjectTo<UserProfile>(mapper.ConfigurationProvider, dict ["currentUsername", box username])
                                .ToListAsync()
                        | _ ->
                            context.UserFollowings
                                .Where(fun x -> x.Observer.UserName = request.Username)
                                .Select(fun u -> u.Target)
                                .ProjectTo<UserProfile>(mapper.ConfigurationProvider, dict ["currentUsername", box username])
                                .ToListAsync()
                    return ServiceResponse.success (profiles |> Seq.toList)
                }
