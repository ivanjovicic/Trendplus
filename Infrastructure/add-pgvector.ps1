# Add pgvector support to Infrastructure project
# Run this from PowerShell in the Infrastructure directory

cd Infrastructure
dotnet add package Pgvector.EntityFrameworkCore --version 0.2.0
dotnet restore
