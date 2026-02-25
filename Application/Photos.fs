namespace Application.Photos

open System.Linq
open Application.Core
open Application.Interfaces
open Domain
open Persistence
open MediatR
open Microsoft.EntityFrameworkCore
open System.Collections.Generic

module Add =
    type Command() =
        member val File: Microsoft.AspNetCore.Http.IFormFile = null with get, set
        interface IRequest<ServiceResponse<Photo>>

    type Handler(context: DataContext, photoAccessor: IPhotoAccessor, userAccessor: IUserAccessor) =
        interface IRequestHandler<Command, ServiceResponse<Photo>> with
            member _.Handle(request, _ct) =
                task {
                    let! user =
                        context.Users
                            .Include(fun p -> p.Photos :> IEnumerable<Photo>)
                            .FirstOrDefaultAsync(fun x -> x.UserName = userAccessor.GetUsername())
                    if isNull (box user) then
                        return ServiceResponse.failure "User not found."
                    else
                        let! uploadResult = photoAccessor.AddPhoto(request.File)
                        match uploadResult with
                        | None -> return ServiceResponse.failure "Photo upload failed."
                        | Some result ->
                            let photo: Photo =
                                { Id = result.PublicId
                                  Url = result.Url
                                  IsMain = not (user.Photos |> Seq.exists (fun x -> x.IsMain)) }
                            user.Photos.Add(photo)
                            let! success = context.SaveChangesAsync()
                            return
                                if success > 0 then ServiceResponse.success photo
                                else ServiceResponse.failure "Problem adding photo."
                }

module Delete =
    type Command() =
        member val Id: string = "" with get, set
        interface IRequest<ServiceResponse<unit>>

    type Handler(context: DataContext, photoAccessor: IPhotoAccessor, userAccessor: IUserAccessor) =
        interface IRequestHandler<Command, ServiceResponse<unit>> with
            member _.Handle(request, _ct) =
                task {
                    let! user =
                        context.Users
                            .Include(fun p -> p.Photos :> IEnumerable<Photo>)
                            .FirstOrDefaultAsync(fun x -> x.UserName = userAccessor.GetUsername())
                    if isNull (box user) then
                        return ServiceResponse.failure "User not found."
                    else
                        let photo = user.Photos |> Seq.tryFind (fun x -> x.Id = request.Id)
                        match photo with
                        | None -> return ServiceResponse.failure "Photo not found."
                        | Some p ->
                            if p.IsMain then
                                return ServiceResponse.failure "Cannot delete main photo."
                            else
                                let! result = photoAccessor.DeletePhoto(request.Id)
                                match result with
                                | None -> return ServiceResponse.failure "Problem deleting photo from Cloudinary."
                                | Some _ ->
                                    user.Photos.Remove(p) |> ignore
                                    let! success = context.SaveChangesAsync()
                                    return
                                        if success > 0 then ServiceResponse.success ()
                                        else ServiceResponse.failure "Problem deleting photo."
                }

module SetMain =
    type Command() =
        member val Id: string = "" with get, set
        interface IRequest<ServiceResponse<unit>>

    type Handler(context: DataContext, userAccessor: IUserAccessor) =
        interface IRequestHandler<Command, ServiceResponse<unit>> with
            member _.Handle(request, _ct) =
                task {
                    let! user =
                        context.Users
                            .Include(fun p -> p.Photos :> IEnumerable<Photo>)
                            .FirstOrDefaultAsync(fun x -> x.UserName = userAccessor.GetUsername())
                    if isNull (box user) then
                        return ServiceResponse.failure "User not found."
                    else
                        let photo = user.Photos |> Seq.tryFind (fun x -> x.Id = request.Id)
                        match photo with
                        | None -> return ServiceResponse.failure "Photo not found."
                        | Some p ->
                            let currentMain = user.Photos |> Seq.tryFind (fun x -> x.IsMain)
                            currentMain |> Option.iter (fun m -> m.IsMain <- false)
                            p.IsMain <- true
                            let! success = context.SaveChangesAsync()
                            return
                                if success > 0 then ServiceResponse.success ()
                                else ServiceResponse.failure "Problem setting main photo."
                }
