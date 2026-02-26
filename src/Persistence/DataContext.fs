namespace Persistence

open System
open Domain
open Microsoft.AspNetCore.Identity.EntityFrameworkCore
open Microsoft.EntityFrameworkCore
open Microsoft.EntityFrameworkCore.Storage.ValueConversion

type DataContext(options: DbContextOptions) =
    inherit IdentityDbContext<User>(options)

    member this.Activities = this.Set<Activity>()
    member this.ActivityAttendees = this.Set<ActivityAttendee>()
    member this.Photos = this.Set<Photo>()
    member this.Comments = this.Set<Comment>()
    member this.UserFollowings = this.Set<UserFollowing>()

    override this.OnModelCreating(builder: ModelBuilder) =
        base.OnModelCreating(builder)

        // Value converter for F# option<DateTime> → Nullable<DateTime>
        // Required for PostgreSQL which does not automatically handle F# option types.
        let dateTimeOptionConverter =
            ValueConverter<DateTime option, Nullable<DateTime>>(
                (fun d -> match d with Some v -> Nullable(v) | None -> Nullable()),
                (fun d -> if d.HasValue then Some d.Value else None)
            )

        builder.Entity<RefreshToken>()
            .Property(fun r -> r.Revoked)
            .HasConversion(dateTimeOptionConverter)
        |> ignore

        builder.Entity<ActivityAttendee>()
            .HasKey([| "UserId"; "ActivityId" |])
        |> ignore

        builder.Entity<ActivityAttendee>()
            .HasOne(fun u -> u.User)
            .WithMany(fun (a: User) -> a.Activities :> System.Collections.Generic.IEnumerable<ActivityAttendee>)
            .HasForeignKey([| "UserId" |])
        |> ignore

        builder.Entity<ActivityAttendee>()
            .HasOne(fun u -> u.Activity)
            .WithMany(fun (a: Activity) -> a.Attendees :> System.Collections.Generic.IEnumerable<ActivityAttendee>)
            .HasForeignKey([| "ActivityId" |])
        |> ignore

        builder.Entity<Comment>()
            .HasOne(fun a -> a.Activity)
            .WithMany(fun (c: Activity) -> c.Comments :> System.Collections.Generic.IEnumerable<Comment>)
            .OnDelete(DeleteBehavior.Cascade)
        |> ignore

        builder.Entity<UserFollowing>()
            .HasKey([| "ObserverId"; "TargetId" |])
        |> ignore

        builder.Entity<UserFollowing>()
            .HasOne(fun o -> o.Observer)
            .WithMany(fun (f: User) -> f.Followings :> System.Collections.Generic.IEnumerable<UserFollowing>)
            .HasForeignKey([| "ObserverId" |])
            .OnDelete(DeleteBehavior.Cascade)
        |> ignore

        builder.Entity<UserFollowing>()
            .HasOne(fun o -> o.Target)
            .WithMany(fun (f: User) -> f.Followers :> System.Collections.Generic.IEnumerable<UserFollowing>)
            .HasForeignKey([| "TargetId" |])
            .OnDelete(DeleteBehavior.Cascade)
        |> ignore
