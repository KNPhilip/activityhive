namespace API.Dtos

[<CLIMutable>]
type LoginDto =
    { mutable Email: string
      mutable Password: string }

[<CLIMutable>]
type RegisterDto =
    { mutable Email: string
      mutable Password: string
      mutable DisplayName: string
      mutable Username: string }

[<CLIMutable>]
type UserDto =
    { mutable DisplayName: string
      mutable Token: string
      mutable Image: string
      mutable Username: string }

[<CLIMutable>]
type FacebookPictureDataDto =
    { mutable Url: string }

[<CLIMutable>]
type FacebookPictureDto =
    { mutable Data: FacebookPictureDataDto }

[<CLIMutable>]
type FacebookDto =
    { mutable Id: string
      mutable Email: string
      mutable Name: string
      mutable Picture: FacebookPictureDto }
