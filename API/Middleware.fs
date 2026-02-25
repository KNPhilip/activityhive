namespace API

open System.Net
open System.Text.Json
open Application.Core
open Microsoft.AspNetCore.Http
open Microsoft.Extensions.Hosting
open Microsoft.Extensions.Logging

type ExceptionMiddleware
    (next: RequestDelegate, logger: ILogger<ExceptionMiddleware>, env: IHostEnvironment) =

    member _.InvokeAsync(context: HttpContext) =
        task {
            try
                do! next.Invoke(context)
            with ex ->
                logger.LogError(ex, ex.Message)
                context.Response.ContentType <- "application/json"
                context.Response.StatusCode <- int HttpStatusCode.InternalServerError

                let response =
                    if env.IsDevelopment() then
                        AppException(context.Response.StatusCode, ex.Message, ex.StackTrace)
                    else
                        AppException(context.Response.StatusCode, "Internal Server Error")

                let options =
                    JsonSerializerOptions(PropertyNamingPolicy = JsonNamingPolicy.CamelCase)

                let json = JsonSerializer.Serialize(response, options)
                do! context.Response.WriteAsync(json)
        }
        :> System.Threading.Tasks.Task
