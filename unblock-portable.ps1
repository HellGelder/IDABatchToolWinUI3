#Requires -Version 5.1
<#
.SYNOPSIS
    Снимает метку «загружено из интернета» (Mark of the Web) со всех файлов
    поставки IDABatchTool.

.DESCRIPTION
    Запускать из корня распакованной поставки (рядом с IDABatchTool.exe).
    Окно SmartScreen «Windows защитила ваш компьютер» и предупреждение
    «неизвестный издатель» появляются только у файлов с меткой Zone.Identifier;
    скрипт снимает её рекурсивно со всех файлов поставки.

    Если сам скрипт заблокирован системой, запустите его так:
        powershell -ExecutionPolicy Bypass -File .\unblock-portable.ps1

    Альтернатива вручную: правый клик по IDABatchTool.exe -> Свойства ->
    «Разблокировать» (или в окне SmartScreen: «Подробнее» -> «Выполнить
    в любом случае»).
#>
$ErrorActionPreference = "Stop"
$root = $PSScriptRoot

$processed = 0
Get-ChildItem -LiteralPath $root -Recurse -File | ForEach-Object {
    Unblock-File -LiteralPath $_.FullName -ErrorAction SilentlyContinue
    $processed++
}

Write-Host ("Обработано файлов: " + $processed)
Write-Host "Готово. Запускайте IDABatchTool.exe - предупреждение больше не появится."
