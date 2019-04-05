module GitHubApp.Server.GitHubCheckRunActor

open GitHubApp.Core
open GitHubApp.Core.Interfaces
open System
open Octokit
open LibGit2Sharp
open System.IO
open System.Diagnostics
open System.Collections.ObjectModel

type CheckRunOutput =
    {
        CheckConclusion: CheckConclusion
        Title: string
        Summary: string
        Annotations: NewCheckRunAnnotation list
    }

let gitHubCheckRunActor (getClient:GetGitHubClient) (checkStatusActor:IGitHubCheckStatusActor) =
    let defaultOutput =
        {
            CheckConclusion = CheckConclusion.Neutral
            Title = GlobalConfiguration.CheckRunName
            Summary = ""
            Annotations = []
        }

    let ensureLocalWorkspaceExists checkRunParameters (token:AccessToken) = 
        let repoPath = Path.Combine(GlobalConfiguration.RepoPath, checkRunParameters.RepositoryName)
        if not <| Directory.Exists(Path.Combine(repoPath, ".git")) then
            Logging.logWarn "Repository does not exist in %s. Fetching it now... (this may take some time)" repoPath
            
            Directory.CreateDirectory(repoPath) |> ignore
            let repository = sprintf "%s/%s.git" GlobalConfiguration.GithubUri checkRunParameters.RepositoryFullName
            Logging.logWarn "Fetching it now from %s (this may take some time)" repository
            let url = sprintf "https://x-access-token:%s@%s" token.Token repository
            Repository.Clone(url, repoPath) |> ignore
        repoPath

    let prepareLocalWorkspace checkRunParameters (token:AccessToken) repoPath =
        let getCredentialsProvider _ _ _ =
            let cread = LibGit2Sharp.UsernamePasswordCredentials()
            cread.Username <- "x-access-token"
            cread.Password <- token.Token
            cread :> LibGit2Sharp.Credentials

        use repo = new Repository(repoPath)

        let fetchRemote (remote:Remote) =
            let refSpecs = 
                remote.FetchRefSpecs
                |> Seq.map (fun spec -> spec.Specification)
            let option = FetchOptions()
            option.TagFetchMode <- Nullable<_>(TagFetchMode.None)
            option.CredentialsProvider <- Handlers.CredentialsHandler(getCredentialsProvider)
            Commands.Fetch(repo, remote.Name, refSpecs, option, "")

        let sw = Stopwatch.StartNew();
        repo.Network.Remotes
        |> Seq.iter fetchRemote

        sw.Stop()
        Logging.logDebug "Fetched updated refs in %i ms" sw.ElapsedMilliseconds
        let targetSha = checkRunParameters.HeadSha
        let sw = Stopwatch.StartNew();
        let branch = 
            repo.Branches
            |> Seq.choose (fun branch -> if branch.Tip.Sha = targetSha then Some branch else None)
            |> Seq.first
        match branch with
        | None ->
            sprintf "Attempted to find branch with Tip.Sha=%s but was not found!" targetSha
            |> InvalidOperationException
            |> raise
        | Some branch ->
            Commands.Checkout(repo, branch) |> ignore
            sw.Stop()
            Logging.logDebug "Checked out %s in %i ms" branch.FriendlyName sw.ElapsedMilliseconds
            let sw = Stopwatch.StartNew();
            repo.Reset(ResetMode.Hard, branch.Tip)
            Logging.logDebug "Reset to %s in %i ms" branch.FriendlyName sw.ElapsedMilliseconds
            sw.Stop()
            repoPath

    let executeChecks checkRunParameters =
        let (_, token) = getClient checkRunParameters.InstallationId
        let repoPath = 
            ensureLocalWorkspaceExists checkRunParameters token
            |> prepareLocalWorkspace checkRunParameters token

        let results = ConsistencyCheck.runAllChecks repoPath
        let output = { defaultOutput with Summary = results |> ConsistencyCheck.aggregateProblems |> ConsistencyCheck.getSummary }
        if results |> ConsistencyCheck.hasProblems then
            let annotations = ConsistencyCheck.getAnnotations results
            let annotations =
                if annotations.Length > 50 then //Output do not allow more than 50 annotations
                    annotations |> List.take 50
                else annotations
            { output with CheckConclusion = CheckConclusion.Failure; Annotations = annotations }
        else
            { output with CheckConclusion = CheckConclusion.Success }

    let getInProgressRunUpdate () =
        let update = CheckRunUpdate()
        let time = System.DateTime.UtcNow
        update.Status <- System.Nullable<_>(StringEnum(CheckStatus.InProgress))
        update.StartedAt <- System.Nullable<_>(System.DateTimeOffset(time))
        update, time

    let getCompletedRunUpdate checkRunOutput =
        let update = CheckRunUpdate()
        let time = System.DateTime.UtcNow
        update.Status <- System.Nullable<_>(StringEnum(CheckStatus.Completed))
        update.CompletedAt <- System.Nullable<_>(System.DateTimeOffset(time))
        update.Conclusion <- System.Nullable<_>(StringEnum(checkRunOutput.CheckConclusion))
        let output = NewCheckRunOutput(checkRunOutput.Title, checkRunOutput.Summary)
        output.Annotations <- ReadOnlyCollection(ResizeArray(checkRunOutput.Annotations))
        update.Output <- output
        update, time

    let runCheck (checkRunParameters:CheckRunParameters) =
        let updateCheckRun update =
            (checkRunParameters, update)
            |> UpdateCheck
            |> checkStatusActor.Post

        let (update, time) = getInProgressRunUpdate()
        let time = time.ToString("MM/dd/yyyy HH:mm:ss")
        Logging.logInfo "CheckRun %i Started at %s" checkRunParameters.CheckRunId time

        updateCheckRun update

        let output = executeChecks checkRunParameters

        let (update, time) = getCompletedRunUpdate output
        let time = time.ToString("MM/dd/yyyy HH:mm:ss")
        Logging.logInfo "CheckRun %i Completed at %s" checkRunParameters.CheckRunId time

        updateCheckRun update

    let router = new MailboxProcessor<_>(fun (inbox:MailboxProcessor<CheckRunMessage>) ->
        let rec loop () = async {
            let! msg = inbox.Receive()
            match msg with
            | RunCheck checkRunParameters ->
                runCheck checkRunParameters
            return! loop () }

        loop ())
    let subscription = router.Error.Subscribe (fun ex -> Logging.logEx ex "Exception in GitHubCheckRunActor")
    router.Start()

    {
        new IGitHubCheckRunActor with
            member __.Post msg = router.Post msg
            member __.CurrentQueueLength = router.CurrentQueueLength
            member __.Dispose () =
                subscription.Dispose()
                (router :> IDisposable).Dispose()
    }
