module GitHubApp.Server.GitHubCheckStatusActor

open GitHubApp.Core
open GitHubApp.Core.Interfaces
open System
open Octokit

let gitHubCheckStatusActor (getClient:GetGitHubClient) =

    let createCheckRun (requestCheck:RequestCheck) =
        let checkSuite = NewCheckRun(GlobalConfiguration.CheckRunName, requestCheck.HeadSha)
        let id = int64 requestCheck.RepositoryId
        if GlobalConfiguration.IgnoredBranchNames |> Set.contains requestCheck.HeadBranch then
            Logging.logInfo "No need to creat CheckRun for commit %s to ignored branches: %A" requestCheck.HeadSha GlobalConfiguration.IgnoredBranchNames
        else
            let (client, _) = getClient requestCheck.InstallationId
            let checkRun = client.Check.Run.Create(id, checkSuite).Result
            Logging.logInfo "CheckRun %i Created for Sha %s" checkRun.Id checkRun.HeadSha

    let updateCheck parameters (checkRunUpdate:CheckRunUpdate) =
        let { CheckRunId = checkRunId; RepositoryId = repositoryId; InstallationId = installationId } = parameters
        let (client, _) = getClient installationId
        let checkRun = client.Check.Run.Update(repositoryId, checkRunId, checkRunUpdate).Result
        let updated = checkRun.StartedAt.ToString("MM/dd/yyyy HH:mm:ss")
        Logging.logInfo "CheckRun %i Updated at %s for Sha %s" checkRun.Id updated checkRun.HeadSha

    let router = new MailboxProcessor<_>(fun (inbox:MailboxProcessor<CheckMessage>) ->
        let rec loop () = async {
            let! msg = inbox.Receive()
            match msg with
            | QueueCheckRun request ->
                createCheckRun request
            | UpdateCheck (parameters, checkRunUpdate) ->
                updateCheck parameters checkRunUpdate
            return! loop () }

        loop ())
    let subscription = router.Error.Subscribe (fun ex -> Logging.logEx ex "Exception in GitHubCheckStatusActor")
    router.Start()

    {
        new IGitHubCheckStatusActor with
            member __.Post msg = router.Post msg
            member __.CurrentQueueLength = router.CurrentQueueLength
            member __.Dispose () =
                subscription.Dispose()
                (router :> IDisposable).Dispose()
    }
