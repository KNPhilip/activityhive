namespace Application.Core

open System
open Domain
open Application.Activities
open Application.Comments
open Application.Profiles
open AutoMapper

type MappingProfiles() =
    inherit Profile()
    do
        let mutable currentUsername: string = Unchecked.defaultof<string>

        base.CreateMap<Activity, Activity>() |> ignore

        base.CreateMap<Activity, ActivityDto>()
            .ForMember(
                "HostUsername",
                fun o ->
                    o.MapFrom<string>(Func<Activity, ActivityDto, string>(fun s _ ->
                        s.Attendees
                        |> Seq.tryFind (fun x -> x.IsHost)
                        |> Option.bind (fun x -> if isNull (box x.User) then None else Some x.User.UserName)
                        |> Option.defaultValue "")))
        |> ignore

        base.CreateMap<ActivityAttendee, AttendeeDto>()
            .ForMember("DisplayName", fun o -> o.MapFrom<string>(Func<ActivityAttendee, AttendeeDto, string>(fun s _ -> s.User.DisplayName)))
            .ForMember("Username",    fun o -> o.MapFrom<string>(Func<ActivityAttendee, AttendeeDto, string>(fun s _ -> s.User.UserName)))
            .ForMember("Bio",         fun o -> o.MapFrom<string>(Func<ActivityAttendee, AttendeeDto, string>(fun s _ -> s.User.Bio)))
            .ForMember("Image", fun o ->
                o.MapFrom<string>(Func<ActivityAttendee, AttendeeDto, string>(fun s _ ->
                    s.User.Photos
                    |> Seq.tryFind (fun x -> x.IsMain)
                    |> Option.map (fun x -> x.Url)
                    |> Option.defaultValue "")))
            .ForMember("FollowersCount", fun o -> o.MapFrom<int>(Func<ActivityAttendee, AttendeeDto, int>(fun s _ -> s.User.Followers |> Seq.length)))
            .ForMember("FollowingCount", fun o -> o.MapFrom<int>(Func<ActivityAttendee, AttendeeDto, int>(fun s _ -> s.User.Followings |> Seq.length)))
            .ForMember("Following", fun o ->
                o.MapFrom<bool>(Func<ActivityAttendee, AttendeeDto, bool>(fun s _ ->
                    s.User.Followers
                    |> Seq.exists (fun x -> not (isNull (box x.Observer)) && x.Observer.UserName = currentUsername))))
        |> ignore

        base.CreateMap<User, UserProfile>()
            .ForMember("Image", fun o ->
                o.MapFrom<string>(Func<User, UserProfile, string>(fun s _ ->
                    s.Photos
                    |> Seq.tryFind (fun x -> x.IsMain)
                    |> Option.map (fun x -> x.Url)
                    |> Option.defaultValue "")))
            .ForMember("FollowersCount", fun o -> o.MapFrom<int>(Func<User, UserProfile, int>(fun s _ -> s.Followers |> Seq.length)))
            .ForMember("FollowingCount", fun o -> o.MapFrom<int>(Func<User, UserProfile, int>(fun s _ -> s.Followings |> Seq.length)))
            .ForMember("Following", fun o ->
                o.MapFrom<bool>(Func<User, UserProfile, bool>(fun s _ ->
                    s.Followers
                    |> Seq.exists (fun x -> not (isNull (box x.Observer)) && x.Observer.UserName = currentUsername))))
        |> ignore

        base.CreateMap<Comment, CommentDto>()
            .ForMember("DisplayName", fun o -> o.MapFrom<string>(Func<Comment, CommentDto, string>(fun s _ -> s.Author.DisplayName)))
            .ForMember("Username",    fun o -> o.MapFrom<string>(Func<Comment, CommentDto, string>(fun s _ -> s.Author.UserName)))
            .ForMember("Image", fun o ->
                o.MapFrom<string>(Func<Comment, CommentDto, string>(fun s _ ->
                    s.Author.Photos
                    |> Seq.tryFind (fun x -> x.IsMain)
                    |> Option.map (fun x -> x.Url)
                    |> Option.defaultValue "")))
        |> ignore

        base.CreateMap<ActivityAttendee, UserActivityDto>()
            .ForMember("Id",       fun o -> o.MapFrom<Guid>(Func<ActivityAttendee, UserActivityDto, Guid>(fun s _ -> s.Activity.Id)))
            .ForMember("Date",     fun o -> o.MapFrom<DateTime>(Func<ActivityAttendee, UserActivityDto, DateTime>(fun s _ -> s.Activity.Date)))
            .ForMember("Title",    fun o -> o.MapFrom<string>(Func<ActivityAttendee, UserActivityDto, string>(fun s _ -> s.Activity.Title)))
            .ForMember("Category", fun o -> o.MapFrom<string>(Func<ActivityAttendee, UserActivityDto, string>(fun s _ -> s.Activity.Category)))
            .ForMember("HostUsername", fun o ->
                o.MapFrom<string>(Func<ActivityAttendee, UserActivityDto, string>(fun s _ ->
                    s.Activity.Attendees
                    |> Seq.tryFind (fun x -> x.IsHost)
                    |> Option.bind (fun x -> if isNull (box x.User) then None else Some x.User.UserName)
                    |> Option.defaultValue "")))
        |> ignore
