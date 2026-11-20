[CmdletBinding()]
param([string]$Image = 'cmcs-demo:ci')
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Net.Http
$name = 'cmcs-production-check-' + [guid]::NewGuid().ToString('N').Substring(0,10)
$handler = [System.Net.Http.HttpClientHandler]::new()
$handler.AllowAutoRedirect = $false
$client = [System.Net.Http.HttpClient]::new($handler)
$client.Timeout = [TimeSpan]::FromSeconds(5)
$started = $false
try {
    # Bind only to loopback. Forwarded-header trust simulates the managed Render ingress.
    & docker run --detach --rm --name $name -p '127.0.0.1::8080' `
        --env 'AllowedHosts=127.0.0.1;localhost' `
        --env 'ASPNETCORE_FORWARDEDHEADERS_ENABLED=true' $Image
    if ($LASTEXITCODE -ne 0) { throw 'Could not start production test container.' }
    $started = $true
    $binding = & docker port $name '8080/tcp'
    if ($LASTEXITCODE -ne 0 -or $binding -notmatch '^127\.0\.0\.1:(\d+)$') { throw 'Expected a loopback-only test port.' }
    $client.BaseAddress = [uri]('http://127.0.0.1:' + $Matches[1])
    $ready = $false
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        try {
            $probe = $client.GetAsync('/health').GetAwaiter().GetResult()
            $ready = [int]$probe.StatusCode -eq 307
            $probe.Dispose()
        }
        catch { $ready = $false }
        if ($ready) { break }
        Start-Sleep -Milliseconds 250
    }
    if (-not $ready) { throw 'The production HTTP probe must redirect safely instead of rendering a secure form.' }
    foreach ($path in @('/', '/Demo', '/health')) {
        $response = $client.GetAsync($path).GetAwaiter().GetResult()
        try {
            if ([int]$response.StatusCode -ne 307 -or
                $response.Headers.Location.Scheme -ne 'https' -or
                $response.Headers.Location.Port -ne 443 -or
                $response.Headers.Location.AbsolutePath -ne $path) {
                throw "Plain HTTP $path must redirect to the same path over standard HTTPS."
            }
            if ($response.Headers.Contains('Set-Cookie')) { throw 'An insecure probe must not receive application cookies.' }
        }
        finally { $response.Dispose() }
    }
    Write-Host 'PASS Production HTTP probes redirect to HTTPS without rendering forms'
    foreach ($path in @('/', '/Demo', '/health')) {
        $request = [System.Net.Http.HttpRequestMessage]::new([System.Net.Http.HttpMethod]::Get, $path)
        $request.Headers.Add('X-Forwarded-Proto', 'https')
        $response = $client.SendAsync($request).GetAwaiter().GetResult()
        try {
            if ([int]$response.StatusCode -ne 200) { throw "Forwarded HTTPS $path did not return 200." }
            $body = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            if ($path -eq '/health') {
                $health = $body | ConvertFrom-Json
                if ($health.status -ne 'ok' -or $health.workspace -ne 'cmcs-demo') { throw 'Unexpected demo health response.' }
            }
            else {
                if ($body -notmatch 'Try as Lecturer' -or $body -notmatch '__RequestVerificationToken') {
                    throw 'The production role form or its antiforgery token is missing.'
                }
                $cookies = @($response.Headers.GetValues('Set-Cookie'))
                $antiforgery = @($cookies | Where-Object { $_ -match '^cmcs-demo\.antiforgery=' })
                if ($antiforgery.Count -ne 1 -or $antiforgery[0] -notmatch '(?i);\s*secure(?:;|$)') {
                    throw 'Production antiforgery cookies must retain the Secure attribute.'
                }
            }
        }
        finally { $response.Dispose(); $request.Dispose() }
    }
    Write-Host 'PASS Forwarded HTTPS serves role forms, secure antiforgery cookies and demo health'
    $savedPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = 'Continue'
        $logs = (& docker logs $name 2>&1 | Out-String)
        $logsCode = $LASTEXITCODE
    }
    finally { $ErrorActionPreference = $savedPreference }
    if ($logsCode -ne 0) { throw 'Could not inspect production container logs.' }
    if ($logs -match 'The antiforgery system has the configuration value|Failed to determine the https port') {
        throw 'Production requests reproduced the HTTPS configuration error.'
    }
    Write-Host 'Production container checks passed.' -ForegroundColor Green
}
finally {
    $client.Dispose(); $handler.Dispose()
    if ($started) {
        & docker stop $name | Out-Null
        if ($LASTEXITCODE -ne 0) { Write-Warning "Could not stop test container $name." }
    }
}
