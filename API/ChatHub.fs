namespace API.SignalR

open System
open Application.Comments
open Application.Core
open MediatR
open Microsoft.AspNetCore.SignalR

type ChatHub(mediator: IMediator) =
    inherit Hub()

    member this.SendComment(command: Create.Command) =
        let clients = this.Clients
        let activityId = command.ActivityId
        task {
            let! response = mediator.Send(command)
            let comment = response.Data
            do! clients.Group(activityId.ToString()).SendAsync("ReceiveComment", comment)
        }

    override this.OnConnectedAsync() =
        let ctx = this.Context.GetHttpContext()
        let activityId = ctx.Request.Query["activityId"]
        let groups = this.Groups
        let connectionId = this.Context.ConnectionId
        let callerClient = this.Clients.Caller

        task {
            do! groups.AddToGroupAsync(connectionId, activityId.ToString())

            let q = List.Query()
            q.ActivityId <- Guid.Parse(activityId.ToString())
            let! result = mediator.Send(q)

            do! callerClient.SendAsync("LoadComments", result.Data)
        }
        :> System.Threading.Tasks.Task
