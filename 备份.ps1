# 一键备份源码
#
# 用法：在项目根目录（有 .sln 的那个文件夹）右键 "使用 PowerShell 运行"，
#       或者命令行里执行：
#         pwsh -File .\备份.ps1
#
# 它会把"源码 + 资源"复制到 _备份_年月日_时分 文件夹，跳过 bin/obj 这些编译垃圾。
# 备份前会先做一次编译检查：源码编译不过就不备份（免得存下一个坏的版本）。

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$stamp = Get-Date -Format 'yyyy-MM-dd_HHmm'
$backup = Join-Path $root "_备份_$stamp"

Write-Host "项目根目录: $root" -ForegroundColor Cyan

# ---------- 1) 先确认能编译（且零警告）----------
Write-Host "编译检查中（警告也算失败）..." -ForegroundColor Cyan

$sln = Join-Path $root 'mEmEmE_Act.Desktop\mEmEmE_Act.Desktop.csproj'
if (-not (Test-Path $sln)) {
    $sln = Get-ChildItem $root -Filter '*.Desktop.csproj' -Recurse |
           Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
           Select-Object -First 1 -ExpandProperty FullName
}

if ($sln) {
    # -warnaserror：现在整个项目是 0 警告，一旦冒出新的就当场拦住。
    # 不想要这么严的话，把 -warnaserror 删掉即可。
    $out = & dotnet build $sln -v quiet --nologo -warnaserror 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "编译没通过，已中止备份（修好再备份）：" -ForegroundColor Red
        $out | Select-String -Pattern 'error|warning' | Select-Object -First 10 | ForEach-Object { Write-Host "  $_" }
        exit 1
    }
    Write-Host "  编译通过，零警告" -ForegroundColor Green
} else {
    Write-Host "  没找到 .csproj，跳过编译检查" -ForegroundColor Yellow
}

# ---------- 2) 复制源码 ----------
if (Test-Path $backup) { throw "备份目录已存在: $backup" }

$copied = 0
Get-ChildItem $root -Recurse -File |
    Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' -and $_.FullName -notmatch '\\_备份_' } |
    ForEach-Object {
        $rel = $_.FullName.Substring($root.Length).TrimStart('\')
        $dest = Join-Path $backup $rel
        $dir = Split-Path $dest -Parent
        if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
        Copy-Item $_.FullName $dest -Force
        $copied++
    }

$size = [math]::Round((Get-ChildItem $backup -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1MB, 1)
Write-Host ""
Write-Host "备份完成：$copied 个文件，$size MB" -ForegroundColor Green
Write-Host "  $backup" -ForegroundColor Green
Write-Host ""
Write-Host "提示：备份越攒越多会占地方，确认没问题后可以删掉旧的 _备份_* 文件夹。" -ForegroundColor DarkGray
