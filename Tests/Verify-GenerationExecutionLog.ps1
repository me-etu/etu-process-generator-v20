param([string]$LibraryPath = "$PSScriptRoot/../etu-process-generator-TiAv21/bin/x64/Debug/TIA_LIB.dll")
$ErrorActionPreference = 'Stop'
$library = (Resolve-Path -LiteralPath $LibraryPath).Path
[void][Reflection.Assembly]::LoadFrom($library)
$scratch = Join-Path ([IO.Path]::GetTempPath()) ('ETU-logging-tests-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($scratch)
$testAssembly = Join-Path $scratch 'GenerationExecutionLogTests.dll'
$compiler = "$env:ProgramFiles/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/Roslyn/csc.exe"
& $compiler /nologo /target:library /langversion:7.3 "/out:$testAssembly" "/reference:$library" "$PSScriptRoot/GenerationExecutionLog.cs"
if ($LASTEXITCODE) { throw 'Offline execution-log fixture compilation failed.' }
[void][Reflection.Assembly]::LoadFrom($testAssembly)
[GenerationExecutionLogTests]::Run()
$root = (Resolve-Path "$PSScriptRoot/..").Path
foreach ($version in '20', '21') {
    $program = [IO.File]::ReadAllText((Join-Path $root "etu-process-generator-TiAv$version/Program.cs"))
    if (-not $program.Contains("return TIA_LIB.Generation.GenerationExecutionLog.Run(`"ETU generator V$version`", () => RunGeneration(args)")) {
        throw "V$version entry point does not return the logged execution status."
    }
    if (-not $program.Contains('Select signal IOs generation style:') -or $program.Contains('(current default)') -or $program.Contains('(experimental)')) {
        throw "V$version signal prompt includes unwanted wording."
    }
    if (-not $program.Contains('pauseOnFailure: true')) { throw "V$version console can close before the failure is reviewed." }
}
Write-Output 'PASS: V20/V21 entry points route complete generation through the error logger.'
