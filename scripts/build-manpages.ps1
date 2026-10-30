#Requires -Version 5.1
<#
.SYNOPSIS
    Сборка встроенной документации man-pages (docs\manpages.db).

.DESCRIPTION
    Скачивает корпуса в кэш (obj\manpages-corpus), распаковывает man-страницы,
    рендерит их в HTML (pandoc) и собирает SQLite-БД:

      * RU - man-pages-ru (проект manpages-l10n, po4a-переводы) с зеркала
        Arch Linux: man1-man8, ~890 страниц (переведённая часть);
      * EN - man-pages с kernel.org: man1-man8 c подразделами
        (man3type, man2const, ...), fallback для непереведённых страниц.

    Результат:
      docs\manpages.db            - БД (схема 2: pages/function_index с lang)
      docs\MANPAGES-SOURCES.txt   - версии, источники, дата сборки

    Рендерер: pandoc. Если его нет - установка:
        winget install --id JohnMacFarlane.Pandoc -e

.EXAMPLE
    .\build-manpages.ps1              # собрать (кэш корпусов переиспользуется)
    .\build-manpages.ps1 -Force       # пересобрать БД, даже если она есть
#>
param([switch]$Force)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent

# ── Версии корпусов (снапшот на релиз; обновляется вручную) ────────────────
$RuVersion = "4.31.0"   # manpages-l10n (пакет man-pages-ru, Arch extra)
$RuPkgRev  = "1"
$EnVersion = "6.18"     # man-pages (kernel.org)

$RuUrl = "https://geo.mirror.pkgbuild.com/extra/os/x86_64/man-pages-ru-$RuVersion-$RuPkgRev-any.pkg.tar.zst"
$EnUrl = "https://www.kernel.org/pub/linux/docs/man-pages/man-pages-$EnVersion.tar.xz"

# Кэш корпусов - ВНЕ репозитория: файлы, скачанные Invoke-WebRequest, получают
# метку «интернет» (Zone.Identifier), а на томах Windows 11 с новой реализацией
# MOTW метка распространяется на папку, и всё новое содержимое внутри (включая
# dist) наследует зону Internet - отсюда окно «Не удаётся проверить издателя»
# при запуске неподписанного exe. Кэш в %LOCALAPPDATA% и Unblock-File сразу
# после скачивания этого не допускают.
$cache   = Join-Path $env:LOCALAPPDATA "IDABatchTool\manpages-corpus"
$docsDir = Join-Path $root "docs"
$dbPath  = Join-Path $docsDir "manpages.db"

# Готовая БД отменяет всю сборку: ни pandoc, ни python, ни сеть не нужны.
# Проверка стоит ДО требований к инструментам - иначе сборка поставки падает
# на машине без pandoc, хотя docs\manpages.db уже лежит в репозитории.
if ((Test-Path $dbPath) -and -not $Force) {
    Write-Host "docs\manpages.db уже собран - пропуск (пересборка: build-manpages.ps1 -Force)"
    return
}

New-Item -ItemType Directory -Force -Path $cache, $docsDir | Out-Null

# ── 0. pandoc ───────────────────────────────────────────────────────────────
$pandoc = @(
    (Get-Command pandoc -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source),
    (Join-Path $env:LOCALAPPDATA "Pandoc\pandoc.exe"),
    "C:\Program Files\Pandoc\pandoc.exe"
) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $pandoc) {
    throw "pandoc не найден. Установите: winget install --id JohnMacFarlane.Pandoc -e"
}
Write-Host "Рендерер: $pandoc (& $(& $pandoc --version | Select-Object -First 1))"

# ── 1. Кэш корпусов ─────────────────────────────────────────────────────────
$ruPkg  = Join-Path $cache "man-pages-ru-$RuVersion-$RuPkgRev-any.pkg.tar.zst"
$enPkg  = Join-Path $cache "man-pages-$EnVersion.tar.xz"
$ruDir  = Join-Path $cache "ru-man"
$enDir  = Join-Path $cache "en-man"
$python = (Get-Command python -ErrorAction SilentlyContinue).Source
if (-not $python) { throw "python не найден в PATH (нужен 3.14+ для распаковки .tar.zst)" }

if (-not (Test-Path $ruPkg)) {
    Write-Host "Скачивание man-pages-ru $RuVersion (Arch)…"
    Invoke-WebRequest -Uri $RuUrl -OutFile $ruPkg -UseBasicParsing
    Unblock-File -LiteralPath $ruPkg -ErrorAction SilentlyContinue
}
if (-not (Test-Path (Join-Path $ruDir "man1"))) {
    Write-Host "Распаковка man-pages-ru…"
    if (Test-Path $ruDir) { Remove-Item $ruDir -Recurse -Force }
    & $python (Join-Path $PSScriptRoot "extract_corpus.py") $ruPkg $ruDir
    if ($LASTEXITCODE -ne 0) { throw "Распаковка man-pages-ru не удалась" }
}
if (-not (Test-Path $enPkg)) {
    Write-Host "Скачивание man-pages $EnVersion (kernel.org)…"
    Invoke-WebRequest -Uri $EnUrl -OutFile $enPkg -UseBasicParsing
    Unblock-File -LiteralPath $enPkg -ErrorAction SilentlyContinue
}
if (-not (Test-Path (Join-Path $enDir "man1"))) {
    Write-Host "Распаковка man-pages…"
    if (Test-Path $enDir) { Remove-Item $enDir -Recurse -Force }
    & $python (Join-Path $PSScriptRoot "extract_corpus.py") $enPkg $enDir
    if ($LASTEXITCODE -ne 0) { throw "Распаковка man-pages не удалась" }
}

# ── 2. Сборка БД ────────────────────────────────────────────────────────────
$buildPy = Join-Path $root "_python\manpages_build.py"
Write-Host "Сборка manpages.db (рендер ~4000 страниц, несколько минут)…"
& $python $buildPy `
    --ru-dir $ruDir --en-dir $enDir --out $dbPath --pandoc $pandoc `
    --ru-version $RuVersion --en-version $EnVersion --jobs 12
if ($LASTEXITCODE -ne 0) { throw "Сборка manpages.db не удалась" }

# ── 3. Источники корпусов ──────────────────────────────────────────────────
$sources = @"
Встроенная документация man-pages - источники
==============================================
Собрано: $((Get-Date).ToString("yyyy-MM-dd HH:mm"))
Сборка:  scripts\build-manpages.ps1 -> _python\manpages_build.py

man-pages-ru (русские страницы):
  версия:  $RuVersion (manpages-l10n)
  источник: $RuUrl

man-pages (английский fallback):
  версия:  $EnVersion
  источник: $EnUrl

Рендер: pandoc (roff man -> HTML). База: schema=2,
pages(page_name, section, lang), function_index(func_name, ...).
Приоритет выбора страницы: секция 3 > 2 > 7 > 1 > 5 > 4 > 8 > 6,
внутри секции ru > en.
"@
Set-Content -Path (Join-Path $docsDir "MANPAGES-SOURCES.txt") -Value $sources -Encoding UTF8

Write-Host "Готово: $dbPath (источники в docs\MANPAGES-SOURCES.txt)"
