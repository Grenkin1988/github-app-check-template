module GitHubApp.Server.Program

open Suave
open Suave.Logging
open GitHubApp.Core
open System.Threading
open System.IO
open GitHubApp.Server

let runLocalConsistency directory =
    if Directory.Exists directory then
        let result = ConsistencyCheck.runAllChecks directory
        if ConsistencyCheck.hasProblems result then
            result
            |> List.iter (fun x -> Logging.logInfo "%A" x)
        else
            Logging.logInfo "Done! No problems found"
    else
        Logging.logWarn "Target folder not found. Folder %s" directory

let startConsistencyServer () =
    Logging.logInfo "Initialising server..."

    let cts = new CancellationTokenSource()

    let fileLogger = 
        { 
            new Logger with
                member __.log level apply =
                    match level with
                    | LogLevel.Fatal
                    | LogLevel.Error -> Logging.logError "Error: %O" (apply level)
                    | LogLevel.Warn -> Logging.logWarn "Warning: %O" (apply level)
                    | LogLevel.Info -> Logging.logInfo "Info: %O" (apply level)
                    | LogLevel.Debug
                    | LogLevel.Verbose -> ()
                member this.logWithAck level apply = async {
                        return this.log level apply
                    }
                member __.name = [| "OM consistency logger" |]
        }

    let ipAddress = "0.0.0.0"
    let port = GlobalConfiguration.WebserverPort

    let cfg = 
        { 
            defaultConfig with
                cancellationToken = cts.Token
                bindings = [ HttpBinding.createSimple HTTP ipAddress port ]
                logger = CombiningTarget([|"combine"|], [ defaultConfig.logger; fileLogger ])
        }
    Server.start cfg

[<EntryPoint>]
let main argv = 
    try
        Logging.logInfo "Starting Consistency..."
        Logging.logDebug "Args: %A" argv

        match argv |> Array.truncate 1 with
        | [| directory |] -> 
            runLocalConsistency directory
        | _ -> startConsistencyServer()
        0
    with ex ->
        Logging.logError "Exception occurs: %O" ex
        1
