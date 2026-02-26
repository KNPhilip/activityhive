namespace Domain

open System
open System.Collections.Generic
open Microsoft.AspNetCore.Identity

[<CLIMutable>]
type Photo =
    { mutable Id: string
      mutable Url: string
      mutable IsMain: bool }

type User() =
    inherit IdentityUser()
    member val DisplayName: string = "" with get, set
    member val Bio: string = "" with get, set
    member val Activities: ICollection<ActivityAttendee> = ResizeArray<ActivityAttendee>() with get, set
    member val Photos: ICollection<Photo> = ResizeArray<Photo>() with get, set
    member val Followings: ICollection<UserFollowing> = ResizeArray<UserFollowing>() with get, set
    member val Followers: ICollection<UserFollowing> = ResizeArray<UserFollowing>() with get, set
    member val RefreshTokens: ICollection<RefreshToken> = ResizeArray<RefreshToken>() with get, set

and [<CLIMutable>] ActivityAttendee =
    { mutable UserId: string
      mutable User: User
      mutable ActivityId: Guid
      mutable Activity: Activity
      mutable IsHost: bool }

and [<CLIMutable>] Activity =
    { mutable Id: Guid
      mutable Title: string
      mutable Date: DateTime
      mutable Description: string
      mutable Category: string
      mutable City: string
      mutable Venue: string
      mutable IsCancelled: bool
      mutable Attendees: ICollection<ActivityAttendee>
      mutable Comments: ICollection<Comment> }

and [<CLIMutable>] Comment =
    { mutable Id: int
      mutable Body: string
      mutable Author: User
      mutable Activity: Activity
      mutable CreatedAt: DateTime }

and [<CLIMutable>] UserFollowing =
    { mutable ObserverId: string
      mutable Observer: User
      mutable TargetId: string
      mutable Target: User }

and [<CLIMutable>] RefreshToken =
    { mutable Id: int
      mutable User: User
      mutable Token: string
      mutable Expires: DateTime
      mutable Revoked: DateTime option }
    member this.IsExpired = DateTime.UtcNow >= this.Expires
    member this.IsActive = this.Revoked.IsNone && not this.IsExpired
