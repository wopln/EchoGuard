$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
New-Item -ItemType Directory -Path (Join-Path $root 'artifacts') -Force|Out-Null
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$out=Join-Path $root 'artifacts\EchoGuard.Tests.exe'
$files=@('Presentation.cs','Selection.cs','AudioInventory.cs','ChangePolicy.cs','Models.cs','Native.cs','Identity.cs','LocalStore.cs','AudioEngine.cs')|ForEach-Object {Join-Path $root ('src\EchoGuard\'+$_)}
$argsTest=@('/nologo','/target:exe','/platform:x64','/optimize+','/main:Tests',('/out:'+$out),('/reference:'+(Join-Path $framework 'System.Web.Extensions.dll')))
$argsTest+=$files
$argsTest+=Join-Path $root 'tests\RulesTests.cs'
$argsTest+=Join-Path $root 'tests\SmartSelectionTests.cs'
$argsTest+=Join-Path $root 'tests\UiPresentationTests.cs'
& (Join-Path $framework 'csc.exe') @argsTest
if($LASTEXITCODE -ne 0){throw 'Test compilation failed'}
& $out
if($LASTEXITCODE -ne 0){throw 'Tests failed'}






