param(
    [string]$LibraryPath = "$PSScriptRoot/../etu-process-generator-TiAv21/bin/x64/Debug/TIA_LIB.dll",
    [string]$ApiDirectory = "$env:ProgramFiles/Siemens/Automation/Portal V21/PublicAPI/V21/net48"
)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path "$PSScriptRoot/..").Path
$library = (Resolve-Path -LiteralPath $LibraryPath).Path
foreach ($module in 'Base', 'Step7') {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $ApiDirectory "Siemens.Engineering.$module.dll"))
}
[void][Reflection.Assembly]::LoadFrom($library)
$scratch = Join-Path ([IO.Path]::GetTempPath()) ('G014-tests-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($scratch)
$testAssembly = Join-Path $scratch 'OperationInterlockTests.dll'
$compiler = "$env:ProgramFiles/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/Roslyn/csc.exe"
& $compiler /nologo /target:library /langversion:7.3 "/out:$testAssembly" "/reference:$library" "/reference:$ApiDirectory/Siemens.Engineering.Base.dll" "/reference:$ApiDirectory/Siemens.Engineering.Step7.dll" /reference:System.Xml.Linq.dll "$PSScriptRoot/OperationInterlocks.cs"
if ($LASTEXITCODE -ne 0) { throw 'Offline fixture compilation failed.' }
[void][Reflection.Assembly]::LoadFrom($testAssembly)
# Pure source/planning checks and uninitialized XML-only call builder. No Portal constructors,
# external sources, Upload, or actual Siemens compiler calls are permitted in this fixture.
[OperationInterlockTests]::Run($root)

# Check the real integration/deployment boundary in addition to the pure fixtures.
foreach ($version in '20', '21') {
    $program = [IO.File]::ReadAllText((Join-Path $root "etu-process-generator-TiAv$version/Program.cs"))
    $selection = $program.IndexOf('PlcProject.InterlockLanguage = OperationInterlockConfiguration.Select(Console.In, Console.Out);')
    if ($selection -lt 0 -or $selection -gt $program.IndexOf('new Project();')) { throw "V$version selector is missing or too late." }
}
[xml]$v20 = Get-Content -LiteralPath (Join-Path $root 'TIAOpenness/TIA_Lib/TIA_LIB/TIA_LIB.csproj')
[xml]$v21 = Get-Content -LiteralPath (Join-Path $root 'TIAOpenness/TIA_Lib/TIA_LIB/TIA_LIB.V21.csproj')
foreach ($item in 'Compile', 'Content') {
    $left = @($v20.Project.ItemGroup.$item | Where-Object { $_ } | ForEach-Object { $_.Include } | Sort-Object)
    $right = @($v21.Project.ItemGroup.$item | Where-Object { $_ } | ForEach-Object { $_.Include } | Sort-Object)
    if (@(Compare-Object $left $right).Count) { throw "V20/V21 $item item lists differ." }
}
Write-Output 'PASS: both app selectors precede model construction; V20/V21 Compile/Content lists match.'
