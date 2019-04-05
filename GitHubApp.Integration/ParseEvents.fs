namespace GitHubApp.Integration

module ParseEvents =
    type GitHubEvent =
        {
            Type : string
            Payload : string
        }

    let (|CheckRunEvent|_|) { Type = event; Payload = payload } =
        match event with
        | CheckRunEvent.EventTag -> 
            CheckRunEvent.parse payload |> Some 
        | _ -> None

    let (|CheckSuiteEvent|_|) { Type = event; Payload = payload } =
        match event with
        | CheckSuiteEvent.EventTag -> 
            CheckSuiteEvent.parse payload |> Some 
        | _ -> None
