# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy only the API project file
COPY Trendplus2/Api.csproj Trendplus2/

# Restore dependencies
RUN dotnet restore Trendplus2/Api.csproj

# Copy the rest of the repo
COPY . .

# Publish only the API project
RUN dotnet publish Trendplus2/Api.csproj -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "Api.dll"]