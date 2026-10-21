param(
    [string]$LibraryPath = "$PSScriptRoot/../etu-process-generator-TiAv21/bin/x64/Debug/TIA_LIB.dll",
    [string]$ApiDirectory = "$env:ProgramFiles/Siemens/Automation/Portal V21/PublicAPI/V21/net48"
)
$ErrorActionPreference = 'Stop'
$library = (Resolve-Path -LiteralPath $LibraryPath).Path
foreach ($module in 'Base', 'Step7', 'WinCCUnified', 'WinCC', 'Sivarc') {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $ApiDirectory "Siemens.Engineering.$module.dll"))
}
[void][Reflection.Assembly]::LoadFrom($library)
$scratch = Join-Path ([IO.Path]::GetTempPath()) ('A2-staging-tests-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($scratch)
$testAssembly = Join-Path $scratch 'DeviceStagingInputTests.dll'
$compiler = "$env:ProgramFiles/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/Roslyn/csc.exe"
& $compiler /nologo /target:library /langversion:7.3 "/out:$testAssembly" "/reference:$library" "/reference:$ApiDirectory/Siemens.Engineering.Base.dll" "/reference:$ApiDirectory/Siemens.Engineering.Step7.dll" "/reference:$ApiDirectory/Siemens.Engineering.WinCCUnified.dll" /reference:System.Xml.Linq.dll "$PSScriptRoot/DeviceStagingInputs.cs"
if ($LASTEXITCODE) { throw 'Offline device staging fixture compilation failed.' }
[void][Reflection.Assembly]::LoadFrom($testAssembly)
# Bypass all Portal/Project/Unit/DB constructors. The fake Portal has only in-memory DB
# caches and null session/software fields. Never Upload, import, compile or run the app.
[DeviceStagingInputTests]::Run((Resolve-Path "$PSScriptRoot/..").Path)
