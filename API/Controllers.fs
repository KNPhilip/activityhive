namespace API.Controllers

open System
open System.IO
open System.Security.Claims
open System.Security.Cryptography
open System.Text
open API
open API.Dtos
open API.Extensions
open Application.Core
open Domain
open Infrastructure
open MediatR
open Microsoft.AspNetCore.Authorization
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Identity
open Microsoft.AspNetCore.Mvc
open Microsoft.AspNetCore.WebUtilities
open Microsoft.EntityFrameworkCore

// ---------------------------------------------------------------------------
// Base controller
// ---------------------------------------------------------------------------

[<ApiController>]
[<Route("api/[controller]")>]
type ControllerTemplate() =
    inherit ControllerBase()

    let mutable _mediator: IMediator = Unchecked.defaultof<IMediator>

    member this.Mediator: IMediator =
        if isNull (box _mediator) then
            _mediator <- this.HttpContext.RequestServices.GetService(typeof<IMediator>) :?> IMediator

        _mediator

    member this.HandleResult<'T>(response: ServiceResponse<'T>) : IActionResult =
        if response.Success then
            if isNull (box response.Data) then
                this.NotFound() :> IActionResult
            else
                this.Ok(response.Data) :> IActionResult
        else
            this.BadRequest(response.Error) :> IActionResult

    member this.HandlePagedResult<'T>(response: ServiceResponse<PagedList<'T>>) : IActionResult =
        if response.Success then
            if isNull (box response.Data) then
                this.NotFound() :> IActionResult
            else
                addPaginationHeader
                    this.Response
                    response.Data.CurrentPage
                    response.Data.PageSize
                    response.Data.TotalCount
                    response.Data.TotalPages

                this.Ok(response.Data) :> IActionResult
        else
            this.BadRequest(response.Error) :> IActionResult

// ---------------------------------------------------------------------------
// ActivitiesController
// ---------------------------------------------------------------------------

[<Route("api/[controller]")>]
type ActivitiesController() =
    inherit ControllerTemplate()

    [<HttpGet>]
    member this.GetActivities([<FromQuery>] request: Application.Activities.ActivityParams) =
        task {
            let q = Application.Activities.List.Query()
            q.Params <- request
            let! result = this.Mediator.Send(q)
            return this.HandlePagedResult(result)
        }

    [<HttpGet("{id}")>]
    member this.GetActivity(id: Guid) =
        task {
            let q = Application.Activities.Details.Query()
            q.Id <- id
            let! result = this.Mediator.Send(q)
            return this.HandleResult(result)
        }

    [<HttpPost>]
    member this.CreateActivity([<FromBody>] activity: Activity) =
        task {
            let! result = this.Mediator.Send({ Application.Activities.Create.Command.Activity = activity })
            return this.HandleResult(result)
        }

    [<HttpPut("{id}")>]
    [<Authorize(Policy = "IsActivityHost")>]
    member this.EditActivity(id: Guid, [<FromBody>] activity: Activity) =
        task {
            activity.Id <- id
            let! result = this.Mediator.Send({ Application.Activities.Edit.Command.Activity = activity })
            return this.HandleResult(result)
        }

    [<HttpDelete("{id}")>]
    [<Authorize(Policy = "IsActivityHost")>]
    member this.DeleteActivity(id: Guid) =
        task {
            let cmd = Application.Activities.Delete.Command()
            cmd.Id <- id
            let! result = this.Mediator.Send(cmd)
            return this.HandleResult(result)
        }

    [<HttpPost("{id}/attend")>]
    member this.Attend(id: Guid) =
        task {
            let cmd = Application.Activities.UpdateAttendance.Command()
            cmd.Id <- id
            let! result = this.Mediator.Send(cmd)
            return this.HandleResult(result)
        }

// ---------------------------------------------------------------------------
// ProfileController
// ---------------------------------------------------------------------------

