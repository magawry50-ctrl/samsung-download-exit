$ErrorActionPreference = 'Stop'

$project = Join-Path $PSScriptRoot 'SamsungDownloadExit\SamsungDownloadExit.csproj'
$publish = Join-Path $PSScriptRoot 'SamsungDownloadExit\bin\Release\net10.0-windows\win-x64\publish'
$release = Join-Path $PSScriptRoot 'Release'

dotnet publish $project -c Release -r win-x64 --self-contained true

New-Item -ItemType Directory -Path $release -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $publish 'Samsung Download Exit.exe') -Destination $release -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'README.md') -Destination $release -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'THIRD-PARTY-NOTICES.md') -Destination $release -Force

Write-Host "Release created at: $release"
