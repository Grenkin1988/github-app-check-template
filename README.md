# github-app-check-template
Consistency GitHub App

## Consistency Checks

These are the checks that are executed on each commit:

##### Banned Files
Some files are banned from the repository (e.g. NuGet `packages.config`). 

### Setup

For testin can use:
https://smee.io/

Example:
```
smee --url https://smee.io/Ng8TtXxk6dZFJz5u --path /ConsistencyApp/event_handler --port 8008
```

#### Build

```
./build.cmd
```

#### Run

```
./run.cmd
```
