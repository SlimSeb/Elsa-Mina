FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /app

COPY .git .
COPY ElsaMina.slnx .
COPY GitVersion.yml .
COPY scripts/**/*.sh /app/scripts/
COPY src/ElsaMina.Console/*.csproj src/ElsaMina.Console/
COPY src/ElsaMina.Core/*.csproj src/ElsaMina.Core/
COPY src/ElsaMina.Commands/*.csproj src/ElsaMina.Commands/
COPY src/ElsaMina.DataAccess/*.csproj src/ElsaMina.DataAccess/
COPY src/ElsaMina.FileSharing/*.csproj src/ElsaMina.FileSharing/
COPY src/ElsaMina.Logging/*.csproj src/ElsaMina.Logging/
COPY src/ElsaMina.Sheets/*.csproj src/ElsaMina.Sheets/
COPY test/ElsaMina.UnitTests/*.csproj test/ElsaMina.UnitTests/
COPY test/ElsaMina.IntegrationTests/*.csproj test/ElsaMina.IntegrationTests/

COPY src/ElsaMina.Console/ src/ElsaMina.Console/
COPY src/ElsaMina.Core/ src/ElsaMina.Core/
COPY src/ElsaMina.Commands/ src/ElsaMina.Commands/
COPY src/ElsaMina.DataAccess/ src/ElsaMina.DataAccess/
COPY src/ElsaMina.FileSharing/ src/ElsaMina.FileSharing/
COPY src/ElsaMina.Logging/ src/ElsaMina.Logging/
COPY src/ElsaMina.Sheets/ src/ElsaMina.Sheets/
COPY test/ElsaMina.UnitTests/ test/ElsaMina.UnitTests/
COPY test/ElsaMina.IntegrationTests/ test/ElsaMina.IntegrationTests/

RUN chmod +x /app/scripts/**/*.sh
RUN /app/scripts/Build/restore.sh
RUN /app/scripts/Build/build.sh
RUN /app/scripts/Build/publish.sh

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final

RUN apt-get update \
    && apt-get install -y --no-install-recommends \
       libkrb5-3 \
       libgssapi-krb5-2 \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/output .
ENTRYPOINT ["dotnet", "ElsaMina.Console.dll"]
