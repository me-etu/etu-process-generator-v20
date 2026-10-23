param([string]$LibraryPath = "$PSScriptRoot/../etu-process-generator-TiAv21/bin/x64/Debug/TIA_LIB.dll")
$ErrorActionPreference = 'Stop'
$library = (Resolve-Path -LiteralPath $LibraryPath).Path
[void][Reflection.Assembly]::LoadFrom($library)
$apiDirectory = "$env:ProgramFiles/Siemens/Automation/Portal V21/PublicAPI/V21/net48"
foreach ($module in 'Base', 'Step7', 'WinCCUnified') {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $apiDirectory "Siemens.Engineering.$module.dll"))
}
$scratch = Join-Path ([IO.Path]::GetTempPath()) ('G017-phase-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($scratch)
$testAssembly = Join-Path $scratch 'GeneratedPhaseModelTests.dll'
$compiler = "$env:ProgramFiles/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/Roslyn/csc.exe"
& $compiler /nologo /target:library /langversion:7.3 "/out:$testAssembly" "/reference:$library" "/reference:$apiDirectory/Siemens.Engineering.Base.dll" "/reference:$apiDirectory/Siemens.Engineering.Step7.dll" /reference:System.Xml.Linq.dll "$PSScriptRoot/GeneratedPhaseModel.cs"
if ($LASTEXITCODE) { throw 'Offline phase fixture compilation failed.' }
[void][Reflection.Assembly]::LoadFrom($testAssembly)
[GeneratedPhaseModelTests]::Run((Resolve-Path "$PSScriptRoot/..").Path)
