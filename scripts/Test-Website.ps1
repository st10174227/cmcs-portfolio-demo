[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$baseUrl = 'http://127.0.0.1:5092'
$health = Invoke-RestMethod -Uri "$baseUrl/health" -TimeoutSec 10
if ($health.workspace -ne 'cmcs-personal' -or $health.status -ne 'ok') {
    throw 'The expected personal website is not running on port 5092.'
}

foreach ($page in @('/', '/Account/Login')) {
    $response = Invoke-WebRequest -UseBasicParsing -Uri "$baseUrl$page" -TimeoutSec 15
    if ($response.StatusCode -ne 200 -or $response.Content -notmatch '<html') {
        throw "Public page did not load: $page"
    }
    if ($response.Content -match '//OpenAI\.2025\.Chat-GPT') {
        throw "A source attribution comment is being rendered on $page."
    }
    Write-Host "PASS Public page loads: $page" -ForegroundColor Green
}

foreach ($path in @('/Claim', '/Coordinator', '/Manager', '/HR', '/HR/ExportAllClaims', '/HR/ExportLecturers')) {
    $request = [System.Net.HttpWebRequest]::CreateHttp("$baseUrl$path")
    $request.AllowAutoRedirect = $false
    $request.Timeout = 10000
    $response = $null
    try {
        try { $response = $request.GetResponse() }
        catch [System.Net.WebException] {
            if (-not $_.Exception.Response) { throw }
            $response = $_.Exception.Response
        }
        $status = [int]$response.StatusCode
        if ($status -ne 302 -and $status -ne 401) {
            throw "Protected route did not require sign-in: $path (status $status)"
        }
        if ($status -eq 302 -and $response.Headers['Location'] -notlike '*/Account/Login*') {
            throw "Protected route redirected somewhere other than sign-in: $path"
        }
        Write-Host "PASS Sign-in required: $path" -ForegroundColor Green
    }
    finally { if ($response) { $response.Dispose() } }
}
Write-Host 'Anonymous smoke checks passed. Signed-in role/ownership cases run in Check-PersonalWorkspace.ps1.'
