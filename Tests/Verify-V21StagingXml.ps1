param(
    [string]$LibraryPath = "$PSScriptRoot/../etu-process-generator-TiAv21/bin/x64/Debug/TIA_LIB.dll",
    [string]$SchemaDirectory = "$env:ProgramFiles/Siemens/Automation/Portal V21/PublicAPI/V21/Schemas"
)

# XML-only regression checks. Never construct PlcProject, SiemensPortal, or XmlDatablock,
# and never call Upload: those operations attach to or modify a TIA project.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Xml.Linq
[void][Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $LibraryPath).Path)
$interfaceNamespace = 'http://www.siemens.com/automation/Openness/SW/Interface/v5'
$settings = New-Object Xml.XmlReaderSettings
$settings.ValidationType = [Xml.ValidationType]::Schema
$settings.XmlResolver = New-Object Xml.XmlUrlResolver
[void]$settings.Schemas.Add($interfaceNamespace, (Join-Path $SchemaDirectory 'SW.InterfaceSections_v5.xsd'))
$schemaErrors = New-Object 'System.Collections.Generic.List[string]'
$settings.add_ValidationEventHandler({ param($sender, $eventArgs) $schemaErrors.Add($eventArgs.Message) })

function Assert-InterfaceSchema($sections, [string]$label) {
    $schemaErrors.Clear()
    $textReader = New-Object IO.StringReader($sections.ToString())
    $reader = [Xml.XmlReader]::Create($textReader, $settings)
    try { while ($reader.Read()) {} }
    finally { $reader.Dispose(); $textReader.Dispose() }
    if ($schemaErrors.Count) { throw "${label}: $($schemaErrors -join '; ')" }
    Write-Output "PASS: $label matches Siemens interface XSD."
}

foreach ($direction in 'IN', 'OUT') {
    $members = New-Object 'System.Collections.Generic.List[TIA_LIB.Xml.XmlDataMember]'
    $members.Add((New-Object TIA_LIB.Xml.XmlDataMember -ArgumentList 'Enabled', 'Bool', 'Enable signal'))
    $members.Add((New-Object TIA_LIB.Xml.XmlDataMember -ArgumentList 'Value', 'Int', 'Analog signal'))
    $udt = New-Object TIA_LIB.Xml.XmlUdt -ArgumentList "hw${direction}_OfflineCheck", $members
    [xml]$document = $udt.Xml.ToString()
    $attributes = $document.Document.'SW.Types.PlcStruct'.AttributeList
    if ($document.Document.Engineering.version -ne 'V21') { throw 'Wrong UDT engineering version.' }
    $identifier = $attributes.SelectSingleNode('Namespace')
    if ($null -eq $identifier -or $identifier.InnerText -ne '') { throw 'Missing/incorrect global Namespace identifier.' }
    $sections = $udt.Xml.Descendants([Xml.Linq.XName]::Get('Sections', $interfaceNamespace)) | Select-Object -First 1
    Assert-InterfaceSchema $sections "hw$direction UDT members and comments"
}
Write-Output 'PASS: both generated UDTs include the empty global Namespace identifier.'

# Exercise the serializer used for nested dbIO.<Unit>.IN/OUT members without constructing a DB.
$unit = New-Object TIA_LIB.Xml.XmlDataMember -ArgumentList 'OfflineCheck', 'Struct', 'Unit IO'
$unit.Children.Add((New-Object TIA_LIB.Xml.XmlDataMember -ArgumentList 'IN', 'hwIN_OfflineCheck'))
$unit.Children.Add((New-Object TIA_LIB.Xml.XmlDataMember -ArgumentList 'OUT', 'hwOUT_OfflineCheck'))
$dbSections = [Xml.Linq.XElement]::Parse("<Sections xmlns='$interfaceNamespace'><Section Name='Static'/></Sections>")
$dbSections.Elements() | Select-Object -First 1 | ForEach-Object { $_.Add($unit.ToXElement()) }
Assert-InterfaceSchema $dbSections 'Nested dbIO struct members and comments'

