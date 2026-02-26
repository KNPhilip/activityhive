namespace Application.Core

open Domain
open AutoMapper

type MappingProfiles() =
    inherit Profile()
    do
        base.CreateMap<Activity, Activity>() |> ignore
