namespace API

open System
open System.Collections.Generic
open System.IdentityModel.Tokens.Jwt
open System.Linq
open System.Net.Http
open System.Net.Http.Json
open System.Security.Claims
open System.Security.Cryptography
open System.Text
open API.Dtos
open Application.Core
open Domain
open Infrastructure
open Microsoft.AspNetCore.Http
open Microsoft.AspNetCore.Identity
open Microsoft.EntityFrameworkCore
open Microsoft.Extensions.Configuration
open Microsoft.IdentityModel.Tokens

type IAuthService =
    abstract CreateJWT: User -> string
    abstract Login: LoginDto -> System.Threading.Tasks.Task<ServiceResponse<UserDto>>
    abstract Register: RegisterDto -> System.Threading.Tasks.Task<ServiceResponse<UserDto>>
    abstract GetCurrentUser: unit -> System.Threading.Tasks.Task<ServiceResponse<UserDto>>
    abstract VerifyFacebookToken: string -> System.Threading.Tasks.Task<bool>
    abstract FacebookLogin: string -> System.Threading.Tasks.Task<ServiceResponse<UserDto>>
    abstract RefreshJWT: unit -> System.Threading.Tasks.Task<ServiceResponse<UserDto>>
    abstract CreateUserObject: User -> UserDto

type AuthService
    (
        userManager: UserManager<User>,
        config: IConfiguration,
        httpContextAccessor: IHttpContextAccessor,
        signInManager: SignInManager<User>,
        emailSender: EmailSender
    ) =

    let httpClient =
        new HttpClient(BaseAddress = Uri("https://graph.facebook.com"))

    let createJWT (user: User) =
        let claims =
            [ Claim(ClaimTypes.NameIdentifier, user.Id)
              Claim(ClaimTypes.Email, user.Email)
              Claim(ClaimTypes.Name, user.UserName) ]

        let key = SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["TokenKey"]))
        let creds = SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature)

        let tokenDescriptor =
            SecurityTokenDescriptor(
                Subject = ClaimsIdentity(claims),
                Expires = DateTime.UtcNow.AddMinutes(10.0),
                SigningCredentials = creds
            )

        let tokenHandler = JwtSecurityTokenHandler()
        let token = tokenHandler.CreateToken(tokenDescriptor)
        tokenHandler.WriteToken(token)

    let createUserObject (user: User) : UserDto =
        { DisplayName = user.DisplayName
          Image =
            user.Photos
            |> Seq.tryFind (fun x -> x.IsMain)
            |> Option.map (fun x -> x.Url)
            |> Option.defaultValue null
          Token = createJWT user
          Username = user.UserName }

    interface IAuthService with
        member _.CreateJWT(user) = createJWT user

        member _.Login(request) =
            task {
                let! user =
                    userManager.Users
                        .Include(fun u -> u.Photos :> IEnumerable<Photo>)
                        .FirstOrDefaultAsync(fun u -> u.Email = request.Email)

                if isNull (box user) then
                    return ServiceResponse.failure<UserDto> "Invalid email"
                else
                    let! result = userManager.CheckPasswordAsync(user, request.Password)

                    if result then
                        return ServiceResponse.success (createUserObject user)
                    else
                        return ServiceResponse.failure<UserDto> "Invalid password"
            }

        member _.Register(request) =
            task {
                let! usernameExists =
                    userManager.Users.AnyAsync(fun x -> x.UserName = request.Username)

                if usernameExists then
                    return ServiceResponse.failure<UserDto> "This username is already taken.."
                else
                    let! emailExists =
                        userManager.Users.AnyAsync(fun x -> x.Email = request.Email)

                    if emailExists then
                        return ServiceResponse.failure<UserDto> "Email is already taken.."
                    else
                        let user =
                            User(
                                DisplayName = request.DisplayName,
                                Email = request.Email,
                                UserName = request.Username
                            )

                        let! result = userManager.CreateAsync(user, request.Password)

                        if result.Succeeded then
                            return ServiceResponse.success (createUserObject user)
                        else
                            return ServiceResponse.failure<UserDto> "Please make a stronger password."
            }

        member _.GetCurrentUser() =
            task {
                let email =
                    httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.Email)

                let! user =
                    userManager.Users
                        .Include(fun u -> u.Photos :> IEnumerable<Photo>)
                        .FirstOrDefaultAsync(fun u -> u.Email = email)

                if isNull (box user) then
                    return ServiceResponse.failure<UserDto> "User not found."
                else
                    return ServiceResponse.success (createUserObject user)
            }

        member _.VerifyFacebookToken(accessToken) =
            task {
                let fbVerifyKeys = config["Facebook:AppId"] + "|" + config["Facebook:ApiSecret"]

                let! response =
                    httpClient.GetAsync(
                        $"debug_token?input_token={accessToken}&access_token={fbVerifyKeys}"
                    )

                return response.IsSuccessStatusCode
            }

        member _.FacebookLogin(accessToken) =
            task {
                let fbUrl =
                    $"me?access_token={accessToken}&fields=name,email,picture.width(100).height(100)"

                let! fbInfo = httpClient.GetFromJsonAsync<FacebookDto>(fbUrl)

                let! existingUser =
                    userManager.Users
                        .Include(fun p -> p.Photos :> IEnumerable<Photo>)
                        .FirstOrDefaultAsync(fun x -> x.Email = fbInfo.Email)

                if not (isNull (box existingUser)) then
                    return ServiceResponse.success (createUserObject existingUser)
                else
                    let user =
                        User(
                            DisplayName = fbInfo.Name,
                            Email = fbInfo.Email,
                            UserName = fbInfo.Email
                        )

                    user.Photos.Add(
                        { Id = "fb_" + fbInfo.Id
                          Url = fbInfo.Picture.Data.Url
                          IsMain = true }
                    )

                    let! result = userManager.CreateAsync(user)

                    if not result.Succeeded then
                        return ServiceResponse.failure<UserDto> "Problem creating user account"
                    else
                        return ServiceResponse.success (createUserObject user)
            }

        member _.RefreshJWT() =
            task {
                let refreshToken =
                    httpContextAccessor.HttpContext.Request.Cookies["refreshToken"]

                let username =
                    httpContextAccessor.HttpContext.User.FindFirstValue(ClaimTypes.Name)

                let! user =
                    userManager.Users
                        .Include(fun u -> u.RefreshTokens :> IEnumerable<RefreshToken>)
                        .Include(fun u -> u.Photos :> IEnumerable<Photo>)
                        .FirstOrDefaultAsync(fun u -> u.UserName = username)

                let hashedToken =
                    if isNull refreshToken then null
                    else Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)))

                let oldToken =
                    if isNull (box user) || isNull hashedToken then
                        None
                    else
                        user.RefreshTokens |> Seq.tryFind (fun x -> x.Token = hashedToken)

                match box user, oldToken with
                | null, _ -> return ServiceResponse.failure<UserDto> "No valid Refresh Tokens found."
                | _, Some t when not t.IsActive ->
                    return ServiceResponse.failure<UserDto> "No valid Refresh Tokens found."
                | _ -> return ServiceResponse.success (createUserObject user)
            }

        member _.CreateUserObject(user) = createUserObject user
