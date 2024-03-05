# Build Stage
FROM mcr.microsoft.com/dotnet/sdk:8.0-alpine3.18 AS build
WORKDIR /source
COPY . .
RUN dotnet restore "./src/Banqer.TotalIpSocketListener/Banqer.TotalIpSocketListener.csproj" --disable-parallel
RUN dotnet publish "./src/Banqer.TotalIpSocketListener/Banqer.TotalIpSocketListener.csproj" -c Release \
                    -p:PreBuildEventUseInBuild=false \
                    -p:PostBuildEventUseInBuild=false \
                    -p:RunCodeAnalysis=false \
                    -p:DebugSymbols=false \
                    -p:DebugType=None \
                    -p:AllowedReferenceRelatedFileExtensions=none \
                    -o /app --no-restore

# Serve Stage
FROM mcr.microsoft.com/dotnet/runtime:8.0-alpine3.18
LABEL org.opencontainers.image.source="https://github.com/banqercrm/totalip-socket-listener"
LABEL org.opencontainers.image.title="TotalIp Socket Listener"
LABEL org.opencontainers.image.vendor="EVR Sistemas"
RUN apk add --no-cache tzdata
ENV TZ=America/Sao_Paulo
WORKDIR /app
COPY --from=build /app ./

ENTRYPOINT ["dotnet", "Banqer.TotalIpSocketListener.dll"]