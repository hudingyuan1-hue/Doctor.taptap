param([string]$Editor = 'C:\Program Files\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe')
$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'DoctorPrototype'
if (-not (Test-Path -LiteralPath $Editor)) { throw "Unity editor not found: $Editor" }
if (-not (Test-Path -LiteralPath (Join-Path $project 'Assets\Doctor\Scenes\FirstRoom.unity'))) { throw 'FirstRoom scene is missing.' }
# Interactive editor requested by the launch script; intentionally visible.
Start-Process -FilePath $Editor -ArgumentList @('-projectPath', ('"' + $project + '"'), '-openfile', ('"' + (Join-Path $project 'Assets\Doctor\Scenes\FirstRoom.unity') + '"'))
