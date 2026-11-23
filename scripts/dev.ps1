param([switch]$InMemory, [string]$ConnectionString)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Set-Location -LiteralPath $projectRoot
New-Item -ItemType Directory -Force .runtime | Out-Null
if (!(Test-Path frontend/node_modules)) {
 Push-Location frontend
 try { npm ci; if ($LASTEXITCODE -ne 0) { throw 'Falha ao instalar frontend.' } } finally { Pop-Location }
}
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://127.0.0.1:5080'
$env:Database__InMemory = if ($InMemory) { 'true' } else { 'false' }
if (!$InMemory -and !$ConnectionString) { throw 'Informe -ConnectionString para PostgreSQL ou -InMemory para demonstração sem persistência.' }
if ($ConnectionString) { $env:ConnectionStrings__Game = $ConnectionString; $env:Database__Migrate = 'true' }
$keyFile = Join-Path $projectRoot '.runtime/dev-key'
if (!(Test-Path -LiteralPath $keyFile)) {
 $bytes = New-Object byte[] 48
 $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
 $rng.GetBytes($bytes); $rng.Dispose()
 [Convert]::ToBase64String($bytes) | Set-Content -LiteralPath $keyFile
}
$env:Jwt__Key = (Get-Content -Raw -LiteralPath $keyFile).Trim()
dotnet restore backend/src/Api --configfile NuGet.Config
if ($LASTEXITCODE -ne 0) { throw 'Falha ao restaurar a API.' }
dotnet build backend/src/Api --no-restore
if ($LASTEXITCODE -ne 0) { throw 'A API não compilou.' }
$apiProcess = Start-Process dotnet -ArgumentList 'run --no-build --no-launch-profile --project backend/src/Api' -WorkingDirectory $projectRoot -WindowStyle Hidden -PassThru -RedirectStandardOutput "$projectRoot/.runtime/api.log" -RedirectStandardError "$projectRoot/.runtime/api-error.log"
$webProcess = Start-Process node -ArgumentList 'node_modules/vite/bin/vite.js --host 127.0.0.1 --port 5173 --strictPort' -WorkingDirectory "$projectRoot/frontend" -WindowStyle Hidden -PassThru -RedirectStandardOutput "$projectRoot/.runtime/web.log" -RedirectStandardError "$projectRoot/.runtime/web-error.log"
Write-Host 'Jogo: http://127.0.0.1:5173'
Write-Host "Processos iniciados: API $($apiProcess.Id), frontend $($webProcess.Id). Logs em .runtime/."
if ($InMemory) { Write-Warning 'Demonstração em memória: contas e carreiras desaparecem quando a API é encerrada.' }
Write-Host 'Pressione Enter para encerrar os dois processos.'
try { Read-Host | Out-Null } finally {
 foreach ($process in @($webProcess,$apiProcess)) { if (!$process.HasExited) { Stop-Process -InputObject $process } }
}
