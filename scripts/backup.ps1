param([string]$OutputDirectory = './backups')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $projectRoot
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
# O arquivo binário não passa pelo redirecionamento de texto do PowerShell.
docker compose exec -T db pg_dump -U manager -d brasileirao -Fc -f /tmp/brasileirao-backup.dump
if ($LASTEXITCODE -ne 0) { throw 'Falha ao gerar backup.' }
docker compose cp db:/tmp/brasileirao-backup.dump "$OutputDirectory/brasileirao-$stamp.dump"
if ($LASTEXITCODE -ne 0) { throw 'Falha ao copiar backup.' }
Write-Host "Backup criado: $OutputDirectory/brasileirao-$stamp.dump"
