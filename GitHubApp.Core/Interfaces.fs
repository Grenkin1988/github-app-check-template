module GitHubApp.Core.Interfaces

open System
open Octokit

type BranchName = string
type Sha = string
type CheckRunId = int64
type RepositoryId = int64
type InstallationId = int64

type GetGitHubClient = InstallationId -> GitHubClient * AccessToken

type RequestCheck =
    {
        HeadBranch: BranchName
        HeadSha: Sha
        RepositoryId: RepositoryId
        InstallationId: InstallationId
    }

type CheckRunParameters =
    { 
        CheckRunId: CheckRunId
        RepositoryId: RepositoryId
        InstallationId: InstallationId
        RepositoryName: string
        RepositoryFullName: string
        HeadSha: Sha
        HeadBranch: BranchName
    }

type CheckMessage =
    | QueueCheckRun of RequestCheck
    | UpdateCheck of CheckRunParameters * CheckRunUpdate

type CheckRunMessage =
    | RunCheck of CheckRunParameters

type IGitHubCheckStatusActor =
    inherit IDisposable
    abstract CurrentQueueLength : int
    abstract Post : CheckMessage -> unit

type IGitHubCheckRunActor =
    inherit IDisposable
    abstract CurrentQueueLength : int
    abstract Post : CheckRunMessage -> unit

type Problem =
    {
        FileName: string
        Message: string
    }

type ConsistencyCheckProblem =
    {
        Name: string
        Header: string
        ProblemLines: Problem[]
    }

type ConsistencyCheckException=
    {
        Name: string
        Message: string
        Exception: exn
    }

type ConsistencyCheckResult =
    | Passed
    | ProblemsFound of ConsistencyCheckProblem
    | ExceptionCaught of ConsistencyCheckException
