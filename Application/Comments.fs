namespace Application.Comments

open System
open System.Linq
open Application.Core
open Application.Interfaces
open Domain
open Persistence
open MediatR
open Microsoft.EntityFrameworkCore

[<CLIMutable>]
type CommentDto =
    { mutable Id: int
      mutable CreatedAt: DateTime
      mutable Body: string
      mutable Username: string
      mutable DisplayName: string
      mutable Image: string }

[<AutoOpen>]
module private CommentMapping =
    let mapCommentToDto (c: Comment) : CommentDto =
        { Id          = c.Id
          CreatedAt   = c.CreatedAt
          Body        = c.Body
          Username    = if isNull (box c.Author) then "" else c.Author.UserName
          DisplayName = if isNull (box c.Author) then "" else c.Author.DisplayName
          Image       =
              if isNull (box c.Author) then ""
              else
                  c.Author.Photos
                  |> Seq.tryFind (fun p -> p.IsMain)
                  |> Option.map (fun p -> p.Url)
                  |> Option.defaultValue "" }

module List =
    type Query() =
        member val ActivityId: Guid = Guid.Empty with get, set
        interface IRequest<ServiceResponse<CommentDto list>>

    type Handler(context: DataContext) =
        interface IRequestHandler<Query, ServiceResponse<CommentDto list>> with
            member _.Handle(request, _ct) =
                task {
                    let! comments =
                        context.Comments
                            .Where(fun x -> x.Activity.Id = request.ActivityId)
                            .OrderByDescending(fun x -> x.CreatedAt)
                            .Include(fun c -> c.Author)
                            .ThenInclude(fun (u: User) -> u.Photos :> System.Collections.Generic.IEnumerable<Photo>)
                            .ToListAsync()
                    return ServiceResponse.success (comments |> Seq.map mapCommentToDto |> Seq.toList)
                }

module Create =
    [<CLIMutable>]
    type Command =
        { mutable Body: string
          mutable ActivityId: Guid }
        interface IRequest<ServiceResponse<CommentDto>>

    type Handler(context: DataContext, userAccessor: IUserAccessor) =
        interface IRequestHandler<Command, ServiceResponse<CommentDto>> with
            member _.Handle(request, _ct) =
                task {
                    let! activity = context.Activities.FindAsync(request.ActivityId)
                    if isNull (box activity) then
                        return ServiceResponse.failure "Activity not found."
                    else
                        let! user =
                            context.Users
                                .Include(fun p -> p.Photos :> System.Collections.Generic.IEnumerable<Photo>)
                                .SingleOrDefaultAsync(fun p -> p.UserName = userAccessor.GetUsername())
                        let comment: Comment =
                            { Id = 0
                              Body = request.Body
                              Author = user
                              Activity = activity
                              CreatedAt = DateTime.UtcNow }
                        activity.Comments.Add(comment)
                        let! success = context.SaveChangesAsync()
                        return
                            if success > 0 then ServiceResponse.success (mapCommentToDto comment)
                            else ServiceResponse.failure "Failed to add comment."
                }
