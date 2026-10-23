param([string]$LibraryPath = "$PSScriptRoot/../etu-process-generator-TiAv21/bin/x64/Debug/TIA_LIB.dll")
$ErrorActionPreference = 'Stop'
$library = (Resolve-Path -LiteralPath $LibraryPath).Path
[void][Reflection.Assembly]::LoadFrom($library)
$scratch = Join-Path ([IO.Path]::GetTempPath()) ('G017-ownership-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($scratch)
$testAssembly = Join-Path $scratch 'ManagedArtifactOwnershipTests.dll'
$compiler = "$env:ProgramFiles/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/Roslyn/csc.exe"
& $compiler /nologo /target:library /langversion:7.3 "/out:$testAssembly" "/reference:$library" /reference:System.Xml.Linq.dll "$PSScriptRoot/ManagedArtifactOwnership.cs"
if ($LASTEXITCODE) { throw 'Offline ownership fixture compilation failed.' }
[void][Reflection.Assembly]::LoadFrom($testAssembly)
[ManagedArtifactOwnershipTests]::Run()