[<Route("api/[controller]")>]
type ProfileController() =
    inherit ControllerTemplate()

    [<HttpGet("{username}")>]
    member this.GetProfile(username: string) =
        task {
            let q = Application.Profiles.Details.Query()
            q.Username <- username
            let! result = this.Mediator.Send(q)
            return this.HandleResult(result)
        }

    [<HttpPut>]
    member this.Edit([<FromBody>] command: Application.Profiles.Edit.Command) =
        task {
            let! result = this.Mediator.Send(command)
            return this.HandleResult(result)
        }

    [<HttpGet("{username}/activities")>]
    member this.GetProfileActivities(username: string, [<FromQuery>] predicate: string) =
        task {
            let q = Application.Profiles.ListActivities.Query()
            q.Username <- username
            q.Predicate <- if isNull predicate then "" else predicate
            let! result = this.Mediator.Send(q)
            return this.HandleResult(result)
        }

// ---------------------------------------------------------------------------
// PhotoController
// ---------------------------------------------------------------------------

[<Route("api/[controller]")>]
type PhotoController() =
    inherit ControllerTemplate()

    [<HttpPost>]
    member this.Add([<FromForm>] command: Application.Photos.Add.Command) =
        task {
            let! result = this.Mediator.Send(command)
            return this.HandleResult(result)
        }

    [<HttpDelete("{id}")>]
    member this.Delete(id: string) =
        task {
            let cmd = Application.Photos.Delete.Command()
            cmd.Id <- id
            let! result = this.Mediator.Send(cmd)
            return this.HandleResult(result)
        }

    [<HttpPost("{id}/setMain")>]
    member this.SetMain(id: string) =
        task {
            let cmd = Application.Photos.SetMain.Command()
            cmd.Id <- id
            let! result = this.Mediator.Send(cmd)
            return this.HandleResult(result)
        }

// ---------------------------------------------------------------------------
// FollowController
// ---------------------------------------------------------------------------

[<Route("api/[controller]")>]
type FollowController() =
    inherit ControllerTemplate()

    [<HttpPost("{username}")>]
    member this.Follow(username: string) =
        task {
            let cmd = Application.Followers.FollowToggle.Command()
            cmd.TargetUsername <- username
            let! result = this.Mediator.Send(cmd)
            return this.HandleResult(result)
        }

    [<HttpGet("{username}")>]
    member this.GetFollowings(username: string, predicate: string) =
        task {
            let q = Application.Followers.List.Query()
            q.Username <- username
            q.Predicate <- if isNull predicate then "" else predicate
            let! result = this.Mediator.Send(q)
            return this.HandleResult(result)
        }

// ---------------------------------------------------------------------------
// ErrorController
// ---------------------------------------------------------------------------

[<Route("api/[controller]")>]
type ErrorController() =
    inherit ControllerTemplate()

    [<HttpGet("bad-request")>]
    member this.GetBadRequest() : IActionResult =
        this.BadRequest("This is a bad request") :> IActionResult

    [<HttpGet("server-error")>]
    member _.GetServerError() : IActionResult =
        raise (Exception("This is a server error"))

// ---------------------------------------------------------------------------
// FallbackController
// ---------------------------------------------------------------------------

[<AllowAnonymous>]
type FallbackController() =
    inherit Controller()

    member _.Index() : IActionResult =
        let path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "index.html")
        PhysicalFileResult(path, "text/HTML") :> IActionResult

// ---------------------------------------------------------------------------
// AuthController
// ---------------------------------------------------------------------------

