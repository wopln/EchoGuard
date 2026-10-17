param([string]$ResultPath)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
New-Item -ItemType Directory -Path (Join-Path $root 'artifacts') -Force|Out-Null
$framework=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$files=@('Selection.cs','AudioInventory.cs','ChangePolicy.cs','Models.cs','Native.cs','Identity.cs','LocalStore.cs','AudioEngine.cs')|ForEach-Object {Join-Path $root ('src\EchoGuard\'+$_)}
$exe=Join-Path $root 'artifacts\EchoGuard.NativeDetectionTests.exe'
$build=@('/nologo','/target:exe','/platform:x64','/main:NativeDetectionTests',('/out:'+$exe),('/reference:'+(Join-Path $framework 'System.Web.Extensions.dll')))+$files
$build+=Join-Path $root 'tests\NativeDetectionTests.cs'
& (Join-Path $framework 'csc.exe') @build
if($LASTEXITCODE -ne 0){throw 'Native detection test compilation failed'}
if(!$ResultPath){$ResultPath=Join-Path $env:TEMP ('EchoGuard-native-detection-'+[Guid]::NewGuid().ToString('N')+'.json')}
& $exe $ResultPath
if($LASTEXITCODE -ne 0){throw 'Native detection tests failed'}
Write-Output "Redacted native test result: $ResultPath"
