# Build from the SOLUTION ROOT:
#   docker build -t finnova-accounts-service:local -f Finnova.AccountsService/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files first for restore-layer caching
COPY Finnova.Models/Finnova.Models.csproj Finnova.Models/
COPY Finnova.Repository/Finnova.Repository.csproj Finnova.Repository/
COPY Finnova.Service/Finnova.Service.csproj Finnova.Service/
COPY Finnova.AccountsService/Finnova.AccountsService.csproj Finnova.AccountsService/
RUN dotnet restore Finnova.AccountsService/Finnova.AccountsService.csproj

# Copy the rest and publish
COPY . .
RUN dotnet publish Finnova.AccountsService/Finnova.AccountsService.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:5020
EXPOSE 5020
ENTRYPOINT ["dotnet", "Finnova.AccountsService.dll"]
