# ============================================================================
# start-mcp-pi.ps1 - launch Pi with TLS trust for the local MCP dev stack
#
#   1. Rebuilds ssl\local.cas.pem (dotnet dev cert + Keycloak cert, PEM)
#      - re-run safe: always reflects the *current* dev cert (rotation-safe)
#      - exactly one newline is joined between the two PEMs (the dotnet
#        export has no trailing newline; a missing separator makes Node
#        silently ignore the whole bundle)
#   2. Sets NODE_EXTRA_CA_CERTS for this process tree only
#      (NOT persisted - no setx; gone when the window closes)
#   3. Runs a node trust check (API + Keycloak), then launches pi
#
#   No local variables. No pre-existing environment settings required -
#   everything Pi needs for this run is prepared here, in this session.
#
# Usage:  powershell -NoProfile -File scripts\start-mcp-pi.ps1 [pi args...]
#         e.g. powershell -NoProfile -File scripts\start-mcp-pi.ps1 mcp list
#         (if execution policy blocks local scripts, add -ExecutionPolicy Bypass)
# ============================================================================

Set-Location (Split-Path -Parent $PSScriptRoot)

# ---------- prerequisites ---------------------------------------------------
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { Write-Host '[error] dotnet not found in PATH' -ForegroundColor Red; exit 1 }
if (-not (Get-Command pi -ErrorAction SilentlyContinue))     { Write-Host '[error] pi not found in PATH' -ForegroundColor Red; exit 1 }
if (-not (Test-Path 'ssl\local.keycloak.crt'))               { Write-Host '[error] missing ssl\local.keycloak.crt (run from repo layout: scripts\..)' -ForegroundColor Red; exit 1 }

# ---------- 1. cert bundle ---------------------------------------------------
Write-Host '[1/3] Bundling dev cert + Keycloak cert into ssl\local.cas.pem'
dotnet dev-certs https --export-path 'ssl\local.api.pem' --format Pem | Out-Null
if ($LASTEXITCODE -ne 0) { Write-Host '[error] dotnet dev-certs export failed' -ForegroundColor Red; exit 1 }

# Get-Content -Raw keeps each file byte-exact; the "`n" between them is the
# load-bearing separator (see header note). Set-Content adds the final
# trailing newline.
(Get-Content 'ssl\local.api.pem' -Raw) + "`n" + (Get-Content 'ssl\local.keycloak.crt' -Raw) | Set-Content 'ssl\local.cas.pem'

if ((Select-String -Path 'ssl\local.cas.pem' -SimpleMatch -Pattern 'BEGIN CERTIFICATE').Count -ne 2) {
    Write-Host '[error] bundle sanity check failed: expected 2 certificates' -ForegroundColor Red
    exit 1
}
Write-Host '      ok - 2 certificates in bundle'

# ---------- 2. session-only trust --------------------------------------------
if (-not $env:NODE_EXTRA_CA_CERTS) {
    $env:NODE_EXTRA_CA_CERTS = (Resolve-Path (Join-Path $PSScriptRoot '..\ssl\local.cas.pem')).Path
    Write-Host "[2/3] NODE_EXTRA_CA_CERTS=$($env:NODE_EXTRA_CA_CERTS)"
    Write-Host '      session only - nothing persisted'
} else {
    Write-Host "[2/3] NODE_EXTRA_CA_CERTS already set - keeping: $($env:NODE_EXTRA_CA_CERTS)"
}

# Trust self-check (non-fatal: pi can start before the API does).
# The JS deliberately uses single quotes - PS 5.1 strips double quotes from
# spaced arguments when passing them to native exes.
if (Get-Command node -ErrorAction SilentlyContinue) {
    node -e "Promise.all([fetch('https://localhost:7160/_system/Ping'),fetch('https://localhost:8443/realms/todolist/.well-known/openid-configuration')]).then(()=>{console.log('      trust check ok - API + Keycloak reachable from node');process.exit(0)}).catch(e=>{console.log('      [warn] trust check failed: '+((e.cause&&e.cause.code)||e.message));console.log('      non-fatal - is the API running?');process.exit(0)})"
}

# ---------- 3. launch pi -------------------------------------------------------
Write-Host '[3/3] Launching pi ...'
pi @args
exit $LASTEXITCODE
