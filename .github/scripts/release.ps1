# Packages a build as a plugin zip and adds it to the plugin repository manifest.
param(
    [Parameter(Mandatory)][string]$Version,
    [Parameter(Mandatory)][string]$PluginDir,
    [Parameter(Mandatory)][string]$ManifestPath,
    [Parameter(Mandatory)][string]$BaseUrl,
    [Parameter(Mandatory)][string]$OutDir,
    [string]$Changelog = ''
)

$ErrorActionPreference = 'Stop'

# Tags are short, like 1.1, but Jellyfin and the assembly use four parts.
$Version = (($Version.Split('.') + @('0', '0', '0'))[0..3]) -join '.'

$metaPath = Join-Path $PluginDir 'meta.json'
$meta = Get-Content $metaPath -Raw | ConvertFrom-Json
if ($meta.version -ne $Version) { throw "The tag says $Version, but meta.json says $($meta.version)" }

New-Item -ItemType Directory -Force $OutDir | Out-Null
$zipName = "jellyfin-extended_$Version.zip"
$zip = Join-Path $OutDir $zipName
Compress-Archive -Path (Join-Path $PluginDir 'Jellyfin.Plugin.Extended.dll'), $metaPath -DestinationPath $zip -Force

# Jellyfin checks a download against this MD5 before it installs it.
$checksum = (Get-FileHash $zip -Algorithm MD5).Hash.ToLowerInvariant()

$earlier = @()
if (Test-Path $ManifestPath) {
    $existing = Get-Content $ManifestPath -Raw | ConvertFrom-Json -NoEnumerate
    $earlier = @($existing[0].versions | Where-Object version -ne $Version)
}

$entry = [pscustomobject][ordered]@{
    version = $Version
    changelog = $Changelog
    targetAbi = $meta.targetAbi
    sourceUrl = "$($BaseUrl.TrimEnd('/'))/$zipName"
    checksum = $checksum
    timestamp = [DateTime]::UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")
}

$plugin = [ordered]@{
    guid = $meta.guid
    name = $meta.name
    description = $meta.description
    overview = $meta.overview
    owner = $meta.owner
    category = $meta.category
    versions = @(@($entry) + $earlier | Sort-Object { [version]$_.version } -Descending)
}

ConvertTo-Json -InputObject @($plugin) -Depth 5 | Set-Content $ManifestPath -Encoding utf8NoBOM
