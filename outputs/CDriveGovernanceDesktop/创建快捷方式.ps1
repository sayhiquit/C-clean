$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Windows.Forms

$appDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$exe = Join-Path $appDir "MYL系统盘检测工具.exe"
$icon = Join-Path $appDir "MYL系统盘检测工具.ico"

if (-not (Test-Path -LiteralPath $exe)) {
  [System.Windows.Forms.MessageBox]::Show("找不到 MYL系统盘检测工具.exe，请确认脚本和主程序在同一目录。", "创建快捷方式失败")
  exit 1
}

$shell = New-Object -ComObject WScript.Shell
$desktop = [Environment]::GetFolderPath("DesktopDirectory")
$startMenu = Join-Path ([Environment]::GetFolderPath("StartMenu")) "Programs"

function New-Shortcut($path) {
  $shortcut = $shell.CreateShortcut($path)
  $shortcut.TargetPath = $exe
  $shortcut.WorkingDirectory = $appDir
  if (Test-Path -LiteralPath $icon) {
    $shortcut.IconLocation = $icon
  }
  $shortcut.Description = "MYL系统盘检测工具"
  $shortcut.Save()
}

New-Shortcut (Join-Path $desktop "MYL系统盘检测工具.lnk")
New-Shortcut (Join-Path $startMenu "MYL系统盘检测工具.lnk")

Write-Host "已创建桌面和开始菜单快捷方式。"
