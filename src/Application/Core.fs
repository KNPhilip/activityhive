namespace Application.Core

open System
open System.Linq
open Microsoft.EntityFrameworkCore

type AppException(statusCode: int, message: string, ?details: string) =
    member _.StatusCode = statusCode
    member _.Message = message
    member _.Details = defaultArg details ""

type ServiceResponse<'T>() =
    member val Success: bool = false with get, set
    member val Data: 'T = Unchecked.defaultof<'T> with get, set
    member val Error: string = null with get, set

module ServiceResponse =
    let success (data: 'T) : ServiceResponse<'T> =
        let r = ServiceResponse<'T>()
        r.Success <- true
        r.Data <- data
        r

    let failure<'T> (error: string) : ServiceResponse<'T> =
        let r = ServiceResponse<'T>()
        r.Success <- false
        r.Error <- error
        r

type PagingParams() =
    let mutable _pageNumber = 1
    let mutable _pageSize = 10
    let maxPageSize = 50

    member _.PageNumber
        with get() = _pageNumber
        and set(v) = if v > 0 then _pageNumber <- v

    member _.PageSize
        with get() = _pageSize
        and set(v) = _pageSize <- if v > maxPageSize then maxPageSize else v

type PagedList<'T>(items: 'T seq, count: int, pageNumber: int, pageSize: int) =
    inherit System.Collections.Generic.List<'T>(items)

    member _.CurrentPage = pageNumber
    member _.TotalPages = int (Math.Ceiling(float count / float pageSize))
    member _.PageSize = pageSize
    member _.TotalCount = count

    static member CreateAsync(source: IQueryable<'T>, pageNumber: int, pageSize: int) =
        task {
            let! count = source.CountAsync()
            let! items =
                source
                    .Skip((pageNumber - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync()
            return PagedList<'T>(items, count, pageNumber, pageSize)
        }
