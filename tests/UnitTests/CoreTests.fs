module CoreTests

open System
open System.Linq
open Application.Core
open Xunit
open FsUnit.Xunit

// ---------------------------------------------------------------------------
// ServiceResponse helpers
// ---------------------------------------------------------------------------

[<Fact>]
let ``ServiceResponse success sets Success to true and Data correctly`` () =
    let response = ServiceResponse.success 42
    response.Success |> should equal true
    response.Data |> should equal 42
    response.Error |> should equal null

[<Fact>]
let ``ServiceResponse failure sets Success to false and Error correctly`` () =
    let response = ServiceResponse.failure<int> "Something went wrong"
    response.Success |> should equal false
    response.Error |> should equal "Something went wrong"

[<Fact>]
let ``ServiceResponse success with unit type works`` () =
    let response = ServiceResponse.success ()
    response.Success |> should equal true

[<Fact>]
let ``ServiceResponse failure preserves error message`` () =
    let msg = "Activity not found."
    let response = ServiceResponse.failure<string> msg
    response.Error |> should equal msg

[<Fact>]
let ``ServiceResponse success with string works`` () =
    let response = ServiceResponse.success "hello"
    response.Success |> should equal true
    response.Data |> should equal "hello"

// ---------------------------------------------------------------------------
// PagingParams
// ---------------------------------------------------------------------------

[<Fact>]
let ``PagingParams defaults to page 1 and size 10`` () =
    let p = PagingParams()
    p.PageNumber |> should equal 1
    p.PageSize |> should equal 10

[<Fact>]
let ``PagingParams clamps page size to 50 max`` () =
    let p = PagingParams()
    p.PageSize <- 100
    p.PageSize |> should equal 50

[<Fact>]
let ``PagingParams accepts page size at max boundary`` () =
    let p = PagingParams()
    p.PageSize <- 50
    p.PageSize |> should equal 50

[<Fact>]
let ``PagingParams accepts page size below max`` () =
    let p = PagingParams()
    p.PageSize <- 25
    p.PageSize |> should equal 25

[<Fact>]
let ``PagingParams ignores non-positive page number`` () =
    let p = PagingParams()
    p.PageNumber <- 0
    p.PageNumber |> should equal 1

[<Fact>]
let ``PagingParams ignores negative page number`` () =
    let p = PagingParams()
    p.PageNumber <- -5
    p.PageNumber |> should equal 1

[<Fact>]
let ``PagingParams accepts positive page number`` () =
    let p = PagingParams()
    p.PageNumber <- 3
    p.PageNumber |> should equal 3

// ---------------------------------------------------------------------------
// PagedList
// ---------------------------------------------------------------------------

[<Fact>]
let ``PagedList TotalPages calculates correctly for exact division`` () =
    let items = [1; 2; 3; 4; 5; 6; 7; 8; 9; 10]
    let paged = PagedList<int>(items, 10, 1, 5)
    paged.TotalPages |> should equal 2

[<Fact>]
let ``PagedList TotalPages rounds up for partial page`` () =
    let items = [1; 2; 3]
    let paged = PagedList<int>(items, 11, 1, 5)
    paged.TotalPages |> should equal 3

[<Fact>]
let ``PagedList CurrentPage matches provided page number`` () =
    let items = [1; 2]
    let paged = PagedList<int>(items, 20, 3, 5)
    paged.CurrentPage |> should equal 3

[<Fact>]
let ``PagedList TotalCount reflects underlying count`` () =
    let items = [1]
    let paged = PagedList<int>(items, 100, 1, 10)
    paged.TotalCount |> should equal 100

[<Fact>]
let ``PagedList PageSize reflects provided page size`` () =
    let items = [1; 2; 3]
    let paged = PagedList<int>(items, 3, 1, 3)
    paged.PageSize |> should equal 3

[<Fact>]
let ``PagedList with one page returns TotalPages of 1`` () =
    let items = [1; 2; 3]
    let paged = PagedList<int>(items, 3, 1, 10)
    paged.TotalPages |> should equal 1

[<Fact>]
let ``PagedList with empty items still works`` () =
    let paged = PagedList<int>([], 0, 1, 10)
    paged.TotalCount |> should equal 0
    paged.TotalPages |> should equal 0

// ---------------------------------------------------------------------------
// AppException
// ---------------------------------------------------------------------------

[<Fact>]
let ``AppException stores status code and message`` () =
    let ex = AppException(404, "Not found")
    ex.StatusCode |> should equal 404
    ex.Message |> should equal "Not found"
    ex.Details |> should equal ""

[<Fact>]
let ``AppException stores details when provided`` () =
    let ex = AppException(500, "Server Error", "Stack trace here")
    ex.Details |> should equal "Stack trace here"
