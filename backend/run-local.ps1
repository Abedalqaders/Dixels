# Runs the backend with dotnet on Windows (no Docker image to build), against the Postgres
# already running on Windows. Only smtp4dev stays in Docker. Reads the same backend/.env as
# docker-compose.yml, so each worktree keeps its own ports and database.
#
#   .\run-local.ps1            # smtp4dev in Docker + the API with `dotnet watch` (rebuilds on save)
#   .\run-local.ps1 -NoWatch   # the API with a plain `dotnet run`
#   .\run-local.ps1 -Migrate   # run the DbMigrator against this worktree's database, then stop
#
# Ports come from .env: DIXELS_API_PORT (default 44334), DIXELS_MAIL_PORT (smtp4dev inbox,
# default 5000). smtp4dev's SMTP port is the inbox port + 20000 (5000 -> 25000), its IMAP port
# the inbox port + 21000 (5000 -> 26000), so worktrees never clash. Needs the HTTPS dev cert from the
# docker-compose.yml setup notes.
param([switch]$NoWatch, [switch]$Migrate)
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot

# .env into this process only. Keys may contain dots (Settings__Abp.Mailing...), so no shell syntax.
foreach ($line in Get-Content (Join-Path $root '.env')) {
    if ($line -match '^\s*([^#=\s][^=]*?)\s*=(.*)$') {
        [Environment]::SetEnvironmentVariable($Matches[1], $Matches[2], 'Process')
    }
}

$apiPort = if ($env:DIXELS_API_PORT) { $env:DIXELS_API_PORT } else { '44334' }
$mailPort = if ($env:DIXELS_MAIL_PORT) { $env:DIXELS_MAIL_PORT } else { '5000' }
$smtpPort = [int]$mailPort + 20000
$imapPort = [int]$mailPort + 21000

# What .env says for a container, said for Windows instead.
$env:ConnectionStrings__Default = $env:ConnectionStrings__Default -replace 'Host=host\.docker\.internal', 'Host=localhost'
[Environment]::SetEnvironmentVariable('Settings__Abp.Mailing.Smtp.Host', 'localhost', 'Process')
[Environment]::SetEnvironmentVariable('Settings__Abp.Mailing.Smtp.Port', "$smtpPort", 'Process')
[Environment]::SetEnvironmentVariable('Settings__Dixels.Emails.RsvpMailbox.Imap.Host', 'localhost', 'Process')
[Environment]::SetEnvironmentVariable('Settings__Dixels.Emails.RsvpMailbox.Imap.Port', "$imapPort", 'Process')
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = "https://localhost:$apiPort"
$env:ASPNETCORE_Kestrel__Certificates__Default__Path = Join-Path $env:USERPROFILE '.aspnet\https\dixels.pfx'
$env:BlobStoring__FileSystem__BasePath = Join-Path $root 'App_Data\blobs'

# From here on only external programs run. They write progress (docker) and banners
# (dotnet watch) to stderr, which Windows PowerShell turns into a terminating error under
# 'Stop' whenever the output is redirected; their exit codes say whether they worked.
$ErrorActionPreference = 'Continue'

if ($Migrate) {
    # The sign-in clients' URLs are rewritten on every run: this worktree's API, and its web
    # ports (App__CorsOrigins), instead of the defaults in the migrator's appsettings.json.
    $env:OpenIddict__Applications__Dixels_Swagger__RootUrl = "https://localhost:$apiPort"
    if ($env:App__CorsOrigins) { $env:OpenIddict__Applications__Dixels_App__RootUrl = $env:App__CorsOrigins }
    # From its own folder, or its appsettings.json (and the OIDC clients in it) isn't loaded.
    Push-Location (Join-Path $root 'src\Dixels.DbMigrator')
    try { dotnet run } finally { Pop-Location }
    exit $LASTEXITCODE
}

# Only the mail catcher in Docker, with its SMTP and IMAP ports open to Windows.
$env:DIXELS_SMTP_PORT = "$smtpPort"
$env:DIXELS_IMAP_PORT = "$imapPort"
Push-Location $root
try { docker compose up -d smtp4dev } finally { Pop-Location }
if ($LASTEXITCODE -ne 0) { throw "Couldn't start smtp4dev (is Docker Desktop running?)" }
Write-Host "API https://localhost:$apiPort  |  inbox http://localhost:$mailPort  |  SMTP localhost:$smtpPort  |  IMAP localhost:$imapPort"

$project = Join-Path $root 'src\Dixels.Web'
if ($NoWatch) {
    dotnet run --project $project --no-launch-profile
} else {
    dotnet watch --project $project run --no-launch-profile
}
