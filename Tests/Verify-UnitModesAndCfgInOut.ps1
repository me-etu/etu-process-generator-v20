param(
    [string]$LibraryPath = "$PSScriptRoot/../etu-process-generator-TiAv21/bin/x64/Debug/TIA_LIB.dll",
    [string]$ContractSnapshotDirectory
)
$ErrorActionPreference = 'Stop'
$library = (Resolve-Path -LiteralPath $LibraryPath).Path
$api = "$env:ProgramFiles/Siemens/Automation/Portal V21/PublicAPI/V21/net48"
foreach ($module in 'Base', 'Step7', 'WinCCUnified', 'WinCC', 'Sivarc') { [void][Reflection.Assembly]::LoadFrom("$api/Siemens.Engineering.$module.dll") }
[void][Reflection.Assembly]::LoadFrom($library)
$scratch = Join-Path ([IO.Path]::GetTempPath()) ('G019-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($scratch)
$assembly = Join-Path $scratch 'UnitModesAndCfgInOut.dll'
$compiler = "$env:ProgramFiles/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/Roslyn/csc.exe"
& $compiler /nologo /target:library /langversion:7.3 "/out:$assembly" "/reference:$library" "/reference:$api/Siemens.Engineering.Base.dll" "/reference:$api/Siemens.Engineering.Step7.dll" "/reference:$api/Siemens.Engineering.WinCCUnified.dll" /reference:System.Xml.Linq.dll "$PSScriptRoot/UnitModesAndCfgInOut.cs" "$PSScriptRoot/DeviceStagingInputs.cs" "$PSScriptRoot/GeneratedNetworkModel.cs"
if ($LASTEXITCODE) { throw 'G-019 offline fixture compilation failed.' }
[void][Reflection.Assembly]::LoadFrom($assembly)
$previous = [IO.Directory]::GetCurrentDirectory()
try {
    [IO.Directory]::SetCurrentDirectory((Split-Path -Parent $library))
    [UnitModesAndCfgInOutTests]::Run((Resolve-Path "$PSScriptRoot/..").Path)
    if ($ContractSnapshotDirectory) {
        [UnitModesAndCfgInOutTests]::ValidateRetainedValveContracts($ContractSnapshotDirectory)
    }
}
catch { Write-Host $_.Exception.ToString(); throw }
finally { [IO.Directory]::SetCurrentDirectory($previous) }
