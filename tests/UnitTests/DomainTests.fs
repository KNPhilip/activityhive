module DomainTests

open System
open Domain
open Xunit
open FsUnit.Xunit

[<Fact>]
let ``RefreshToken IsExpired returns true when Expires is in the past`` () =
    let token =
        { Id = 1
          User = Unchecked.defaultof<User>
          Token = "tok"
          Expires = DateTime.UtcNow.AddSeconds(-1.0)
          Revoked = None }
    token.IsExpired |> should equal true

[<Fact>]
let ``RefreshToken IsExpired returns false when Expires is in the future`` () =
    let token =
        { Id = 1
          User = Unchecked.defaultof<User>
          Token = "tok"
          Expires = DateTime.UtcNow.AddDays(1.0)
          Revoked = None }
    token.IsExpired |> should equal false

[<Fact>]
let ``RefreshToken IsActive returns false when token is expired`` () =
    let token =
        { Id = 1
          User = Unchecked.defaultof<User>
          Token = "tok"
          Expires = DateTime.UtcNow.AddSeconds(-1.0)
          Revoked = None }
    token.IsActive |> should equal false

[<Fact>]
let ``RefreshToken IsActive returns false when token is revoked`` () =
    let token =
        { Id = 1
          User = Unchecked.defaultof<User>
          Token = "tok"
          Expires = DateTime.UtcNow.AddDays(1.0)
          Revoked = Some DateTime.UtcNow }
    token.IsActive |> should equal false

[<Fact>]
let ``RefreshToken IsActive returns true when token is valid and not revoked`` () =
    let token =
        { Id = 1
          User = Unchecked.defaultof<User>
          Token = "tok"
          Expires = DateTime.UtcNow.AddDays(1.0)
          Revoked = None }
    token.IsActive |> should equal true

[<Fact>]
let ``RefreshToken IsActive returns false when both expired and revoked`` () =
    let token =
        { Id = 1
          User = Unchecked.defaultof<User>
          Token = "tok"
          Expires = DateTime.UtcNow.AddSeconds(-10.0)
          Revoked = Some (DateTime.UtcNow.AddSeconds(-5.0)) }
    token.IsActive |> should equal false

[<Fact>]
let ``New Activity starts as not cancelled`` () =
    let activity: Activity =
        { Id = Guid.NewGuid()
          Title = "Test"
          Date = DateTime.UtcNow
          Description = "desc"
          Category = "drinks"
          City = "London"
          Venue = "Pub"
          IsCancelled = false
          Attendees = ResizeArray()
          Comments = ResizeArray() }
    activity.IsCancelled |> should equal false

[<Fact>]
let ``Activity can be marked as cancelled`` () =
    let mutable activity: Activity =
        { Id = Guid.NewGuid()
          Title = "Test"
          Date = DateTime.UtcNow
          Description = "desc"
          Category = "drinks"
          City = "London"
          Venue = "Pub"
          IsCancelled = false
          Attendees = ResizeArray()
          Comments = ResizeArray() }
    activity <- { activity with IsCancelled = true }
    activity.IsCancelled |> should equal true

[<Fact>]
let ``User has empty collections by default`` () =
    let user = User()
    user.Activities |> Seq.isEmpty |> should equal true
    user.Photos |> Seq.isEmpty |> should equal true
    user.Followings |> Seq.isEmpty |> should equal true
    user.Followers |> Seq.isEmpty |> should equal true
    user.RefreshTokens |> Seq.isEmpty |> should equal true
