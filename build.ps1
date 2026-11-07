# 重新编译「错题锁屏」
#
# 用法：在这个文件夹里右键 build.ps1 → 使用 PowerShell 运行
#      或者在 PowerShell 里执行：  .\build.ps1
#
# 说明：程序用 Windows 自带的 .NET Framework 编译器（csc.exe）编译，
#      不需要安装 Visual Studio / .NET SDK，也不需要联网。

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
$srcDir = Join-Path $root 'src'
$outExe = Join-Path $root '错题锁屏.exe'

$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) {
    Write-Host '找不到 .NET Framework 编译器 csc.exe。' -ForegroundColor Red
    Write-Host '这台机器需要装有 .NET Framework 4.x（Windows 自带）。'
    exit 1
}

$sources = Get-ChildItem (Join-Path $srcDir '*.cs') | ForEach-Object { $_.FullName }
if ($sources.Count -eq 0) {
    Write-Host 'src 目录里没有 .cs 源文件。' -ForegroundColor Red
    exit 1
}

$args = @(
    '/nologo',
    '/target:winexe',
    '/platform:anycpu',
    '/optimize+',
    '/codepage:65001',
    ('/out:' + $outExe),
    ('/win32icon:' + (Join-Path $root 'app.ico')),
    '/r:System.dll',
    '/r:System.Core.dll',
    '/r:System.Drawing.dll',
    '/r:System.Windows.Forms.dll',
    '/r:System.Runtime.Serialization.dll',
    '/r:System.Xml.dll'
) + $sources

Write-Host ('正在编译 ' + $sources.Count + ' 个源文件 ...')
& $csc $args

if ($LASTEXITCODE -ne 0) {
    Write-Host ('编译失败，退出码 ' + $LASTEXITCODE) -ForegroundColor Red
    exit $LASTEXITCODE
}

$f = Get-Item $outExe
Write-Host ('编译成功：' + $f.FullName) -ForegroundColor Green
Write-Host ('大小：' + [math]::Round($f.Length / 1KB, 1) + ' KB')
