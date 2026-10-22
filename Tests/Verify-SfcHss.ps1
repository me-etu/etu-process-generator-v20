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
$scratch = Join-Path ([IO.Path]::GetTempPath()) ('G016-HSS-tests-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($scratch)
$testAssembly = Join-Path $scratch 'SfcHssTests.dll'
$compiler = "$env:ProgramFiles/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/Roslyn/csc.exe"
& $compiler /nologo /target:library /langversion:7.3 "/out:$testAssembly" "/reference:$library" "/reference:$ApiDirectory/Siemens.Engineering.Base.dll" "/reference:$ApiDirectory/Siemens.Engineering.Step7.dll" "/reference:$ApiDirectory/Siemens.Engineering.WinCCUnified.dll" /reference:System.Xml.Linq.dll "$PSScriptRoot/SfcHss.cs"
if ($LASTEXITCODE) { throw 'Offline SFC HSS fixture compilation failed.' }
[void][Reflection.Assembly]::LoadFrom($testAssembly)
# Bypass all Portal/Project/Unit/Operation/Phase/DB constructors. Execute the shared
# controller call configuration path only. Never Upload, import or run the app.
[SfcHssTests]::Run((Resolve-Path "$PSScriptRoot/..").Path)

# Verify the three real constructor routes delegate to the executable shared path above.
$root = (Resolve-Path "$PSScriptRoot/..").Path
$routes = @{
    'XmlUnit.cs' = 'ConfigureSfcControl("Unit sfc control", "sfc_" + Name, out isNewControl)'
    'XmlOperation.cs' = 'ConfigureSfcControl(Name, "sfc_" + Name, out isNewControl)'
    'XmlPhase.cs' = 'Op.ConfigureSfcControl("Sfc control " + name, "sfc_" + name, out isNewControl)'
}
foreach ($file in $routes.Keys) {
    $source = [IO.File]::ReadAllText((Join-Path $root "TIAOpenness/TIA_Lib/TIA_LIB/Xml/$file"))
    if (-not $source.Contains($routes[$file])) { throw "Missing SFC HSS constructor integration: $file" }
}
Write-Output 'PASS: all three constructor routes use the tested controller configuration path.'
