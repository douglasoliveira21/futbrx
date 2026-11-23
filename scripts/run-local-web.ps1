$ErrorActionPreference = 'Stop'
Set-Location (Join-Path $PSScriptRoot '../frontend')
npm run dev -- --port 5173 --strictPort
