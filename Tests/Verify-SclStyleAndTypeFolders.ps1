param([string]$LibraryPath = "$PSScriptRoot/../etu-process-generator-TiAv21/bin/x64/Debug/TIA_LIB.dll")
$ErrorActionPreference = 'Stop'
$library = (Resolve-Path -LiteralPath $LibraryPath).Path
$apiDirectory = "$env:ProgramFiles/Siemens/Automation/Portal V21/PublicAPI/V21/net48"
foreach ($module in 'Base', 'Step7', 'WinCCUnified', 'WinCC', 'Sivarc') {
    [void][Reflection.Assembly]::LoadFrom("$apiDirectory/Siemens.Engineering.$module.dll")
}
[void][Reflection.Assembly]::LoadFrom($library)
$scratch = Join-Path ([IO.Path]::GetTempPath()) ('G018-' + [Guid]::NewGuid().ToString('N'))
[void][IO.Directory]::CreateDirectory($scratch)
$assembly = Join-Path $scratch 'SclStyleAndTypeFolders.dll'
$compiler = "$env:ProgramFiles/Microsoft Visual Studio/18/Community/MSBuild/Current/Bin/Roslyn/csc.exe"
& $compiler /nologo /target:library /langversion:7.3 "/out:$assembly" "/reference:$library" "/reference:$apiDirectory/Siemens.Engineering.Base.dll" "/reference:$apiDirectory/Siemens.Engineering.Step7.dll" "/reference:$apiDirectory/Siemens.Engineering.WinCCUnified.dll" /reference:System.Xml.Linq.dll "$PSScriptRoot/SclStyleAndTypeFolders.cs" "$PSScriptRoot/DeviceInstanceComments.cs"
if ($LASTEXITCODE) { throw 'G-018 fixture compilation failed.' }
[void][Reflection.Assembly]::LoadFrom($assembly)
$previousDirectory = [IO.Directory]::GetCurrentDirectory()
try {
    [IO.Directory]::SetCurrentDirectory((Split-Path -Parent $library))
    [SclStyleAndTypeFoldersTests]::Run((Resolve-Path "$PSScriptRoot/..").Path)
}
finally { [IO.Directory]::SetCurrentDirectory($previousDirectory) }
