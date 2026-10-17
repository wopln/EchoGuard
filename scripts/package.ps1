param([string]$OutputDirectory)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if(!$OutputDirectory){$OutputDirectory=Join-Path $root 'artifacts\release'}
$OutputDirectory=[IO.Path]::GetFullPath($OutputDirectory)
if(Test-Path -LiteralPath $OutputDirectory){if((Get-ChildItem -LiteralPath $OutputDirectory -Force).Count){throw 'Choose an empty output directory to avoid mixing release files.'}}
New-Item -ItemType Directory -Path $OutputDirectory -Force|Out-Null
& (Join-Path $PSScriptRoot 'build-installer.ps1')
& (Join-Path $PSScriptRoot 'test.ps1')
$staging=Join-Path $root ('artifacts\staging-'+[Guid]::NewGuid().ToString('N'))
$portable=Join-Path $staging 'EchoGuard'
$source=Join-Path $staging 'EchoGuard-source'
New-Item -ItemType Directory -Path $portable,$source -Force|Out-Null
Copy-Item -LiteralPath (Join-Path $root 'artifacts\EchoGuard.exe'),(Join-Path $root 'artifacts\EchoGuard.exe.config') -Destination $portable
foreach($name in @('README.md','LICENSE','CHANGELOG.md','SUPPORTED_CONFIGURATIONS.md','PRIVACY.md','THIRD_PARTY_NOTICES.md')){Copy-Item -LiteralPath (Join-Path $root $name) -Destination $portable;Copy-Item -LiteralPath (Join-Path $root $name) -Destination $source}
Copy-Item -LiteralPath (Join-Path $root 'docs') -Destination $portable -Recurse
foreach($name in @('src','scripts','tests','docs','.github')){Copy-Item -LiteralPath (Join-Path $root $name) -Destination $source -Recurse}
foreach($name in @('.gitignore','EchoGuard.csproj','CONTRIBUTING.md','SECURITY.md')){Copy-Item -LiteralPath (Join-Path $root $name) -Destination $source}
Compress-Archive -LiteralPath $portable -DestinationPath (Join-Path $OutputDirectory 'EchoGuard-1.0.0-win-x64.zip')
Compress-Archive -LiteralPath $source -DestinationPath (Join-Path $OutputDirectory 'EchoGuard-1.0.0-source.zip')
Copy-Item -LiteralPath (Join-Path $root 'artifacts\EchoGuardSetup.exe') -Destination $OutputDirectory
Get-ChildItem -LiteralPath $OutputDirectory -File|Get-FileHash -Algorithm SHA256|ForEach-Object {$_.Hash.ToLowerInvariant()+'  '+[IO.Path]::GetFileName($_.Path)}|Set-Content -LiteralPath (Join-Path $OutputDirectory 'SHA256SUMS.txt') -Encoding ascii
Write-Output "Packaged release in $OutputDirectory"
# Staging is intentionally retained under ignored artifacts for inspection. No recursive cleanup is needed.



