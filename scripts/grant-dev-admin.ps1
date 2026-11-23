param([Parameter(Mandatory=$true)][Guid]$UserId, [switch]$Revoke)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$configPath = Join-Path $root 'backend/src/Api/appsettings.Development.json'
$settings = if (Test-Path -LiteralPath $configPath) { Get-Content -Raw -LiteralPath $configPath | ConvertFrom-Json } else { [PSCustomObject]@{} }
if (!$settings.PSObject.Properties['Admin']) { $settings | Add-Member -NotePropertyName Admin -NotePropertyValue ([PSCustomObject]@{}) }
$current = if ($settings.Admin.PSObject.Properties['UserIds']) { $settings.Admin.UserIds } else { '' }
$ids = @($current -split '[,;]' | ForEach-Object { $_.Trim() } | Where-Object { $_ -and $_ -ne $UserId.ToString() })
if (!$Revoke) { $ids += $UserId.ToString() }
$settings.Admin | Add-Member -NotePropertyName UserIds -NotePropertyValue (($ids | Select-Object -Unique) -join ';') -Force
$settings | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $configPath -Encoding utf8
Write-Host $(if ($Revoke) { 'Autorização removida no ambiente Development.' } else { 'Conta autorizada no ambiente Development. Atualize a página para ver o editor.' })
Write-Host 'Em produção, configure Admin__UserIds no servidor. Este arquivo não concede acesso em Production.'
