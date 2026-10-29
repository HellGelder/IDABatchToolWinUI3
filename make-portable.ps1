#Requires -Version 5.1
<#
.SYNOPSIS
    Сборка переносимой поставки IDABatchTool (WinUI, исполнение 2).

.DESCRIPTION
    Раскладка результата (dist\portable):

        IDABatchTool.exe    лончер (~10 КБ, .NET Framework из состава Windows):
                            запускает app\IDABatchToolWinUI.exe, иконка приложения
        bindiff.exe         BinDiff CLI в корне (находится ToolLocator'ом по
                            родительскому каталогу app\)
        app\                вся publish-папка: exe приложения, все DLL,
                            _python\, scripts\, config.yaml, Assets\, ...

    Дополнительно создаётся dist\IDABatchToolWinUI-<версия>-<rid>.zip.

    Дистрибутив собирается ВНЕ репозитория (..\dist - папкой выше): папка
    репозитория помечена зоной Internet (побочный эффект скачивания файлов
    внутрь проекта, см. scripts\build-manpages.ps1), и всё создаваемое в ней
    наследует эту зону - запуск неподписанного exe из такой папки показывает
    окно «Не удаётся проверить издателя». В чистой папке поставка остаётся
    без зоны.

.EXAMPLE
    .\make-portable.ps1                  # publish + раскладка + лончер + zip
.EXAMPLE
    .\make-portable.ps1 -NoPublish       # пересобрать раскладку из готового publish
.EXAMPLE
    .\make-portable.ps1 -NoZip           # без архивирования
.EXAMPLE
    .\make-portable.ps1 -Rid win-arm64 -NoZip
.EXAMPLE
    .\make-portable.ps1 -SkipGraph        # без актуализации графа graphify
.EXAMPLE
    .\make-portable.ps1 -SkipManpages     # без сборки/встраивания man-pages