[<ApiController>]
[<Route("api/[controller]")>]
type AuthController
    (authService: IAuthService, userManager: UserManager<User>, emailSender: EmailSender) =
    inherit ControllerBase()

    let hashToken (raw: string) =
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(raw)))

    let generateRefreshToken () =
        let randomNumber = Array.zeroCreate<byte> 32
        use rng = RandomNumberGenerator.Create()
        rng.GetBytes(randomNumber)
        let rawToken = Convert.ToBase64String(randomNumber)
        { Id = 0
          User = Unchecked.defaultof<User>
          Token = hashToken rawToken
          Expires = DateTime.UtcNow.AddDays(7.0)
          Revoked = None },
        rawToken

    member this.SetRefreshToken(user: User) =
        task {
            let refreshToken, rawToken = generateRefreshToken ()
            user.RefreshTokens.Add(refreshToken)
            let! _ = userManager.UpdateAsync(user)

            let cookieOptions =
                CookieOptions(
                    HttpOnly = true,
                    Expires = Nullable(DateTimeOffset.UtcNow.AddDays(7.0))
                )

            this.Response.Cookies.Append("refreshToken", rawToken, cookieOptions)
        }

    [<HttpPost("login")>]
    [<AllowAnonymous>]
    member this.Login([<FromBody>] request: LoginDto) =
        task {
            let! user = userManager.Users.FirstOrDefaultAsync(fun u -> u.Email = request.Email)

            if isNull (box user) then
                return this.Unauthorized() :> IActionResult
            else
                let! response = authService.Login(request)

                if response.Success then
                    do! this.SetRefreshToken(user)
                    return this.Ok(response.Data) :> IActionResult
                else
                    return this.Unauthorized(response.Error |> box) :> IActionResult
        }

    [<HttpPost("register")>]
    [<AllowAnonymous>]
    member this.Register([<FromBody>] request: RegisterDto) =
        task {
            let! response = authService.Register(request)

            if response.Success then
                let! user =
                    userManager.Users.FirstOrDefaultAsync(fun u ->
                        u.UserName = response.Data.Username)

                if isNull (box user) then
                    return this.Unauthorized() :> IActionResult
                else
                    do! this.SetRefreshToken(user)
                    return this.Ok(response.Data) :> IActionResult
            else
                this.ModelState.AddModelError("user", response.Error)
                return this.ValidationProblem() :> IActionResult
        }

    [<HttpGet>]
    [<Authorize>]
    member this.GetCurrentUser() =
        task {
            let emailClaim = this.User.FindFirst(ClaimTypes.Email)
            let email = if isNull emailClaim then null else emailClaim.Value

            let! user =
                userManager.Users.FirstOrDefaultAsync(fun u -> u.Email = email)

            if isNull (box user) then
                return this.Unauthorized() :> IActionResult
            else
                let! response = authService.GetCurrentUser()

                if response.Success then
                    do! this.SetRefreshToken(user)
                    return this.Ok(response.Data) :> IActionResult
                else
                    return this.NotFound(response.Error |> box) :> IActionResult
        }

    [<HttpPost("fbLogin")>]
    [<AllowAnonymous>]
    member this.FacebookLogin(accessToken: string) =
        task {
            let! verified = authService.VerifyFacebookToken(accessToken)

            if not verified then
                return this.Unauthorized() :> IActionResult
            else
                let! response = authService.FacebookLogin(accessToken)

                if response.Success then
                    return this.Ok(response.Data) :> IActionResult
                else
                    return this.NotFound(response.Error |> box) :> IActionResult
        }

    [<HttpPost("refreshToken")>]
    [<Authorize>]
    member this.RefreshToken() =
        task {
            let! response = authService.RefreshJWT()

            if response.Success then
                return this.Ok(response.Data) :> IActionResult
            else
                return this.Unauthorized(response.Error |> box) :> IActionResult
        }

    [<HttpPost("verifyEmail")>]
    [<AllowAnonymous>]
    member this.VerifyEmail(token: string, email: string) =
        task {
            let! user = userManager.FindByEmailAsync(email)

            if isNull (box user) then
                return this.Unauthorized() :> IActionResult
            else
                let decodedTokenBytes = WebEncoders.Base64UrlDecode(token)
                let decodedToken = Encoding.UTF8.GetString(decodedTokenBytes)
                let! result = userManager.ConfirmEmailAsync(user, decodedToken)

                if not result.Succeeded then
                    return this.BadRequest("Could not verify email address.") :> IActionResult
                else
                    return this.Ok("Email confirmed, you can now login") :> IActionResult
        }

    [<HttpGet("resendEmailConfirmationLink")>]
    [<AllowAnonymous>]
    member this.ResendEmailConfirmationLink(email: string) =
        task {
            let! user = userManager.FindByEmailAsync(email)

            if isNull (box user) then
                return this.Unauthorized() :> IActionResult
            else
                let request = this.Request
                let baseUrl = sprintf "%s://%s%s" request.Scheme request.Host.Value (request.PathBase.ToString())
                let! token = userManager.GenerateEmailConfirmationTokenAsync(user)
                let encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token))
                let verifyUrl = $"{baseUrl}/account/verifyEmail?token={encodedToken}&email={user.Email}"

                let message =
                    $"<p>Please click the below link to verify your email address:</p><p><a href='{verifyUrl}'>Click to verify email</a></p>"

                do! emailSender.SendEmailAsync(user.Email, "Please verify email", message)
                return this.Ok("Email verification link resent") :> IActionResult
        }
