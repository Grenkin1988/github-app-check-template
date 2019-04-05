namespace GitHubApp.Core

open System.IO
open System.Reflection

module GlobalConfiguration = 
    let private defaultSettingsLocation = "C:\\github-app-check-template\\Settings\\settings.json"

    let private Config = 
        
        let configFile = 
            if File.Exists(defaultSettingsLocation) then
                defaultSettingsLocation
            else
                let workingDirectory = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location)
                Path.Combine(workingDirectory, "Settings//settings.json")
        let configTxt = File.ReadAllText configFile
        Settings.parse configTxt

    let RepoPath =
        Config.RepoPath

    let PemPath =
        Config.PemPath

    let WebserverPort =
        Config.WebserverPort

    let GithubUri =
        Config.GithubUri

    let GithubAppIdentifier =
        Config.GithubAppIdentifier

    let IgnoredBranchNames =
        let getBranchName (branchName:Settings.Reader.IgnoredBranchName) =
            match branchName.Name.String, branchName.Name.Number with
            | Some name, _ -> name
            | _, Some name -> string name
            | _ -> ""
        Config.IgnoredBranchNames
        |> Array.map getBranchName
        |> Set.ofArray

    let CheckRunName = "My App Check"
