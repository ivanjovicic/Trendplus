# ---------- BUILD ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY . ./

RUN dotnet restore Api/Api.csproj
RUN dotnet publish Api/Api.csproj -c Release -o /app/publish

# ---------- RUNTIME ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Cross-platform Access (.mdb/.accdb) import via ODBC + mdbtools
# Required by AccessImportService — no ACE/OLEDB/Office dependency needed
RUN apt-get update && apt-get install -y --no-install-recommends \
        unixodbc \
        libodbc2 \
        mdbtools \
        odbc-mdbtools \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Api.dll"]
