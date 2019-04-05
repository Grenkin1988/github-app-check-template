# github-app-check-template
Consistency GitHub App

## Consistency Checks

These are the checks that are executed on each commit:

##### Banned Files
Some files are banned from the repository (e.g. NuGet `packages.config`). 

### Dependencies

 - Docker for Windows that is configured to run Windows containers currently
 - Docker volume created on target machine
 - `"volume"\\pem\\` on machine should containe correct pem security file

### Setup

For testin can use:
```
smee --url https://smee.io/Ng8TtXxk6dZFJz5u --path /ConsistencyApp/event_handler --port 8008
```

You need to create volume on machine
```
docker volume create --name github-app-check-template
```
This will create folder in `[data-root]/volumes/github-app-check-template`

*[data-root] is not actual folder you need to use root folder of docker*

#### Build

```
docker build . -t github-app-check-template
```

#### Run

```
docker run --name github-app-check-template-service -v [data-root]\volumes\om-consistency-app:C:\github-app-check-template -p=8008:8008 -d github-app-check-template
```

Examples:

```
docker run --name github-app-check-template-service -v C:\ProgramData\Docker\volumes\github-app-check-template:C:\github-app-check-template -p 8008:8008 github-app-check-template
```

After this, we can stop/start the container as usual:

```
docker stop github-app-check-template-service
docker start github-app-check-template-service
```
