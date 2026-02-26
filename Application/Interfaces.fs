namespace Application.Interfaces

open System.Threading.Tasks
open Microsoft.AspNetCore.Http

type PhotoUploadResult =
    { PublicId: string
      Url: string }

type IPhotoAccessor =
    abstract member AddPhoto: IFormFile -> Task<PhotoUploadResult option>
    abstract member DeletePhoto: string -> Task<string option>

type IUserAccessor =
    abstract member GetUsername: unit -> string
