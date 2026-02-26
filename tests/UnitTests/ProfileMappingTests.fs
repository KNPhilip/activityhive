module ProfileMappingTests

open System
open System.Collections.Generic
open Application.Profiles
open Application.Core
open Domain
open Xunit
open FsUnit.Xunit

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

let private makeUser username displayName bio (photos: Photo list) (followersCount: int) (followingCount: int) =
    let user = User()
    user.Id <- Guid.NewGuid().ToString()
    user.UserName <- username
    user.DisplayName <- displayName
    user.Bio <- bio
    for p in photos do user.Photos.Add(p)
    // Followers/Followings are populated by caller
    user

let private mainPhotoOf (url: string) =
    { Id = "main"; Url = url; IsMain = true }

let private nonMainPhoto (url: string) =
    { Id = "side"; Url = url; IsMain = false }

// ---------------------------------------------------------------------------
// mainPhoto helper
// ---------------------------------------------------------------------------

[<Fact>]
let ``mainPhoto returns empty string when no photos`` () =
    let photos: ICollection<Photo> = ResizeArray()
    mainPhoto photos |> should equal ""

[<Fact>]
let ``mainPhoto returns main photo url`` () =
    let photos: ICollection<Photo> = ResizeArray([ mainPhotoOf "http://main.jpg"; nonMainPhoto "http://side.jpg" ])
    mainPhoto photos |> should equal "http://main.jpg"

[<Fact>]
let ``mainPhoto returns empty string when no photo is marked main`` () =
    let photos: ICollection<Photo> = ResizeArray([ nonMainPhoto "http://side.jpg" ])
    mainPhoto photos |> should equal ""

[<Fact>]
let ``mainPhoto returns first main photo url when multiple main photos exist`` () =
    // Weakness: Multiple IsMain=true photos — first match wins
    let photos: ICollection<Photo> =
        ResizeArray(
            [ { Id = "a"; Url = "http://first.jpg"; IsMain = true }
              { Id = "b"; Url = "http://second.jpg"; IsMain = true } ])
    mainPhoto photos |> should equal "http://first.jpg"

// ---------------------------------------------------------------------------
// mapUserToProfile
// ---------------------------------------------------------------------------

[<Fact>]
let ``mapUserToProfile maps basic user fields`` () =
    let user = makeUser "bob" "Bob Smith" "A developer" [] 0 0
    let profile = mapUserToProfile Set.empty user
    profile.Username |> should equal "bob"
    profile.DisplayName |> should equal "Bob Smith"
    profile.Bio |> should equal "A developer"

[<Fact>]
let ``mapUserToProfile sets Following to false when user not in followingSet`` () =
    let user = makeUser "bob" "Bob" "" [] 0 0
    let profile = mapUserToProfile Set.empty user
    profile.Following |> should equal false

[<Fact>]
let ``mapUserToProfile sets Following to true when user id is in followingSet`` () =
    let user = makeUser "bob" "Bob" "" [] 0 0
    let followingSet = Set.ofList [ user.Id ]
    let profile = mapUserToProfile followingSet user
    profile.Following |> should equal true

[<Fact>]
let ``mapUserToProfile sets Image to main photo url`` () =
    let user = makeUser "bob" "Bob" "" [ mainPhotoOf "http://photo.jpg" ] 0 0
    let profile = mapUserToProfile Set.empty user
    profile.Image |> should equal "http://photo.jpg"

[<Fact>]
let ``mapUserToProfile sets Image to empty string when no photos`` () =
    let user = makeUser "bob" "Bob" "" [] 0 0
    let profile = mapUserToProfile Set.empty user
    profile.Image |> should equal ""

[<Fact>]
let ``mapUserToProfile counts followers correctly`` () =
    let user = makeUser "bob" "Bob" "" [] 0 0
    // Add follower relations
    let follower = makeUser "jane" "Jane" "" [] 0 0
    user.Followers.Add({ ObserverId = follower.Id; Observer = follower; TargetId = user.Id; Target = user })
    let profile = mapUserToProfile Set.empty user
    profile.FollowersCount |> should equal 1

[<Fact>]
let ``mapUserToProfile counts followings correctly`` () =
    let user = makeUser "bob" "Bob" "" [] 0 0
    let target = makeUser "jane" "Jane" "" [] 0 0
    user.Followings.Add({ ObserverId = user.Id; Observer = user; TargetId = target.Id; Target = target })
    let profile = mapUserToProfile Set.empty user
    profile.FollowingCount |> should equal 1

[<Fact>]
let ``mapUserToProfile includes photos collection`` () =
    let photo = mainPhotoOf "http://img.jpg"
    let user = makeUser "bob" "Bob" "" [ photo ] 0 0
    let profile = mapUserToProfile Set.empty user
    profile.Photos |> Seq.length |> should equal 1

// ---------------------------------------------------------------------------
// mapUserActivityDto
// ---------------------------------------------------------------------------

let private makeActivity title date category =
    let id = Guid.NewGuid()
    { Id = id
      Title = title
      Date = date
      Description = "desc"
      Category = category
      City = "London"
      Venue = "Pub"
      IsCancelled = false
      Attendees = ResizeArray()
      Comments = ResizeArray() }

let private makeActivityAttendee (user: User) (activity: Activity) isHost =
    let aa: ActivityAttendee =
        { UserId = user.Id
          User = user
          ActivityId = activity.Id
          Activity = activity
          IsHost = isHost }
    activity.Attendees.Add(aa)
    aa

[<Fact>]
let ``mapUserActivityDto maps activity fields correctly`` () =
    let host = makeUser "bob" "Bob" "" [] 0 0
    let activity = makeActivity "Test Activity" (DateTime.UtcNow.AddDays(1.0)) "music"
    let aa = makeActivityAttendee host activity true
    let dto = mapUserActivityDto aa
    dto.Id |> should equal activity.Id
    dto.Title |> should equal "Test Activity"
    dto.Category |> should equal "music"

[<Fact>]
let ``mapUserActivityDto returns host username from attendees`` () =
    let host = makeUser "bob" "Bob" "" [] 0 0
    let activity = makeActivity "Test" (DateTime.UtcNow) "drinks"
    let aa = makeActivityAttendee host activity true
    let dto = mapUserActivityDto aa
    dto.HostUsername |> should equal "bob"

[<Fact>]
let ``mapUserActivityDto returns empty host username when no host found`` () =
    let guest = makeUser "jane" "Jane" "" [] 0 0
    let activity = makeActivity "Test" (DateTime.UtcNow) "drinks"
    let aa = makeActivityAttendee guest activity false
    let dto = mapUserActivityDto aa
    // No attendee has IsHost=true, so HostUsername defaults to empty
    dto.HostUsername |> should equal ""

[<Fact>]
let ``mapUserActivityDto maps date correctly`` () =
    let expectedDate = DateTime(2025, 6, 15, 10, 0, 0, DateTimeKind.Utc)
    let host = makeUser "bob" "Bob" "" [] 0 0
    let activity = makeActivity "Concert" expectedDate "music"
    let aa = makeActivityAttendee host activity true
    let dto = mapUserActivityDto aa
    dto.Date |> should equal expectedDate
