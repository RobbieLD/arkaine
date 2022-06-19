# Build Server
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build-env
WORKDIR /app

COPY Server.Arkaine/Server/*.csproj ./
RUN dotnet restore

COPY Server.Arkaine/Server/ .
RUN dotnet publish -c Release -o out

# Build Client
FROM node:16 AS web
RUN apt-get update
WORKDIR /app
COPY Client.Arkaine/package*.json ./
RUN npm install
COPY . .
RUN npm run build

# Build Release
FROM mcr.microsoft.com/dotnet/aspnet:6.0
ENV DB_CONNECTION_STRING=$DB_CONNECTION_STIRNG
ENV ACCEPT_IP_RANGE=$ACCEPT_IP_RANGE
ENV B2_KEY=$B2_KEY
ENV B2_KEY_ID=$B2_KEY_ID
ENV ASPNETCORE_ENVIRONMENT=$ASPNETCORE_ENVIRONMENT
WORKDIR /app
COPY --from=build-env /app/out .
COPY --from=web /app/dist ./wwwroot

CMD ASPNETCORE_URLS=http://*:$PORT dotnet Server.Arkaine.dll