# Bypass the side-effecting XmlBlock constructor and initialize only the XML fields this method needs.
# Load public API type metadata needed for XmlBlock's field types; do not instantiate any Siemens type.
foreach ($module in 'Base', 'Step7') {
    $apiPath = Join-Path $SchemaDirectory "../net48/Siemens.Engineering.$module.dll"
    [void][Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $apiPath).Path)
}

# Exercise the exact DB fallback loader used by the constructor, using deployed templates.
$outputDirectory = Split-Path -Parent (Resolve-Path -LiteralPath $LibraryPath).Path
$previousDirectory = [IO.Directory]::GetCurrentDirectory()
try {
    [IO.Directory]::SetCurrentDirectory($outputDirectory)
    $loader = [TIA_LIB.Xml.XmlDatablock].GetMethod('LoadEmptyTemplate', [Reflection.BindingFlags]'Static,NonPublic')
    $dbXml = $loader.Invoke($null, @())
}
finally { [IO.Directory]::SetCurrentDirectory($previousDirectory) }
[xml]$dbDocument = $dbXml.ToString()
if ($dbDocument.Document.Engineering.version -ne 'V21') { throw 'DB fallback still uses an unsupported engineering version.' }
$dbIdentifier = $dbDocument.Document.'SW.Blocks.GlobalDB'.AttributeList.SelectSingleNode('Namespace')
if ($null -eq $dbIdentifier -or $dbIdentifier.InnerText -ne '') { throw 'DB fallback lacks its global Namespace identifier.' }
$db = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([TIA_LIB.Xml.XmlDatablock])
$db.Xml = $dbXml
$db.SectionList = $dbXml.Descendants([Xml.Linq.XName]::Get('Section', $interfaceNamespace)) | Select-Object -First 1
[TIA_LIB.Xml.XmlDatablock].GetField('_Name', [Reflection.BindingFlags]'Instance,NonPublic').SetValue($db, 'dbIO')
$db.Name = 'dbIO'
$db.SetMember($unit)
$db.SetMember($unit)
$actualDbSections = $dbXml.Descendants([Xml.Linq.XName]::Get('Sections', $interfaceNamespace)) | Select-Object -First 1
Assert-InterfaceSchema $actualDbSections 'Complete generated dbIO fallback interface'
if (@($db.SectionList.Elements()).Count -ne 1 -or $db.Name -ne 'dbIO') { throw 'DB unit member replacement/name is incorrect.' }
Write-Output 'PASS: DB fallback has V21/global Namespace and preserves one staged unit after replacement.'

$block = [Runtime.Serialization.FormatterServices]::GetUninitializedObject([TIA_LIB.Xml.XmlBlock])
$block.Xml = [Xml.Linq.XElement]::Load("$PSScriptRoot/../TIAOpenness/TIA_Lib/TIA_LIB/Xml/EmptyUnit.xml")
[TIA_LIB.Xml.XmlBlock].GetField('_Namespace', [Reflection.BindingFlags]'Instance,NonPublic').SetValue(
    $block, [Xml.Linq.XNamespace]::Get($interfaceNamespace))
$block.GetInterfaceMember('Input', 'hwIN', 'hwIN_OfflineCheck')
$block.GetInterfaceMember('Output', 'hwOUT', 'hwOUT_OfflineCheck')
$block.GetInterfaceMember('Input', 'hwIN', 'hwIN_OfflineCheck')
$block.GetInterfaceMember('Output', 'hwOUT', 'hwOUT_OfflineCheck')
$unitSections = $block.Xml.Descendants([Xml.Linq.XName]::Get('Sections', $interfaceNamespace)) | Select-Object -First 1
Assert-InterfaceSchema $unitSections 'Unit staging interfaces'
$stagingMembers = @($unitSections.Elements().Elements() | Where-Object { $_.Attribute('Name').Value -in 'hwIN', 'hwOUT' })
if ($stagingMembers.Count -ne 2) { throw 'Staging interface creation is not idempotent.' }
Write-Output 'PASS: repeated staging interface creation preserves one hwIN and one hwOUT. No Portal connection.'
