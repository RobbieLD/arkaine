# Build Server
FROM mcr.microsoft.com/dotnet/sdk:7.0 AS server-build
WORKDIR /app

COPY Server.Arkaine/Server/*.csproj ./
RUN dotnet restore

COPY Server.Arkaine/Server/ .
RUN dotnet publish -c Release -o out

# Build Client
FROM node:16 AS client-build
ARG VERSION=DEV
WORKDIR /app
COPY Client.Arkaine/package*.json ./
RUN yarn install
COPY Client.Arkaine/. ./
ENV VUE_APP_ARKAINE_VERSION=$VERSION
RUN yarn run build

# Build Release
FROM mcr.microsoft.com/dotnet/aspnet:7.0
ENV DB_CONNECTION_STRING=$DB_CONNECTION_STIRNG
ENV ACCEPT_IP_RANGE=$ACCEPT_IP_RANGE
ENV B2_KEY=$B2_KEY
ENV B2_KEY_ID=$B2_KEY_ID
ENV ASPNETCORE_ENVIRONMENT=$ASPNETCORE_ENVIRONMENT
ENV MAX_COOKIE_LIFETIME=$MAX_COOKIE_LIFETIME
WORKDIR /app
COPY --from=server-build /app/out .
RUN rm -rf ./wwwroot/Identity
COPY --from=client-build /app/dist ./wwwroot

RUN apt-get -y update && apt-get -y upgrade && apt-get install -y --no-install-recommends ffmpeg

CMD ASPNETCORE_URLS=http://*:$PORT dotnet Server.Arkaine.dll
