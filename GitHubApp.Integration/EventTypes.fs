namespace GitHubApp.Integration

open FSharp.Data

module CheckRunEvent =
    let [<Literal>] EventTag = "check_run"
    let [<Literal>] Path = __SOURCE_DIRECTORY__ + "\SampleJsons\CheckRunEvent.json"

    type Reader = JsonProvider<Path>

    let getSample () =
        Reader.GetSample ()

    let parse payload =
        Reader.Parse payload

module CheckSuiteEvent =
    let [<Literal>] EventTag = "check_suite"
    let [<Literal>] Path = __SOURCE_DIRECTORY__ + "\SampleJsons\CheckSuiteEvent.json"

    type Reader = JsonProvider<Path>

    let getSample () =
        Reader.GetSample ()

    let parse payload =
        Reader.Parse payload
