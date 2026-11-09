$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$frontendPath = Join-Path $projectRoot 'frontend'
$apiPath = Join-Path $projectRoot 'backend\InvoiceManager.Api'
$dotnetPath = (Get-Command dotnet -ErrorAction SilentlyContinue).Source
if (-not $dotnetPath) { $dotnetPath = Join-Path $env:USERPROFILE '.dotnet\dotnet.exe' }
if (-not (Test-Path -LiteralPath $dotnetPath)) { throw 'Instala el SDK de .NET 8 antes de iniciar.' }
$nodePath = (Get-Command node -ErrorAction Stop).Source
foreach ($port in @(5080, 5173)) {
    if (Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue) {
        throw "El puerto $port ya está ocupado. Detén la ejecución anterior o usa el inicio manual del README."
    }
}
Push-Location $projectRoot
try {
    & $dotnetPath build Eficiencia.sln --nologo
    if ($LASTEXITCODE -ne 0) { throw 'No se pudo compilar la solución.' }
    if (-not (Test-Path -LiteralPath (Join-Path $frontendPath 'node_modules'))) {
        Push-Location $frontendPath
        try {
            & npm.cmd ci
            if ($LASTEXITCODE -ne 0) { throw 'No se pudieron instalar las dependencias del frontend.' }
        } finally { Pop-Location }
    }
    $logDirectory = Join-Path $projectRoot 'artifacts\logs'
    New-Item -ItemType Directory -Force -Path $logDirectory | Out-Null
    $apiDll = Join-Path $apiPath 'bin\Debug\net8.0\InvoiceManager.Api.dll'
    $viteEntry = Join-Path $frontendPath 'node_modules\vite\bin\vite.js'
    $apiProcess = Start-Process -FilePath $dotnetPath -ArgumentList ('"{0}" --urls http://localhost:5080' -f $apiDll) -WorkingDirectory $apiPath -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $logDirectory 'api.log') -RedirectStandardError (Join-Path $logDirectory 'api-error.log')
    $webProcess = Start-Process -FilePath $nodePath -ArgumentList ('"{0}" --host 127.0.0.1' -f $viteEntry) -WorkingDirectory $frontendPath -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $logDirectory 'web.log') -RedirectStandardError (Join-Path $logDirectory 'web-error.log')
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        $apiProcess.Refresh(); $webProcess.Refresh()
        if ($apiProcess.HasExited -or $webProcess.HasExited) { throw "Un servidor se detuvo. Revisa $logDirectory." }
        try {
            $null = Invoke-WebRequest -Uri 'http://localhost:5080/api/health' -UseBasicParsing
            $null = Invoke-WebRequest -Uri 'http://127.0.0.1:5173' -UseBasicParsing
            break
        } catch {
            if ($attempt -eq 39) { throw 'Los servidores no respondieron dentro del plazo.' }
            Start-Sleep -Milliseconds 500
        }
    }
    Write-Host "`nAplicación: http://127.0.0.1:5173" -ForegroundColor Green
    Write-Host 'Swagger:    http://localhost:5080/swagger'
    Write-Host "Importa el archivo data\bd_exam_invoices.json desde el botón Importar JSON.`n"
    $null = Read-Host 'Presiona Enter para detener ambos servidores'
} finally {
    if ($webProcess -and -not $webProcess.HasExited) { Stop-Process -Id $webProcess.Id -ErrorAction SilentlyContinue }
    if ($apiProcess -and -not $apiProcess.HasExited) { Stop-Process -Id $apiProcess.Id -ErrorAction SilentlyContinue }
    Pop-Location
}
