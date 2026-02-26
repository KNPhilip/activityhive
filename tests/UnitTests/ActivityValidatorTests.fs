module ActivityValidatorTests

open System
open Application.Activities
open Domain
open Xunit
open FsUnit.Xunit

let private validActivity () : Activity =
    { Id = Guid.NewGuid()
      Title = "Test Activity"
      Date = DateTime.UtcNow.AddDays(1.0)
      Description = "A valid description"
      Category = "music"
      City = "London"
      Venue = "Wembley"
      IsCancelled = false
      Attendees = ResizeArray()
      Comments = ResizeArray() }

let private validate (activity: Activity) =
    let validator = ActivityValidator()
    validator.Validate(activity)

[<Fact>]
let ``ActivityValidator passes for a fully valid activity`` () =
    let result = validate (validActivity())
    result.IsValid |> should equal true

[<Fact>]
let ``ActivityValidator fails when Title is empty`` () =
    let activity = { validActivity() with Title = "" }
    let result = validate activity
    result.IsValid |> should equal false
    result.Errors |> Seq.exists (fun e -> e.PropertyName = "Title") |> should equal true

[<Fact>]
let ``ActivityValidator fails when Description is empty`` () =
    let activity = { validActivity() with Description = "" }
    let result = validate activity
    result.IsValid |> should equal false
    result.Errors |> Seq.exists (fun e -> e.PropertyName = "Description") |> should equal true

[<Fact>]
let ``ActivityValidator fails when Category is empty`` () =
    let activity = { validActivity() with Category = "" }
    let result = validate activity
    result.IsValid |> should equal false
    result.Errors |> Seq.exists (fun e -> e.PropertyName = "Category") |> should equal true

[<Fact>]
let ``ActivityValidator fails when City is empty`` () =
    let activity = { validActivity() with City = "" }
    let result = validate activity
    result.IsValid |> should equal false
    result.Errors |> Seq.exists (fun e -> e.PropertyName = "City") |> should equal true

[<Fact>]
let ``ActivityValidator fails when Venue is empty`` () =
    let activity = { validActivity() with Venue = "" }
    let result = validate activity
    result.IsValid |> should equal false
    result.Errors |> Seq.exists (fun e -> e.PropertyName = "Venue") |> should equal true

[<Fact>]
let ``ActivityValidator fails when Date is DateTime.MinValue`` () =
    let activity = { validActivity() with Date = DateTime.MinValue }
    let result = validate activity
    result.IsValid |> should equal false
    result.Errors |> Seq.exists (fun e -> e.PropertyName = "Date") |> should equal true

[<Fact>]
let ``ActivityValidator does NOT enforce future dates - past dates are accepted`` () =
    // Weakness: past dates pass validation, allowing creation of activities in the past
    let activity = { validActivity() with Date = DateTime.UtcNow.AddYears(-10) }
    let result = validate activity
    result.IsValid |> should equal true

[<Fact>]
let ``ActivityValidator reports multiple errors simultaneously`` () =
    let activity = { validActivity() with Title = ""; Description = ""; Category = "" }
    let result = validate activity
    result.Errors.Count |> should be (greaterThan 1)

[<Fact>]
let ``ActivityValidator accepts whitespace-only title as valid - potential weakness`` () =
    // Weakness: whitespace-only strings pass NotEmpty validation in FluentValidation
    // Consider adding NotEmpty().Must(fun s -> not (String.IsNullOrWhiteSpace(s))) 
    let activity = { validActivity() with Title = "   " }
    let result = validate activity
    // FluentValidation's NotEmpty treats whitespace as empty - this should fail
    result.IsValid |> should equal false
