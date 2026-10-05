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

    Дополнительно создаётся dist\IDABatchTool-portable-<rid>.zip.

.EXAMPLE
    .\make-portable.ps1                  # publish + раскладка + лончер + zip
.EXAMPLE
    .\make-portable.ps1 -NoPublish       # пересобрать раскладку из готового publish
.EXAMPLE
    .\make-portable.ps1 -NoZip           # без архивирования
.EXAMPLE
    .\make-portable.ps1 -Rid win-arm64 -NoZip
#>
param(
    [string]$Configuration = "Release",
    [string]$Rid = "win-x64",
    [switch]$NoPublish,
    [switch]$NoZip
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

# ── 1. Publish ──────────────────────────────────────────────────────────────
if (-not $NoPublish) {
    dotnet publish "$root\IDABatchToolWinUI.csproj" -c $Configuration -r $Rid
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
    throw "В $publishDir нет IDABatchToolWinUI.exe — публикация неполная."
}

# ── 2. Раскладка: всё содержимое publish -> dist\portable\app\ ─────────────
$dist    = Join-Path $root "dist"
$staging = Join-Path $dist "portable"
$appDir  = Join-Path $staging "app"

if (Test-Path $staging) { Remove-Item $staging -Recurse -Force }
New-Item -ItemType Directory -Path $appDir -Force | Out-Null
Copy-Item (Join-Path $publishDir "*") $appDir -Recurse -Force

# ── 3. bindiff.exe — в корень поставки ──────────────────────────────────────
$bindiff = Join-Path $appDir "bindiff.exe"
if (Test-Path $bindiff) {
    Move-Item $bindiff (Join-Path $staging "bindiff.exe") -Force
    Write-Host "bindiff.exe перенесён в корень поставки"
}
else {
    Write-Warning "bindiff.exe не найден в publish — корень будет без него (поиск BinDiff: конфиг/реестр/PATH)"
}

# ── 4. Лончер ───────────────────────────────────────────────────────────────
$launcherCs = Join-Path $env:TEMP "idabatchtool-launcher.cs"
@'
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

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
            UseShellExecute = true,
        };
        // ProcessStartInfo.ArgumentList недоступен в .NET Framework 4.8 —
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
'@ | Set-Content -Path $launcherCs -Encoding ASCII

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

# ── 5. Zip ──────────────────────────────────────────────────────────────────
if (-not $NoZip) {
    $zip = Join-Path $dist "IDABatchTool-portable-$Rid.zip"
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path (Join-Path $staging "*") -DestinationPath $zip -CompressionLevel Optimal
    Write-Host "Архив: $zip ($('{0:N1}' -f ((Get-Item $zip).Length / 1MB)) МБ)"
}

# ── 6. Итог ─────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "Готово. Корень поставки ($staging):"
Get-ChildItem $staging | ForEach-Object {
    if ($_.PSIsContainer) { Write-Host ("  [dir ] " + $_.Name) }
    else { Write-Host ("  [exe ] " + $_.Name + "  ($('{0:N1}' -f ($_.Length / 1KB)) КБ)") }
}
