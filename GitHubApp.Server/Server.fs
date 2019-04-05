module GitHubApp.Server.Server

open Suave
open Suave.Successful
open Suave.Operators
open Suave.Filters
open GitHubApp.Integration
open GitHubApp.Integration.ParseEvents
open Octokit
open GitHubApp.Core
open GitHubApp.Core.Interfaces
open System

let start conf =
    let getClient installationID =
        let pemFile = GlobalConfiguration.PemPath
        Logging.logDebug "Pem file location: %s" pemFile
        let keySource = GitHubJwt.FilePrivateKeySource(pemFile)
        let options = GitHubJwt.GitHubJwtFactoryOptions()
        Logging.logDebug "GitHub App Identifier: %i" GlobalConfiguration.GithubAppIdentifier
        options.AppIntegrationId <- GlobalConfiguration.GithubAppIdentifier
        options.ExpirationSeconds <- 30
        let generator = GitHubJwt.GitHubJwtFactory(keySource, options)
        let token = generator.CreateEncodedJwtToken()
        let url = sprintf "https://%s" GlobalConfiguration.GithubUri
        let client = GitHubClient(ProductHeaderValue("ConsistencyApp"), Uri(url));
        client.Credentials <- Octokit.Credentials(token, AuthenticationType.Bearer)

        Logging.logDebug "Installation ID: %i" installationID
        let token = client.GitHubApps.CreateInstallationToken(installationID).Result

        let installationClient = GitHubClient(ProductHeaderValue("ConsistencyApp"), Uri(url));
        installationClient.Credentials <- Octokit.Credentials(token.Token)
        installationClient, token

    let checkActor = GitHubCheckStatusActor.gitHubCheckStatusActor getClient
    Logging.logInfo "Check Actor started"

    let checkRunActor = GitHubCheckRunActor.gitHubCheckRunActor getClient checkActor
    Logging.logInfo "Check Run Actor started"

    let queueCheckRun branch sha repositoryId installationId =
        let repositoryId = int64 repositoryId
        let installationId = int64 installationId
        {
            HeadBranch = branch
            HeadSha = sha
            RepositoryId = repositoryId
            InstallationId = installationId
        }
        |> QueueCheckRun
        |> checkActor.Post

    let startCheck (checkRunEvent:CheckRunEvent.Reader.Root) =
        let checkRunId = int64 checkRunEvent.CheckRun.Id
        let repositoryId = int64 checkRunEvent.Repository.Id
        let installationId = int64 checkRunEvent.Installation.Id
        {
            CheckRunId = checkRunId
            RepositoryId = repositoryId
            InstallationId = installationId
            RepositoryName = checkRunEvent.Repository.Name
            RepositoryFullName = checkRunEvent.Repository.FullName
            HeadSha = checkRunEvent.CheckRun.HeadSha
            HeadBranch = checkRunEvent.CheckRun.CheckSuite.HeadBranch
        }
        |> RunCheck
        |> checkRunActor.Post

    let processCheckRunEvent (checkRunEvent:CheckRunEvent.Reader.Root) =
        match checkRunEvent.Action with
        | "created" ->
            Logging.logInfo "Received CheckRun: %i with action %s" checkRunEvent.CheckRun.Id checkRunEvent.Action
            startCheck checkRunEvent
        | "rerequested" -> 
            Logging.logInfo "Received CheckRun: %i with action %s" checkRunEvent.CheckRun.Id checkRunEvent.Action
            let branch = checkRunEvent.CheckRun.CheckSuite.HeadBranch
            let sha = checkRunEvent.CheckRun.HeadSha
            let repositoryId = checkRunEvent.Repository.Id
            let installationId = checkRunEvent.Installation.Id
            queueCheckRun branch sha repositoryId installationId |> ignore
        | "completed"
        | "requested_action"
        | _ ->
            ()
    
    let processCheckSuiteEvent (checkSuiteEvent:CheckSuiteEvent.Reader.Root) =
        match checkSuiteEvent.Action with
        | "requested"
        | "rerequested" -> 
            Logging.logInfo "Received CheckSuite: %i with action %s" checkSuiteEvent.CheckSuite.Id checkSuiteEvent.Action
            let branch = checkSuiteEvent.CheckSuite.HeadBranch
            let sha = checkSuiteEvent.CheckSuite.HeadSha
            let repositoryId = checkSuiteEvent.Repository.Id
            let installationId = checkSuiteEvent.Installation.Id
            queueCheckRun branch sha repositoryId installationId |> ignore
        | "completed"
        | _ ->
            ()
    
    let processGitHubEvent event payload =
        let event = { Type = event; Payload = payload }
        match event with
        | CheckRunEvent checkRun -> 
            processCheckRunEvent checkRun
            OK ("checkRun")
        | CheckSuiteEvent checkSuiteEvent -> 
            processCheckSuiteEvent checkSuiteEvent
            OK ("checkSuiteEvent")
        | _ -> RequestErrors.BAD_REQUEST event.Type
    
    let processRequest (req : HttpRequest) =
        let getPayload (req : HttpRequest) =
            let getString (rawForm:byte[]) =
                System.Text.Encoding.UTF8.GetString(rawForm)
            req.rawForm |> getString
    
        match req.header "X-GitHub-Event" with
        | Choice1Of2 event ->
            match event with
            | "ping" -> OK ("ping")
            | _ -> 
                getPayload req
                |> processGitHubEvent event
        | Choice2Of2 error ->
            RequestErrors.BAD_REQUEST error
            
    let webPart = choose [
            path "/ConsistencyApp" >=> OK "Hello from GitHub App Consistency"
            path "/ConsistencyApp/event_handler" >=> choose [
                GET >=> RequestErrors.NOT_FOUND "GitHub App Consistency expects POST messages on this URL"
                POST >=> request processRequest
            ]
        ]
    
    startWebServer conf webPart
