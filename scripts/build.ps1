param([string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if(!$OutputDirectory){$OutputDirectory=Join-Path $root 'artifacts'}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force|Out-Null
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler=Join-Path $framework 'csc.exe'
$wpf=Join-Path $framework 'WPF'
$source=Join-Path $root 'src\EchoGuard'
$refs=@('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll','System.Xaml.dll')|ForEach-Object {Join-Path $framework $_}
$refs+=@('PresentationCore.dll','PresentationFramework.dll','WindowsBase.dll')|ForEach-Object {Join-Path $wpf $_}
$argsBuild=@('/nologo','/target:winexe','/platform:x64','/optimize+','/debug-','/main:EchoGuard.Entry',('/out:'+(Join-Path $OutputDirectory 'EchoGuard.exe')),('/win32manifest:'+(Join-Path $source 'app.manifest')),('/resource:'+(Join-Path $source 'MainWindow.xaml')+',EchoGuard.MainWindow.xaml'))
foreach($name in @('Theme.xaml','SettingsWindow.xaml')){$argsBuild+=('/resource:'+(Join-Path $source $name)+',EchoGuard.'+$name)}
$argsBuild+=('/win32icon:'+(Join-Path $source 'Assets\EchoGuard.ico'))
$argsBuild+=('/resource:'+(Join-Path $source 'Assets\EchoGuard.ico')+',EchoGuard.Icon.ico')
$argsBuild+=@($refs|ForEach-Object {'/reference:'+$_})
$argsBuild+=@(Get-ChildItem -LiteralPath $source -Filter *.cs|ForEach-Object FullName)
& $compiler @argsBuild
if($LASTEXITCODE -ne 0){throw 'EchoGuard compilation failed'}
Copy-Item -LiteralPath (Join-Path $source 'App.config') -Destination (Join-Path $OutputDirectory 'EchoGuard.exe.config') -Force
Write-Output "Built $OutputDirectory"


