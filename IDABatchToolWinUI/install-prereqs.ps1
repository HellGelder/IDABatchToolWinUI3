#Requires -Version 5.1
<#
install-prereqs.ps1 — доустановка Python-окружения для IDA Batch Tool.

Скачивает встроенный Python (embeddable, python.org) в app\Tools\Python,
подключает site-packages и устанавливает туда пакеты jinja2 и requests.
Права администратора не требуются, система не изменяется.
Нужен доступ в интернет (python.org, bootstrap.pypa.io, pypi.org).
#>
$ErrorActionPreference = "Stop"
$packageRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$pyDir = Join-Path (Join-Path $packageRoot "app") "Tools\Python"
$spDir = Join-Path $pyDir "site-packages"

$PyVersion = "3.11.9"
$arch = "amd64"
if ($env:PROCESSOR_ARCHITECTURE -eq "ARM64") { $arch = "arm64" }
$zipUrl = "https://www.python.org/ftp/python/$PyVersion/python-$PyVersion-embed-$arch.zip"
$zipPath = Join-Path $env:TEMP ("python-$PyVersion-embed-$arch.zip")

try {
    # 1. Интерпретатор
    if (Test-Path (Join-Path $pyDir "python.exe")) {
        Write-Host "1/4 Python уже установлен: $pyDir"
    }
    else {
        Write-Host "1/4 Скачивание Python $PyVersion ($arch) с python.org..."
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri $zipUrl -OutFile $zipPath -UseBasicParsing
        Write-Host "2/4 Распаковка в $pyDir ..."
        New-Item -ItemType Directory -Force -Path $pyDir | Out-Null
        Expand-Archive -Path $zipPath -DestinationPath $pyDir -Force
    }

    # 2. Подключить site-packages (файл python*._pth)
    $pth = Get-ChildItem $pyDir -Filter "python*._pth" | Select-Object -First 1
    if ($pth) {
        $lines = Get-Content $pth.FullName
        if (-not ($lines | Where-Object { $_ -match '^site-packages' })) {
            $lines = $lines | ForEach-Object { if ($_ -match '^\s*#\s*import\s+site') { 'import site' } else { $_ } }
            $lines += 'site-packages'
            Set-Content -Path $pth.FullName -Value $lines -Encoding ASCII
            Write-Host "site-packages подключён ($($pth.Name))"
        }
    }
    New-Item -ItemType Directory -Force -Path $spDir | Out-Null

    # 3. Пакеты
    Write-Host "3/4 Установка пакетов jinja2 и requests (pypi.org)..."
    $pyExe = Join-Path $pyDir "python.exe"
    $getPip = Join-Path $env:TEMP "get-pip.py"
    if (-not (Test-Path (Join-Path $pyDir "Scripts\pip.exe"))) {
        Invoke-WebRequest -Uri "https://bootstrap.pypa.io/get-pip.py" -OutFile $getPip -UseBasicParsing
        & $pyExe $getPip --no-warn-script-location --quiet
    }
    & $pyExe -m pip install --target $spDir --upgrade --quiet jinja2 requests
    if ($LASTEXITCODE -ne 0) { throw "pip завершился с кодом $LASTEXITCODE" }

    # 4. Проверка
    Write-Host "4/4 Проверка импорта..."
    & $pyExe -c "import jinja2, requests; print('OK: jinja2', jinja2.__version__, '| requests', requests.__version__)"
    if ($LASTEXITCODE -ne 0) { throw "проверка импорта не прошла" }

    Write-Host ""
    Write-Host "ГОТОВО: Python $PyVersion и пакеты установлены в app\Tools\Python." -ForegroundColor Green
    Write-Host "Перезапустите IDA Batch Tool." -ForegroundColor Green
    exit 0
}
catch {
    Write-Host ""
    Write-Host ("ОШИБКА: " + $_.Exception.Message) -ForegroundColor Red
    Write-Host "Проверьте доступ в интернет и запустите скрипт ещё раз," -ForegroundColor Yellow
    Write-Host "либо установите Python 3.10+ с пакетами jinja2 и requests вручную." -ForegroundColor Yellow
    exit 1
}