#>
param(
    [string]$Configuration = "Release",
    [string]$Rid = "win-x64",
    [switch]$NoPublish,
    [switch]$NoZip,
    [switch]$SkipGraph,
    [switch]$SkipManpages
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

# ── 0. Версия - единый источник: Directory.Build.props ──────────────────────
# При отсутствии версии раньше молча подставлялась 1.0.0.0 - теперь это ошибка.
$propsPath = Join-Path $root "Directory.Build.props"
if (-not (Test-Path $propsPath)) {
    throw "Не найден Directory.Build.props - единый источник версии сборки."
}
$propsRaw = Get-Content $propsPath -Raw
$AppVersion = if ($propsRaw -match '<FileVersion>([^<]+)</FileVersion>') { $Matches[1] } else { "" }
if (-not $AppVersion) {
    throw "В Directory.Build.props нет <FileVersion> - версия не определена."
}
Write-Host "Версия сборки: $AppVersion (Directory.Build.props)"

# Assert консистентности: манифесты и журнал version.txt обязаны совпадать
# с Directory.Build.props, иначе поставка выйдет с рассинхронизированной версией.
$manifestChecks = @(
    @{ Path = Join-Path $root "Package.appxmanifest"
       Pattern = '(?m)^\s*Version="([^"]+)"\s*/>'
       Name = "Package.appxmanifest (Identity Version)" },
    @{ Path = Join-Path $root "app.manifest"
       Pattern = '<assemblyIdentity version="([^"]+)"'
       Name = "app.manifest (assemblyIdentity)" }
)
foreach ($c in $manifestChecks) {
    if (-not (Test-Path $c.Path)) { throw "Не найден $($c.Path) - расхождение версии не проверить." }
    $raw = Get-Content $c.Path -Raw
    $v = if ($raw -match $c.Pattern) { $Matches[1] } else { "" }
    if ($v -ne $AppVersion) {
        throw ("Рассинхрон версии: {0} = '{1}', Directory.Build.props = '{2}'. " +
               "Подними версию синхронно (см. комментарий в Directory.Build.props).") -f $c.Name, $v, $AppVersion
    }
}
$versionTxtPath = Join-Path $root "version.txt"
if (-not (Test-Path $versionTxtPath)) {
    throw "Не найден version.txt - журнал сборок обязателен в поставке."
}
$vtRaw = Get-Content $versionTxtPath -Raw
$vtVersion = if ($vtRaw -match '(?m)^(\d+\.\d+\.\d+\.\d+)\s*/\s*\d{4}-\d{2}-\d{2}') { $Matches[1] } else { "" }
if ($vtVersion -ne $AppVersion) {
    throw ("Рассинхрон версии: последняя запись version.txt = '{0}', Directory.Build.props = '{1}'. " +
           "Допиши запись в version.txt (формат - в шапке файла).") -f $vtVersion, $AppVersion
}

# ── 1. Publish ──────────────────────────────────────────────────────────────
if (-not $NoPublish) {
    # SelfContained обязателен явно: без него publish выходит framework-dependent
    # (без рантайма .NET) и на чистой машине требует установки .NET.
    dotnet publish "$root\IDABatchToolWinUI.csproj" -c $Configuration -r $Rid -p:SelfContained=true
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish завершился с кодом $LASTEXITCODE" }
}

$publishDir = Get-ChildItem "$root\bin\$Configuration" -Directory -Filter "net9.0-*" |
    ForEach-Object { Join-Path $_.FullName "$Rid\publish" } |
    Where-Object { Test-Path $_ } |
    Select-Object -First 1
if (-not $publishDir) {
    throw "Не найдена папка publish (bin\$Configuration\net9.0-*\$Rid\publish). Сначала выполните dotnet publish."
}
if (-not (Test-Path (Join-Path $publishDir "IDABatchToolWinUI.exe"))) {
    throw "В $publishDir нет IDABatchToolWinUI.exe - публикация неполная."
}

# ── 2. Раскладка: всё содержимое publish -> dist\portable\app\ ─────────────
# dist - папкой ВЫШЕ репозитория (см. комментарий в .DESCRIPTION): внутри
# репозитория новые файлы наследуют зону Internet и дают окно SmartScreen.
$dist    = Join-Path (Split-Path $root -Parent) "dist"
$staging = Join-Path $dist "portable"
$appDir  = Join-Path $staging "app"
Write-Host ("Поставка собирается вне репозитория: " + $dist)

if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
New-Item -ItemType Directory -Path $appDir -Force | Out-Null
Copy-Item (Join-Path $publishDir "*") $appDir -Recurse -Force

# ── 3. bindiff.exe - в корень поставки ──────────────────────────────────────
$bindiff = Join-Path $appDir "bindiff.exe"
if (Test-Path $bindiff) {
    Move-Item $bindiff (Join-Path $staging "bindiff.exe") -Force
    Write-Host "bindiff.exe перенесён в корень поставки"
}
else {
    Write-Warning "bindiff.exe не найден в publish - корень будет без него (поиск BinDiff: конфиг/реестр/PATH)"
}

# ── 3.5. Документация поставки: readme, журнал версий, автоустановка Python ─
foreach ($doc in @("README.txt", "version.txt", "install-prereqs.ps1", "unblock-portable.ps1")) {
    $src = Join-Path $root $doc
    if (Test-Path $src) { Copy-Item $src (Join-Path $staging $doc) -Force }
    else { Write-Warning "$doc не найден в проекте - в поставку не попал" }
}

# ── 3.6. Python-окружение: встроить Tools\Python из проекта, если есть ─────
$toolsSrc = Join-Path $root "Tools\Python"
if (Test-Path (Join-Path $toolsSrc "python.exe")) {
    $toolsDst = Join-Path $appDir "Tools\Python"
    if (Test-Path $toolsDst) { Remove-Item $toolsDst -Recurse -Force }
    New-Item -ItemType Directory -Force -Path (Split-Path $toolsDst) | Out-Null
    Copy-Item $toolsSrc $toolsDst -Recurse -Force
    Write-Host "Python-окружение встроено в поставку (app\Tools\Python)"
}
else {
    Write-Warning "Tools\Python не встроен: на целевой машине нужен Python с jinja2/requests либо автоустановка через install-prereqs.ps1 (приложение само предложит при запуске)"
}

# ── 3.7. Встроенная документация man-pages: app\docs\ ──────────────────────
if (-not $SkipManpages) {
    & (Join-Path $root "scripts\build-manpages.ps1")
}
$docsSrc = Join-Path $root "docs"
if (Test-Path (Join-Path $docsSrc "manpages.db")) {
    $docsDst = Join-Path $appDir "docs"
    if (Test-Path $docsDst) { Remove-Item $docsDst -Recurse -Force }
    Copy-Item $docsSrc $docsDst -Recurse -Force
    Write-Host "Документация man-pages встроена (app\docs)"
}
else {
    Write-Warning "docs\manpages.db не собрана - поставка без встроенной документации (см. scripts\build-manpages.ps1)"
}

# ── 4. Лончер ───────────────────────────────────────────────────────────────
$launcherCs = Join-Path $env:TEMP "idabatchtool-launcher.cs"
$launcherSource = @'
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

[assembly: AssemblyVersion("__APPVERSION__")]
[assembly: AssemblyFileVersion("__APPVERSION__")]
[assembly: AssemblyTitle("IDA Batch Tool")]
[assembly: AssemblyProduct("IDA Batch Tool")]

static class Launcher
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    static extern int MessageBox(IntPtr hWnd, string text, string caption, int type);

    [STAThread]
    static int Main(string[] args)
    {
        string appExe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app", "IDABatchToolWinUI.exe");
        if (!File.Exists(appExe))
        {
            MessageBox(IntPtr.Zero,
                "\u041D\u0435 \u043D\u0430\u0439\u0434\u0435\u043D\u043E \u043F\u0440\u0438\u043B\u043E\u0436\u0435\u043D\u0438\u0435:\n" + appExe +
                "\n\n\u0420\u0430\u0441\u043F\u0430\u043A\u0443\u0439\u0442\u0435 \u0430\u0440\u0445\u0438\u0432 \u0446\u0435\u043B\u0438\u043A\u043E\u043C, \u0441\u043E\u0445\u0440\u0430\u043D\u044F\u044F \u043F\u0430\u043F\u043A\u0443 app.",
                "IDA Batch Tool", 0x10 /* MB_ICONERROR */);
            return 1;
        }

        var psi = new ProcessStartInfo
        {
            FileName = appExe,
            WorkingDirectory = Path.GetDirectoryName(appExe),
            // CreateProcess, а не ShellExecute: ShellExecute запускает проверку
            // зоны Attachment Manager, и для неподписанного exe Windows показывает
            // окно «Не удаётся проверить издателя» - CreateProcess её не вызывает.
            UseShellExecute = false,
        };
        // ProcessStartInfo.ArgumentList недоступен в .NET Framework 4.8 -
        // передаём аргументы строкой с простым кавычиванием.
        if (args != null && args.Length > 0)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var a in args)
            {
                if (sb.Length > 0) sb.Append(' ');
                sb.Append('"').Append(a.Replace("\"", "\\\"")).Append('"');
            }
            psi.Arguments = sb.ToString();
        }
        Process.Start(psi);
        return 0;
    }
}
'@
$launcherSource = $launcherSource.Replace("__APPVERSION__", $AppVersion)
# .NET Framework csc не знает /version - версия задаётся assembly-атрибутами выше
Set-Content -Path $launcherCs -Value $launcherSource -Encoding ASCII

