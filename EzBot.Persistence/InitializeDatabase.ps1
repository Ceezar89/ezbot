Param(
    [string]$MigrationName = "InitialCreate"
)

Write-Host "Starting EF Core migration script..."

# 1. Check if dotnet-ef is installed
Write-Host "Checking if 'dotnet-ef' tool is installed..."
$toolList = dotnet tool list --global | Out-String
if ($toolList -notmatch "dotnet-ef") {
    Write-Host "'dotnet-ef' is not installed globally. Installing..."
    dotnet tool install --global dotnet-ef
} else {
    Write-Host "'dotnet-ef' is already installed globally."
}

# 2. Navigate to this script's directory (where the .csproj typically lives)
Set-Location $PSScriptRoot

# 3. Create a new migration (if one doesn't already exist) or update an existing one
Write-Host "Creating or updating EF Core migration: $MigrationName"
dotnet ef migrations add $MigrationName

# 4. Apply the migration to update (or create) the SQLite database
Write-Host "Applying migration to the database..."
dotnet ef database update

Write-Host "Done. The database should now be up to date with the latest EF Core models."
