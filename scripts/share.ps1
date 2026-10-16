<#
.SYNOPSIS
  Shares the local backend on the internet through an ngrok tunnel, for the web front end on Vercel (ISKELET W-10).

.DESCRIPTION
  Starts the compose services, the Notification service and the API (each API/worker in its own window), waits until
  the API is ready and then opens the tunnel https://<Domain> -> http://localhost:8080. Closing the ngrok window (or
  Ctrl+C) stops sharing; the site on Vercel then shows its "cannot reach the server" messages until the next run.

.PARAMETER Domain
  The free static domain of your ngrok account, e.g. "example-name.ngrok-free.app". Defaults to $env:NGROK_DOMAIN.

.EXAMPLE
  ./scripts/share.ps1 -Domain example-name.ngrok-free.app
#>
param([string]$Domain = $env:NGROK_DOMAIN)

$ErrorActionPreference = 'Stop'
if (-not $Domain) { throw 'Give your ngrok static domain: ./scripts/share.ps1 -Domain <name>.ngrok-free.app (or set NGROK_DOMAIN).' }
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root

Write-Host '1/4 Docker services (PostgreSQL, Redis, RabbitMQ, Mailpit, Aspire Dashboard)...'
docker compose up -d --wait
if ($LASTEXITCODE -ne 0) { throw 'docker compose failed: is Docker Desktop running?' }

Write-Host '2/4 Building the API and the Notification service...'
# One at a time: both use the Contracts project, and parallel builds lock each other's files.
dotnet build src/EventTicketing.Notifications --nologo -v quiet
dotnet build src/EventTicketing.Api --nologo -v quiet
if ($LASTEXITCODE -ne 0) { throw 'build failed' }

Write-Host '3/4 Starting the Notification service and the API in their own windows...'
Start-Process powershell -WorkingDirectory $root -ArgumentList '-NoExit', '-Command', 'dotnet run --no-build --project src/EventTicketing.Notifications'
Start-Process powershell -WorkingDirectory $root -ArgumentList '-NoExit', '-Command', 'dotnet run --no-build --project src/EventTicketing.Api'

$deadline = (Get-Date).AddMinutes(2)
do {
    Start-Sleep -Seconds 2
    try { $ready = (Invoke-WebRequest -UseBasicParsing -TimeoutSec 5 http://localhost:8080/health/ready).StatusCode -eq 200 } catch { $ready = $false }
} until ($ready -or (Get-Date) -gt $deadline)
if (-not $ready) { throw 'The API did not become ready within 2 minutes; check its window for errors.' }

Write-Host "4/4 Opening the tunnel https://$Domain -> http://localhost:8080 (close this window or press Ctrl+C to stop sharing)"
Write-Host '    Requests can be watched at http://127.0.0.1:4040'
ngrok http 8080 --url "https://$Domain"
