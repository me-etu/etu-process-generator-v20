param(
    [string]$LibraryPath = "$PSScriptRoot/../etu-process-generator-TiAv21/bin/x64/Debug/TIA_LIB.dll",
    [string]$ApiDirectory = "$env:ProgramFiles/Siemens/Automation/Portal V21/PublicAPI/V21/net48"
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Xml.Linq
$library = (Resolve-Path -LiteralPath $LibraryPath).Path
$references = @('System.Core', 'System.Xml', 'System.Xml.Linq', $library)
foreach ($module in 'Base', 'Step7', 'WinCCUnified', 'WinCC', 'Sivarc') {
    $api = Join-Path $ApiDirectory "Siemens.Engineering.$module.dll"
    [void][Reflection.Assembly]::LoadFrom($api)
    $references += $api
}
[void][Reflection.Assembly]::LoadFrom($library)
Add-Type -Path "$PSScriptRoot/DeviceInstanceComments.cs" -ReferencedAssemblies $references
$previousDirectory = [IO.Directory]::GetCurrentDirectory()
try {
    [IO.Directory]::SetCurrentDirectory((Split-Path -Parent $library))
    [DeviceInstanceComments]::Run((Resolve-Path "$PSScriptRoot/..").Path, (Join-Path $ApiDirectory '../Schemas'))
}
finally { [IO.Directory]::SetCurrentDirectory($previousDirectory) }
