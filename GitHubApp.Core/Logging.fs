module GitHubApp.Core.Logging

open System

let logInfo msg =
    Printf.ksprintf (fun msg ->
        printfn "[%O] [Info] %s" DateTime.UtcNow msg) msg

let logWarn msg =
    Printf.ksprintf (fun msg ->
        printfn "[%O] [Warn] %s" DateTime.UtcNow msg) msg

let logDebug msg =
    Printf.ksprintf (fun msg ->
        printfn "[%O] [Debug] %s" DateTime.UtcNow msg) msg

let logError msg =
    Printf.ksprintf (fun msg ->
        printfn "[%O] [Error] %s" DateTime.UtcNow msg) msg

let logEx (ex:System.Exception) msg =
    Printf.ksprintf (fun msg ->
        printfn "[%O] [Error] %s %O" DateTime.UtcNow msg ex) msg
