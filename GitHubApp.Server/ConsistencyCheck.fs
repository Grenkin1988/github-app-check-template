module GitHubApp.Server.ConsistencyCheck

open GitHubApp.Core
open GitHubApp.Core.Interfaces
open GitHubApp.Checks
open System.Diagnostics
open Octokit
open System

let runAllChecks repoPath =
    let context = ConsistencyContext(repoPath)

    let checks : ConsistencyCheckBase list = [
        BannedFiles(context)
    ]

    let runCheck (check:ConsistencyCheckBase) =
        let sw = Stopwatch.StartNew();
        try
            let result = check.Run()
            Logging.logDebug "Ran '%s' in %ims" check.Name sw.ElapsedMilliseconds
            result
        with ex ->
            Logging.logEx ex "Execution failed for '%s'" check.Name
            {
                Name = check.Name
                Message = ex.Message
                Exception = ex
            } |> ExceptionCaught

    let results =
        checks
        |> List.map runCheck
    results

type AggregatedProblems = { ProblemsFound:int; ExceptionFound: int }

type AggregatedConsistencyCheckResult =
    | NoProblemsFound
    | SomeProblemsFound of AggregatedProblems

let hasProblems results =
    results |> List.exists (function Passed -> false | ProblemsFound _ | ExceptionCaught _ -> true)

let aggregateProblems results =
    let aggregated =
        ({ ProblemsFound = 0; ExceptionFound = 0 }, results)
        ||> List.fold (fun state result -> 
                            match result with
                            | Passed -> state 
                            | ProblemsFound problem -> { state with ProblemsFound = state.ProblemsFound + problem.ProblemLines.Length}
                            | ExceptionCaught _ -> { state with ExceptionFound = state.ExceptionFound + 1})
    match aggregated with
    | { ProblemsFound = 0; ExceptionFound = 0 } -> NoProblemsFound
    | aggregated -> SomeProblemsFound aggregated

let getSummary = function
    | NoProblemsFound -> sprintf "%s haven't found any issues" GlobalConfiguration.CheckRunName
    | SomeProblemsFound aggregated -> sprintf "%s have found issues: Problems: %i, Exceptions: %i" GlobalConfiguration.CheckRunName aggregated.ProblemsFound aggregated.ExceptionFound

let getAnnotations results =
    let problemToAnotation (problems:ConsistencyCheckProblem) =
        let startLine = 1
        let endLine = 2
        let getLine problem =
            let path = if String.IsNullOrEmpty problem.FileName then "FileNotSpecified" else problem.FileName
            let message = sprintf "%s%s%s" problems.Header Environment.NewLine problem.Message
            let annotation = NewCheckRunAnnotation(path, startLine, endLine, CheckAnnotationLevel.Failure, message)
            annotation.Title <- problems.Name
            annotation
        problems.ProblemLines 
        |> Array.map getLine

    results
    |> List.choose (function ProblemsFound problem -> problem |> problemToAnotation |> Some | _ -> None)
    |> List.collect (fun array -> List.ofArray array)
    |> List.distinct
