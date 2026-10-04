# Build from the SOLUTION ROOT:
#   docker build -t finnova-ua-service:local -f Finnova.UAService/Dockerfile .
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy csproj files first for restore-layer caching
COPY Finnova.Models/Finnova.Models.csproj Finnova.Models/
COPY Finnova.Repository/Finnova.Repository.csproj Finnova.Repository/
COPY Finnova.Service/Finnova.Service.csproj Finnova.Service/
COPY Finnova.UAService/Finnova.UAService.csproj Finnova.UAService/
RUN dotnet restore Finnova.UAService/Finnova.UAService.csproj

# Copy the rest and publish
COPY . .
RUN dotnet publish Finnova.UAService/Finnova.UAService.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:5010
EXPOSE 5010
ENTRYPOINT ["dotnet", "Finnova.UAService.dll"]
