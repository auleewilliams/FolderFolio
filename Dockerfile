FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json Directory.Packages.props ./
COPY src/FolderFolio/FolderFolio.csproj src/FolderFolio/packages.lock.json src/FolderFolio/
RUN dotnet restore src/FolderFolio/FolderFolio.csproj --locked-mode
COPY src/FolderFolio/ src/FolderFolio/
RUN dotnet publish src/FolderFolio/FolderFolio.csproj -c Release --no-restore -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
ENV ASPNETCORE_HTTP_PORTS=8080
RUN mkdir -p /cache && chown -R app:app /cache
COPY --from=build /app/publish .
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "FolderFolio.dll"]
