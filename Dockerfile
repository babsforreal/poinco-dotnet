# Étape 1 — construction (le SDK complet, plus lourd)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Api/Api.csproj Api/
RUN dotnet restore Api/Api.csproj

COPY Api/ Api/
RUN dotnet publish Api/Api.csproj -c Release -o /app/publish

# Étape 2 — exécution (juste le runtime, image finale légère)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

# Les images aspnet fournissent un utilisateur non-root prêt à l'emploi via $APP_UID : sans ça
# le conteneur tourne en root, et une RCE dans l'app devient directement root dans le conteneur.
# Le port 8080 n'est pas privilégié, donc rien d'autre à changer.
USER $APP_UID

ENTRYPOINT ["dotnet", "Api.dll"]