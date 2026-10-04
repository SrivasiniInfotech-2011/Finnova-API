# Build from the SOLUTION ROOT:
#   docker build -t finnova-systemadmin-service:local -f Finnova.SystemAdminService/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files first for restore-layer caching
COPY Finnova.Models/Finnova.Models.csproj Finnova.Models/
COPY Finnova.Repository/Finnova.Repository.csproj Finnova.Repository/
COPY Finnova.Service/Finnova.Service.csproj Finnova.Service/
COPY Finnova.SystemAdminService/Finnova.SystemAdminService.csproj Finnova.SystemAdminService/
RUN dotnet restore Finnova.SystemAdminService/Finnova.SystemAdminService.csproj

# Copy the rest and publish
COPY . .
RUN dotnet publish Finnova.SystemAdminService/Finnova.SystemAdminService.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:5030
EXPOSE 5030
ENTRYPOINT ["dotnet", "Finnova.SystemAdminService.dll"]
