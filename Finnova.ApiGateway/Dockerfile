# Build from the SOLUTION ROOT:
#   docker build -t finnova-api-gateway:local -f Finnova.ApiGateway/Dockerfile .
# The gateway has no project references, but building from root keeps the command
# shape identical to the other services.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Finnova.ApiGateway/Finnova.ApiGateway.csproj Finnova.ApiGateway/
RUN dotnet restore Finnova.ApiGateway/Finnova.ApiGateway.csproj

COPY Finnova.ApiGateway/ Finnova.ApiGateway/
RUN dotnet publish Finnova.ApiGateway/Finnova.ApiGateway.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:5000
EXPOSE 5000
ENTRYPOINT ["dotnet", "Finnova.ApiGateway.dll"]
