FROM microsoft/dotnet:2.0-sdk AS build
SHELL ["cmd", "/S", "/C" ]
WORKDIR /app
COPY . ./
RUN dotnet publish -o ../github-app-check-template/build -c Release

FROM microsoft/dotnet:2.0-runtime
WORKDIR /app
COPY --from=build /app/github-app-check-template/build ./
EXPOSE 8008
ENTRYPOINT ["dotnet", "GitHubApp.Server.dll"]
