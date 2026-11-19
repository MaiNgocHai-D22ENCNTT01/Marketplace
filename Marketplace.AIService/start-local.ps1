# Start the local AI service with the same PostgreSQL login as the development WebAPI.
$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot
if (-not $env:MARKETPLACE_AI_DSN) {
    $settings = Join-Path $env:APPDATA 'Microsoft/UserSecrets/marketplace-api-local-development/secrets.json'
    $connection = $env:ConnectionStrings__Marketplace
    if (-not $connection -and (Test-Path $settings)) {
        $config = Get-Content -Raw -LiteralPath $settings | ConvertFrom-Json
        $connection = $config.'ConnectionStrings:Marketplace'
        if (-not $connection) { $connection = $config.ConnectionStrings.Marketplace }
    }
    if ($connection) {
        $db = New-Object System.Data.Common.DbConnectionStringBuilder
        $db.set_ConnectionString($connection)
        foreach ($pair in @(@('Host','PGHOST'), @('Port','PGPORT'), @('Database','PGDATABASE'),
                            @('Username','PGUSER'), @('Password','PGPASSWORD'))) {
            if ($db.ContainsKey($pair[0])) {
                [Environment]::SetEnvironmentVariable($pair[1], [string]$db[$pair[0]], 'Process')
            }
        }
        $env:MARKETPLACE_AI_DSN = 'connect_timeout=3'
    }
}
$python = Join-Path $PSScriptRoot '.venv/Scripts/python.exe'
if (-not (Test-Path $python)) { $python = 'python' }
& $python -m uvicorn main:app --host 127.0.0.1 --port 8000
exit $LASTEXITCODE
