namespace GitHubApp.Core

open FSharp.Data

module Settings =
    type Reader = JsonProvider<"../GitHubApp.Core/Settings/settings.json">

    let getSample () =
        Reader.GetSample ()

    let parse payload =
        Reader.Parse payload
