param([string]$LibraryPath = "$PSScriptRoot/../etu-process-generator-TiAv21/bin/x64/Debug/TIA_LIB.dll")
$ErrorActionPreference = 'Stop'
$library = (Resolve-Path -LiteralPath $LibraryPath).Path
[void][Reflection.Assembly]::LoadFrom($library)
$scratch = Join-Path ([IO.Path]::GetTempPath()) ('G017-phase-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($scratch)
$testAssembly = Join-Path $scratch 'GeneratedPhaseModelTests.dll'
$compiler = "$env:ProgramFiles/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/Roslyn/csc.exe"
& $compiler /nologo /target:library /langversion:7.3 "/out:$testAssembly" "/reference:$library" /reference:System.Xml.Linq.dll "$PSScriptRoot/GeneratedNetworkModel.cs" "$PSScriptRoot/GeneratedPhaseModel.cs"
if ($LASTEXITCODE) { throw 'Offline phase fixture compilation failed.' }
[void][Reflection.Assembly]::LoadFrom($testAssembly)
[GeneratedPhaseModelTests]::Run((Resolve-Path "$PSScriptRoot/..").Path)
