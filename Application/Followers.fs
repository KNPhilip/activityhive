namespace Application.Followers

open System.Linq
open Application.Core
open Application.Interfaces
open Application.Profiles
open Domain
open Persistence
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

    type Handler(context: DataContext, userAccessor: IUserAccessor) =
        interface IRequestHandler<Query, ServiceResponse<UserProfile list>> with
            member _.Handle(request, _ct) =
                task {
                    let username = userAccessor.GetUsername()

                    let! followRelations =
                        match request.Predicate with
                        | "followers" ->
                            context.UserFollowings
                                .Where(fun x -> x.Target.UserName = request.Username)
                                .Include(fun x -> x.Observer)
                                .ThenInclude(fun (u: User) -> u.Photos :> System.Collections.Generic.IEnumerable<Photo>)
                                .Include(fun x -> x.Observer)
                                .ThenInclude(fun (u: User) -> u.Followers :> System.Collections.Generic.IEnumerable<UserFollowing>)
                                .Include(fun x -> x.Observer)
                                .ThenInclude(fun (u: User) -> u.Followings :> System.Collections.Generic.IEnumerable<UserFollowing>)
                                .ToListAsync()
                        | _ ->
                            context.UserFollowings
                                .Where(fun x -> x.Observer.UserName = request.Username)
                                .Include(fun x -> x.Target)
                                .ThenInclude(fun (u: User) -> u.Photos :> System.Collections.Generic.IEnumerable<Photo>)
                                .Include(fun x -> x.Target)
                                .ThenInclude(fun (u: User) -> u.Followers :> System.Collections.Generic.IEnumerable<UserFollowing>)
                                .Include(fun x -> x.Target)
                                .ThenInclude(fun (u: User) -> u.Followings :> System.Collections.Generic.IEnumerable<UserFollowing>)
                                .ToListAsync()

                    let! myFollowingIds =
                        context.UserFollowings
                            .Where(fun f -> f.Observer.UserName = username)
                            .Select(fun f -> f.TargetId)
                            .ToListAsync()
                    let followingSet = Set.ofSeq myFollowingIds

                    let users =
                        followRelations
                        |> Seq.map (fun f ->
                            if request.Predicate = "followers" then f.Observer else f.Target)

                    return ServiceResponse.success (users |> Seq.map (mapUserToProfile followingSet) |> Seq.toList)
                }
