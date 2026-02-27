namespace Infrastructure

open System
open System.Security.Claims
open System.Threading.Tasks
open Application.Interfaces
open CloudinaryDotNet
open CloudinaryDotNet.Actions
open Microsoft.AspNetCore.Authorization
open Microsoft.AspNetCore.Http
open Microsoft.EntityFrameworkCore
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Options
open Persistence
open SendGrid
open SendGrid.Helpers.Mail

[<CLIMutable>]
type CloudinarySettings =
    { CloudName: string
      ApiKey: string
      ApiSecret: string }

type UserAccessor(httpContextAccessor: IHttpContextAccessor) =
    interface IUserAccessor with
        member _.GetUsername() =
            if isNull httpContextAccessor.HttpContext then null
            else httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.Name)

type PhotoAccessor(settings: IOptions<CloudinarySettings>) =
    let cloudinary =
        let s = settings.Value
        let account = Account(s.CloudName, s.ApiKey, s.ApiSecret)
        new Cloudinary(account)

    interface IPhotoAccessor with
        member _.AddPhoto(file: IFormFile) =
            task {
                if file.Length > 0L then
                    use stream = file.OpenReadStream()
                    let uploadParams =
                        ImageUploadParams(
                            File = new FileDescription(file.FileName, stream),
                            Transformation = Transformation().Height(500).Width(500).Crop("fill")
                        )
                    let! uploadResult = cloudinary.UploadAsync(uploadParams)
                    if not (isNull uploadResult.Error) then
                        raise (Exception(uploadResult.Error.Message))
                    return
                        Some
                            { PublicId = uploadResult.PublicId
                              Url = uploadResult.SecureUrl.ToString() }
                else
                    return None
            }

        member _.DeletePhoto(publicId: string) =
            task {
                let deleteParams = DeletionParams(publicId)
                let! result = cloudinary.DestroyAsync(deleteParams)
                return if result.Result = "ok" then Some result.Result else None
            }

type EmailSender(config: IConfiguration) =
    member _.SendEmailAsync(userEmail: string, emailSubject: string, msg: string) =
        task {
            let client = SendGridClient(config["Sendgrid:Key"])
            let message =
                SendGridMessage(
                    From = EmailAddress("knphilip@outlook.com", config["Sendgrid:User"]),
                    Subject = emailSubject,
                    PlainTextContent = msg,
                    HtmlContent = msg
                )
            message.AddTo(EmailAddress(userEmail))
            message.SetClickTracking(false, false)
            do! client.SendEmailAsync(message) :> Task
        }

type IsHostRequirement() =
    interface IAuthorizationRequirement

type IsHostRequirementHandler(dbContext: DataContext, httpContextAccessor: IHttpContextAccessor) =
    inherit AuthorizationHandler<IsHostRequirement>()

    override _.HandleRequirementAsync(context: AuthorizationHandlerContext, requirement: IsHostRequirement) =
        task {
            let userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
            if not (isNull userId) then
                let routeId =
                    httpContextAccessor.HttpContext.Request.RouteValues
                    |> Seq.tryFind (fun kv -> kv.Key = "id")
                    |> Option.bind (fun kv -> if isNull kv.Value then None else Some (kv.Value.ToString()))
                match routeId with
                | Some idStr ->
                    match Guid.TryParse(idStr) with
                    | true, activityId ->
                        let! attendee =
                            dbContext.ActivityAttendees
                                .AsNoTracking()
                                .SingleOrDefaultAsync(fun x -> x.UserId = userId && x.ActivityId = activityId)
                        if not (isNull (box attendee)) && attendee.IsHost then
                            context.Succeed(requirement)
                    | false, _ -> ()
                | None -> ()
        } :> Task
