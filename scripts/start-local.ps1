$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$runtimeDirectory = Join-Path $projectRoot '.runtime'
New-Item -ItemType Directory -Path $runtimeDirectory -Force | Out-Null
$env:APPDATA = $runtimeDirectory
$env:NUGET_PACKAGES = Join-Path $projectRoot '.nuget'
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ASPNETCORE_URLS = 'http://127.0.0.1:5080'
$env:Database__InMemory = 'false'
$env:Logging__LogLevel__Default = 'Warning'
Push-Location $projectRoot
try {
    if (-not (Get-NetTCPConnection -LocalPort 5080 -State Listen -ErrorAction SilentlyContinue)) {
        dotnet build backend/src/Api/Api.csproj --nologo
        if ($LASTEXITCODE -ne 0) { throw 'Não foi possível compilar a API.' }
        Start-Process -FilePath 'dotnet' -ArgumentList @('run', '--no-build', '--no-launch-profile', '--project', 'backend/src/Api') -WorkingDirectory $projectRoot -WindowStyle Hidden -RedirectStandardOutput (Join-Path $runtimeDirectory 'local-api.log') -RedirectStandardError (Join-Path $runtimeDirectory 'local-api-error.log') | Out-Null
    }
    if (-not (Get-NetTCPConnection -LocalPort 5173 -State Listen -ErrorAction SilentlyContinue)) {
        $viteEntry = Join-Path $projectRoot 'frontend/node_modules/vite/bin/vite.js'
        $nodeExecutable = (Get-Command node.exe -ErrorAction Stop).Source
        Start-Process -FilePath $nodeExecutable -ArgumentList @(('"' + $viteEntry + '"'), '--host', '127.0.0.1', '--port', '5173', '--strictPort') -WorkingDirectory (Join-Path $projectRoot 'frontend') -WindowStyle Hidden -RedirectStandardOutput (Join-Path $runtimeDirectory 'local-web.log') -RedirectStandardError (Join-Path $runtimeDirectory 'local-web-error.log') | Out-Null
    }
    foreach ($endpoint in @('http://127.0.0.1:5080/api/health', 'http://127.0.0.1:5173/')) {
        $ready = $false
        for ($attempt = 0; $attempt -lt 15; $attempt++) {
            try { if ((Invoke-WebRequest -Uri $endpoint -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200) { $ready = $true; break } } catch { }
            Start-Sleep -Milliseconds 200
        }
        if (-not $ready) { throw "Serviço não ficou disponível: $endpoint. Confira os logs em .runtime." }
    }
    Write-Output 'Jogo: http://127.0.0.1:5173/'
    Write-Output 'Saves locais: .runtime/local-game.db (ou banco configurado em ConnectionStrings__Game).'
    Write-Output 'O arquivo .runtime/local-jwt.key mantém a chave de acesso local quando Jwt__Key não é configurada.'
} finally { Pop-Location }
