FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy only the API project
COPY Trendplus/*.csproj Trendplus/
RUN dotnet restore Trendplus/Trendplus.csproj

# Copy everything
COPY . .
RUN dotnet publish Trendplus/Trendplus.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "Trendplus.dll"]