$csc = "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if (-not (Test-Path $csc)) { $csc = "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe" }
if (-not (Test-Path $csc)) { throw "Компилятор csc.exe (.NET Framework) не найден" }

$launcherExe = Join-Path $staging "IDABatchTool.exe"
$icon = Join-Path $appDir "Assets\AppIcon.ico"
$cscArgs = @("/nologo", "/target:winexe", "/platform:anycpu", "/optimize+")
if (Test-Path $icon) { $cscArgs += "/win32icon:$icon" }
$cscArgs += @("/out:$launcherExe", $launcherCs)

& $csc @cscArgs
if ($LASTEXITCODE -ne 0) { throw "Компиляция лончера не удалась (csc exit $LASTEXITCODE)" }
Remove-Item $launcherCs -ErrorAction SilentlyContinue
Write-Host "Лончер собран: $launcherExe"

# ── 5. Zip: IDABatchToolWinUI-<версия>-<rid>.zip ────────────────────────────
if (-not $NoZip) {
    $zip = Join-Path $dist "IDABatchToolWinUI-$AppVersion-$Rid.zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    # архивы прежних схем имён убираем, чтобы не путаться
    Get-ChildItem $dist -Filter "IDABatchTool-portable-*.zip" -ErrorAction SilentlyContinue |
        Remove-Item -Force
    Get-ChildItem $dist -Filter "IDABatchToolWinUI-$AppVersion.zip" -ErrorAction SilentlyContinue |
        Remove-Item -Force
    Compress-Archive -Path (Join-Path $staging "*") -DestinationPath $zip -CompressionLevel Optimal
    Write-Host "Архив: $zip ($('{0:N1}' -f ((Get-Item $zip).Length / 1MB)) МБ)"
}

