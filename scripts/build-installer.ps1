param([string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if(!$OutputDirectory){$OutputDirectory=Join-Path $root 'artifacts'}
& (Join-Path $PSScriptRoot 'build.ps1') -OutputDirectory $OutputDirectory
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler=Join-Path $framework 'csc.exe'
$source=Join-Path $root 'src\EchoGuard.Setup'
$manifest=Join-Path $root 'src\EchoGuard\app.manifest'
$cleanup=Join-Path $OutputDirectory 'EchoGuard.Cleanup.exe'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /debug- ('/out:'+$cleanup) ('/win32manifest:'+$manifest) (Join-Path $source 'Cleanup.cs')
if($LASTEXITCODE -ne 0){throw 'Cleanup compilation failed'}
$build=@('/nologo','/target:winexe','/platform:x64','/optimize+','/debug-',('/out:'+(Join-Path $OutputDirectory 'EchoGuardSetup.exe')),('/win32manifest:'+$manifest))
$build+=('/win32icon:'+(Join-Path $root 'src\EchoGuard\Assets\EchoGuard.ico'))
$build+=@('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Web.Extensions.dll')|ForEach-Object {'/reference:'+(Join-Path $framework $_)}
$resources=@(@((Join-Path $OutputDirectory 'EchoGuard.exe'),'Payload.EchoGuard.exe'),@((Join-Path $OutputDirectory 'EchoGuard.exe.config'),'Payload.Config'),@((Join-Path $root 'LICENSE'),'Payload.License'),@((Join-Path $root 'README.md'),'Payload.Readme'),@($cleanup,'Payload.Cleanup'))
$resources+=,@((Join-Path $root 'docs\images\app.png'),'Payload.Screenshot')
foreach($name in @('ARCHITECTURE.md','DELIVERY_VALIDATION.md','SMART_DETECTION.md','TESTING.md','TROUBLESHOOTING.md')){$resources+=,@((Join-Path $root ('docs\'+$name)),('Payload.Doc.'+$name))}
foreach($name in @('PRIVACY.md','SUPPORTED_CONFIGURATIONS.md','CHANGELOG.md')){$resources+=,@((Join-Path $root $name),('Payload.Root.'+$name))}
$build+=@($resources|ForEach-Object {'/resource:'+$_[0]+','+$_[1]})
$build+=Join-Path $source 'Installer.cs'
$build+=Join-Path $source 'AssemblyInfo.cs'
& $compiler @build
if($LASTEXITCODE -ne 0){throw 'Installer compilation failed'}
Write-Output 'Built per-user EchoGuardSetup.exe'


