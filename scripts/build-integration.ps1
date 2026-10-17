$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
New-Item -ItemType Directory -Path (Join-Path $root 'artifacts') -Force|Out-Null
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$files=@('Selection.cs','AudioInventory.cs','ChangePolicy.cs','Models.cs','Native.cs','Identity.cs','LocalStore.cs','AudioEngine.cs')|ForEach-Object {Join-Path $root ('src\EchoGuard\'+$_)}
$argsNative=@('/nologo','/target:exe','/platform:x64','/main:Integration',('/out:'+(Join-Path $root 'artifacts\EchoGuard.Integration.exe')),('/reference:'+(Join-Path $framework 'System.Web.Extensions.dll')))
$argsNative+=$files
$argsNative+=Join-Path $root 'tests\NativeIntegration.cs'
& (Join-Path $framework 'csc.exe') @argsNative
if($LASTEXITCODE -ne 0){throw 'Integration harness compilation failed'}



