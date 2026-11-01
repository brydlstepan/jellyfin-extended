# Installs the built plugin through a plugin repository on a throwaway Jellyfin 12 server.
# Needs Docker and PowerShell 7. Build the plugin to artifacts/plugin first.
param(
    [string]$Image = 'jellyfin/jellyfin:12.4',
    [int]$Port = 18099
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path "$PSScriptRoot/../.."
$work = Join-Path $root 'artifacts/repository-test'
$files = Join-Path $work 'files'
$manifest = Join-Path $files 'manifest.json'
$built = Join-Path $root 'artifacts/plugin'
$release = Join-Path $root '.github/scripts/release.ps1'
$network = 'jellyfin-extended-repo-test'
$server = 'jellyfin-extended-repo-server'
$host_ = 'jellyfin-extended-repo-files'
$base = "http://127.0.0.1:$Port"
$user = 'tester'
$password = [guid]::NewGuid().ToString('N')
$script:failures = 0

function Pass-Fail($name, $ok, $detail) {
    if (-not $ok) { $script:failures++ }
    '{0} {1,-52} {2}' -f ($(if ($ok) { 'PASS' } else { 'FAIL' })), $name, $detail
}

function Auth($token) {
    $header = 'MediaBrowser Client="repository-test", Device="admin", DeviceId="admin", Version="1.0"'
    if ($token) { $header += ", Token=`"$token`"" }
    @{ Authorization = $header }
}

function Call($method, $path, $headers, $json) {
    $request = @{ Method = $method; Uri = "$base$path"; Headers = $headers; ContentType = 'application/json'; TimeoutSec = 60 }
    if ($json) { $request.Body = $json }
    foreach ($attempt in 1..30) {
        try { return Invoke-RestMethod @request }
        catch {
            $response = $_.Exception.Response
            if (($response -and [int]$response.StatusCode -ne 503) -or $attempt -eq 30) { throw }
            Start-Sleep 2
        }
    }
}

function Wait-Server {
    foreach ($attempt in 1..60) {
        try { if ((Invoke-RestMethod "$base/System/Info/Public" -TimeoutSec 3).Version) { return } } catch { }
        Start-Sleep 2
    }

    throw 'The server did not start'
}

function Release($version, $from) {
    & $release -Version $version -PluginDir $from -ManifestPath $manifest -BaseUrl "http://$host_" -OutDir $files -Changelog "Test $version"
}

function Installed-Dirs { @(Get-ChildItem (Join-Path $work 'config/plugins') -Directory -ErrorAction SilentlyContinue | Where-Object Name -Like '*Extended*') }

function Stop-All {
    docker stop $server $host_ 2>$null | Out-Null
    docker network rm $network 2>$null | Out-Null
}

if (-not (Test-Path (Join-Path $built 'meta.json'))) { throw 'Build the plugin to artifacts/plugin first' }

Stop-All
if (Test-Path $work) { Remove-Item $work -Recurse -Force }
New-Item -ItemType Directory -Force $files, (Join-Path $work 'config'), (Join-Path $work 'cache') | Out-Null
$version = (Get-Content (Join-Path $built 'meta.json') -Raw | ConvertFrom-Json).version
$older = '1.0.0.0'

'--- Manifest'
$tagError = $null
try { Release '9.9.9.9' $built } catch { $tagError = $_.Exception.Message }
Pass-Fail 'A tag that differs from meta.json is refused' ($tagError -match 'meta.json') $tagError

# An earlier version to keep. Its checksum is wrong on purpose, for the install check below.
$olderBuild = Join-Path $work 'older'
Copy-Item $built $olderBuild -Recurse
$meta = Get-Content (Join-Path $olderBuild 'meta.json') -Raw | ConvertFrom-Json
$meta.version = $older
$meta | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $olderBuild 'meta.json')
Release $older $olderBuild
$single = Get-Content $manifest -Raw
Pass-Fail 'The first manifest is a list with a list of versions' ($single -match '(?s)^\s*\[.*"versions":\s*\[') ''

(Get-Content $manifest -Raw).Replace(((Get-Content $manifest -Raw | ConvertFrom-Json -NoEnumerate)[0].versions[0].checksum), '00000000000000000000000000000000') | Set-Content $manifest -Encoding utf8NoBOM
Release $version $built
Release $version $built

$plugin = (Get-Content $manifest -Raw | ConvertFrom-Json -NoEnumerate)[0]
$listed = @($plugin.versions)
Pass-Fail 'Earlier versions are kept, and a rerun adds no copy' (($listed.version -join ',') -eq "$version,$older") ($listed.version -join ',')
Pass-Fail 'The older entry is unchanged' ($listed[1].checksum -eq '00000000000000000000000000000000') $listed[1].checksum
$expected = (Get-FileHash (Join-Path $files "jellyfin-extended_$version.zip") -Algorithm MD5).Hash
Pass-Fail 'The checksum matches the zip' ($listed[0].checksum -eq $expected) $listed[0].checksum
Pass-Fail 'The target is Jellyfin 12' ($listed[0].targetAbi -eq '12.0.0.0') $listed[0].targetAbi
Pass-Fail 'The plugin ID matches' ($plugin.guid -eq '6f0c2b7e-4d1a-4c86-9a53-2e7b8d1f3c40') $plugin.guid
Expand-Archive (Join-Path $files "jellyfin-extended_$version.zip") (Join-Path $work 'unzipped')
$contents = (Get-ChildItem (Join-Path $work 'unzipped') -Name | Sort-Object) -join ','
Pass-Fail 'The zip has the plugin and its metadata' ($contents -eq 'Jellyfin.Plugin.Extended.dll,meta.json') $contents

docker network create $network | Out-Null
try {
    docker run -d --rm --name $host_ --network $network -v "${files}:/usr/share/nginx/html:ro" nginx:alpine | Out-Null
    docker run -d --rm --name $server --network $network -p "127.0.0.1:${Port}:8096" -v "$work/config:/config" -v "$work/cache:/cache" $Image | Out-Null

    Wait-Server
    Call POST '/Startup/Configuration' (Auth) '{"UICulture":"en-US","MetadataCountryCode":"US","PreferredMetadataLanguage":"en"}' | Out-Null
    Call GET '/Startup/User' (Auth) | Out-Null
    Call POST '/Startup/User' (Auth) (@{ Name = $user; Password = $password } | ConvertTo-Json) | Out-Null
    Call POST '/Startup/Complete' (Auth) | Out-Null
    $token = (Call POST '/Users/AuthenticateByName' (Auth) (@{ Username = $user; Pw = $password } | ConvertTo-Json)).AccessToken
    $admin = Auth $token

    '--- Catalog'
    Call POST '/Repositories' $admin "[{`"Name`":`"Jellyfin Extended`",`"Url`":`"http://$host_/manifest.json`",`"Enabled`":true}]" | Out-Null
    $package = @(Call GET '/Packages' $admin | ForEach-Object { $_ } | Where-Object { ($_.guid -replace '-') -eq ($plugin.guid -replace '-') })
    Pass-Fail 'The catalog shows the plugin' ($package.Count -eq 1) "found=$($package.Count)"
    Pass-Fail 'The catalog shows both versions' ((@($package[0].versions.version) -join ',') -eq "$version,$older") (@($package[0].versions.version) -join ',')

    $name = [Uri]::EscapeDataString($plugin.name)
    '--- Install'
    $refused = (Invoke-WebRequest -Method POST -Uri "$base/Packages/Installed/${name}?assemblyGuid=$($plugin.guid)&version=$older" -Headers $admin -SkipHttpErrorCheck).StatusCode
    Pass-Fail 'A wrong checksum is refused' ($refused -ge 400) "status=$refused"
    Pass-Fail 'A wrong checksum is not installed' ((Installed-Dirs).Count -eq 0) (((Installed-Dirs).Name) -join ',')

    Call POST "/Packages/Installed/${name}?assemblyGuid=$($plugin.guid)&version=$version" $admin | Out-Null
    Pass-Fail 'The plugin is downloaded and unpacked' ((Installed-Dirs).Count -eq 1) (((Installed-Dirs).Name) -join ',')

    docker restart $server | Out-Null
    Wait-Server
    $token = (Call POST '/Users/AuthenticateByName' (Auth) (@{ Username = $user; Pw = $password } | ConvertTo-Json)).AccessToken
    $admin = Auth $token
    $loaded = @(Call GET '/Plugins' $admin | ForEach-Object { $_ } | Where-Object Id -eq ($plugin.guid -replace '-'))
    Pass-Fail 'The plugin is Active after a restart' ($loaded.Count -eq 1 -and $loaded[0].Status -eq 'Active') "status=$($loaded[0].Status) version=$($loaded[0].Version)"
    Pass-Fail 'It is the version from the manifest' ($loaded[0].Version -eq $version) $loaded[0].Version
    $devices = @(Call GET '/Extended/Hdr/Devices' $admin | ForEach-Object { $_ })
    Pass-Fail 'The installed plugin answers' ($devices.Count -ge 1) "devices=$($devices.Count)"
}
finally {
    if ($script:failures -gt 0) { docker logs --tail 40 $server 2>&1 | Out-String }
    Stop-All
}

"FAILURES: $script:failures"
exit [int]($script:failures -gt 0)