# ── 5.5. Проверка зоны поставки (MOTW) ─────────────────────────────────────
# Файлы поставки обязаны быть вне зоны Internet: ShellExecute неподписанного
# exe из Internet-зоны показывает окно «Не удаётся проверить издателя».
# Правило и предыстория - AGENTS.md, раздел «Зоны MOTW / SmartScreen».
$zoneTargets = @((Join-Path $staging "IDABatchTool.exe"), (Join-Path $appDir "IDABatchToolWinUI.exe"))
if (-not $NoZip) { $zoneTargets += $zip }
foreach ($t in $zoneTargets) {
    if (-not (Test-Path $t)) { throw "Проверка зоны: файл не найден: $t" }
    $url = "file:///" + ($t -replace "\\", "/")
    $zone = [System.Security.Policy.Zone]::CreateFromUrl($url).SecurityZone
    if ($zone -ne [System.Security.SecurityZone]::MyComputer -and
        $zone -ne [System.Security.SecurityZone]::NoZone) {
        throw ("Файл в зоне '{0}': {1}. Папка сборки помечена зоной Internet - " +
               "перенесите dist вне помеченной папки (см. AGENTS.md, «Зоны MOTW»).") -f $zone, $t
    }
}
Write-Host "Зона поставки: чисто (MyComputer) - окно SmartScreen от зоны не появится"

# ── 6. Граф знаний (graphify): малый граф, свой код без вендоренной diaphora ─
# Полный граф (graphify-out-full, с diaphora) обновляется вручную - см. AGENTS.md.
if ($NoPublish) {
    Write-Host "Граф знаний: пропущен (-NoPublish)"
}
elseif ($SkipGraph) {
    Write-Host "Граф знаний: пропущен (-SkipGraph)"
}
elseif (-not (Get-Command graphify -ErrorAction SilentlyContinue)) {
    Write-Warning "graphify не установлен - граф не обновлён (pip install graphifyy)"
}
else {
    Push-Location $root
    try {
        if (Test-Path (Join-Path $root "graphify-out\graph.json")) {
            & graphify update .
        }
        else {
            # Первичная сборка; --exclude сохраняется в конфиг graphify-out/,
            # дальше update переиспользует его.
            & graphify . --code-only --exclude scripts/diaphora
            if ($LASTEXITCODE -eq 0) { & graphify cluster-only . }
        }
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "graphify завершился с кодом $LASTEXITCODE - граф не обновлён"
        }
        else {
            Write-Host "Граф знаний обновлён: graphify-out\GRAPH_REPORT.md"
        }
    }
    finally {
        Pop-Location
    }
}

# ── 7. Итог ─────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "Готово. Корень поставки ($staging):"
Get-ChildItem $staging | ForEach-Object {
    if ($_.PSIsContainer) { Write-Host ("  [dir ] " + $_.Name) }
    elseif ($_.Extension -eq ".exe") { Write-Host ("  [exe ] " + $_.Name + "  ($('{0:N1}' -f ($_.Length / 1KB)) КБ)") }
    else { Write-Host ("  [file] " + $_.Name + "  ($('{0:N1}' -f ($_.Length / 1KB)) КБ)") }
}
