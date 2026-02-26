namespace Persistence

open System
open System.Linq
open Domain
open Microsoft.AspNetCore.Identity

module Seed =
    let private mkActivity title date desc cat city venue (hostUser: User) (attendees: (User * bool) list) =
        let id = Guid.NewGuid()
        let activity =
            { Id = id
              Title = title
              Date = date
              Description = desc
              Category = cat
              City = city
              Venue = venue
              IsCancelled = false
              Attendees = ResizeArray()
              Comments = ResizeArray() }
        for (user, isHost) in (hostUser, true) :: attendees do
            activity.Attendees.Add(
                { UserId = user.Id
                  User = user
                  ActivityId = id
                  Activity = activity
                  IsHost = isHost })
        activity

    let seedData (context: DataContext) (userManager: UserManager<User>) =
        task {
            if not (userManager.Users.Any()) && not (context.Activities.Any()) then
                let users = [
                    User(DisplayName = "Bob", UserName = "bob", Email = "bob@test.com")
                    User(DisplayName = "Jane", UserName = "jane", Email = "jane@test.com")
                    User(DisplayName = "Tom", UserName = "tom", Email = "tom@test.com")
                ]

                for user in users do
                    let! result = userManager.CreateAsync(user, "Pa$$w0rd")
                    if not result.Succeeded then
                        let errors = result.Errors |> Seq.map (fun e -> e.Description) |> String.concat ", "
                        failwith $"Failed to create seed user '{user.UserName}': {errors}"

                let bob = users.[0]
                let jane = users.[1]
                let tom = users.[2]

                let activities = [
                    mkActivity "Past Activity 1"   (DateTime.UtcNow.AddMonths(-2)) "Activity 2 months ago"          "drinks"  "London"  "Pub"              bob []
                    mkActivity "Past Activity 2"   (DateTime.UtcNow.AddMonths(-1)) "Activity 1 month ago"           "culture" "Paris"   "The Louvre"       bob [(jane, false)]
                    mkActivity "Future Activity 1" (DateTime.UtcNow.AddMonths(1))  "Activity 1 month in future"     "music"   "London"  "Wembley Stadium"  tom [(jane, false)]
                    mkActivity "Future Activity 2" (DateTime.UtcNow.AddMonths(2))  "Activity 2 months in future"    "food"    "London"  "Jamies Italian"   bob [(tom, false)]
                    mkActivity "Future Activity 3" (DateTime.UtcNow.AddMonths(3))  "Activity 3 months in future"    "drinks"  "London"  "Pub"              jane [(bob, false)]
                    mkActivity "Future Activity 4" (DateTime.UtcNow.AddMonths(4))  "Activity 4 months in future"    "culture" "London"  "British Museum"   jane []
                    mkActivity "Future Activity 5" (DateTime.UtcNow.AddMonths(5))  "Activity 5 months in future"    "drinks"  "London"  "Punch and Judy"   bob [(jane, false)]
                    mkActivity "Future Activity 6" (DateTime.UtcNow.AddMonths(6))  "Activity 6 months in future"    "music"   "London"  "O2 Arena"         tom [(jane, false)]
                    mkActivity "Future Activity 7" (DateTime.UtcNow.AddMonths(7))  "Activity 7 months in future"    "travel"  "Berlin"  "All"              bob [(tom, false)]
                    mkActivity "Future Activity 8" (DateTime.UtcNow.AddMonths(8))  "Activity 8 months in future"    "drinks"  "London"  "Pub"              tom [(jane, false)]
                ]

                do! context.Activities.AddRangeAsync(activities)
                let! _ = context.SaveChangesAsync()
                ()
        }
