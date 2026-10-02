param([switch]$Unity, [switch]$Build)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
dotnet run --project (Join-Path $root 'Validation\CoreChecks.csproj')
if ($LASTEXITCODE -ne 0) { throw 'Core rule checks failed.' }
dotnet build (Join-Path $root 'Validation\UnityCompile.csproj') --nologo -v minimal
if ($LASTEXITCODE -ne 0) { throw 'Unity API reference compilation failed.' }
python (Join-Path $PSScriptRoot 'verify_assets.py')
if ($LASTEXITCODE -ne 0) { throw 'Asset checks failed.' }
if ($Unity -or $Build) {
    $editor = 'C:\Program Files\Unity\Hub\Editor\6000.3.16f1\Editor\Unity.exe'
    $method = if ($Build) { 'Doctor.Editor.BuildPrototype.Build' } else { 'Doctor.Editor.BuildPrototype.VerifyScene' }
    $log = Join-Path $root 'logs\unity-verification.log'
    $arguments = '-batchmode -nographics -quit -projectPath "' + (Join-Path $root 'DoctorPrototype') + '" -executeMethod ' + $method + ' -logFile "' + $log + '"'
    $run = Start-Process -FilePath $editor -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $run.WaitForExit()
    if ($run.ExitCode -ne 0) { throw "Unity exited with $($run.ExitCode). See $log" }
}
