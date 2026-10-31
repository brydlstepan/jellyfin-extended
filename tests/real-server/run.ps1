# Checks the built plugin against a throwaway Jellyfin 12 server, using generated clips.
# Needs Docker and PowerShell 7. Build the plugin to artifacts/plugin first.
param(
    [string]$Image = 'jellyfin/jellyfin:12.4',
    [int]$Port = 18098
)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path "$PSScriptRoot/../.."
$work = Join-Path $root 'artifacts/real-server'
$container = 'jellyfin-extended-test'
$base = "http://127.0.0.1:$Port"
$user = 'tester'
$password = [guid]::NewGuid().ToString('N')
$script:failures = 0

function Pass-Fail($name, $ok, $detail) {
    if (-not $ok) { $script:failures++ }
    '{0} {1,-52} {2}' -f ($(if ($ok) { 'PASS' } else { 'FAIL' })), $name, $detail
}

# Adds a Dolby Vision profile 8.1 record to an HDR10 MP4 whose moov box follows its mdat,
# because ffmpeg can't create one without an existing Dolby Vision source.
function Add-DolbyVisionRecord($path) {
    $bytes = [IO.File]::ReadAllBytes($path)
    $record = [byte[]](0, 0, 0, 32) + [Text.Encoding]::ASCII.GetBytes('dvcC') + [byte[]](1, 0, 0x10, 0x35, 0x10) + (New-Object byte[] 19)
    $containers = 'moov', 'trak', 'mdia', 'minf', 'stbl'

    function Find-Box($start, $end, $type) {
        $at = $start
        while ($at + 8 -le $end) {
            $size = [Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($bytes, $at))
            if ([Text.Encoding]::ASCII.GetString($bytes, $at + 4, 4) -eq $type) { return @($at, $size) }
            $at += $size
        }
        throw "No $type box"
    }

    $parents = @()
    $start = 0; $end = $bytes.Length
    foreach ($type in $containers) {
        $box = Find-Box $start $end $type
        $parents += $box[0]
        $start = $box[0] + 8; $end = $box[0] + $box[1]
    }

    $stsd = Find-Box $start $end 'stsd'
    $entry = $stsd[0] + 16
    $entrySize = [Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($bytes, $entry))
    $insertAt = $entry + $entrySize
    foreach ($offset in ($parents + $stsd[0] + $entry)) {
        $size = [Net.IPAddress]::NetworkToHostOrder([BitConverter]::ToInt32($bytes, $offset)) + $record.Length
        [Array]::Copy([BitConverter]::GetBytes([Net.IPAddress]::HostToNetworkOrder([int]$size)), 0, $bytes, $offset, 4)
    }

    [IO.File]::WriteAllBytes($path, [byte[]]($bytes[0..($insertAt - 1)] + $record + $bytes[$insertAt..($bytes.Length - 1)]))
}

function Auth($device, $token) {
    $header = "MediaBrowser Client=`"real-server-test`", Device=`"$device`", DeviceId=`"$device`", Version=`"1.0`""
    if ($token) { $header += ", Token=`"$token`"" }
    @{ Authorization = $header }
}

function Call($method, $path, $headers, $body) {
    $request = @{ Method = $method; Uri = "$base$path"; Headers = $headers; ContentType = 'application/json'; TimeoutSec = 60 }
    if ($null -ne $body) { $request.Body = $body | ConvertTo-Json -Depth 12 }

    # While the server starts, it answers 503 with a holding page or drops the connection.
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

function Login($device) { (Call POST '/Users/AuthenticateByName' (Auth $device) @{ Username = $user; Pw = $password }).AccessToken }

# Like a browser: range rules for each codec it can play.
function Web-Profile {
    @{
        MaxStreamingBitrate = 120000000
        DirectPlayProfiles = @(@{ Container = 'mp4,m4v'; Type = 'Video'; VideoCodec = 'hevc,h264'; AudioCodec = 'aac' })
        TranscodingProfiles = @(@{ Container = 'ts'; Type = 'Video'; AudioCodec = 'aac'; VideoCodec = 'h264'; Context = 'Streaming'; Protocol = 'hls'; MaxAudioChannels = '2'; BreakOnNonKeyFrames = $true })
        CodecProfiles = @(
            @{ Type = 'Video'; Codec = 'hevc'; Conditions = @(@{ Condition = 'EqualsAny'; Property = 'VideoRangeType'; Value = 'SDR|HDR10|HLG|DOVIWithHDR10'; IsRequired = $false }) },
            @{ Type = 'Video'; Codec = 'h264'; Conditions = @(@{ Condition = 'EqualsAny'; Property = 'VideoRangeType'; Value = 'SDR'; IsRequired = $false }) }
        )
        SubtitleProfiles = @()
    }
}

# Like the Android app: no range rules at all.
function App-Profile {
    $profile = Web-Profile
    $profile.CodecProfiles = @()
    $profile
}

function Playback($device, $item, $deviceProfile) {
    $body = @{ UserId = $script:userId; IsPlayback = $true; MaxStreamingBitrate = 120000000 }
    if ($deviceProfile) { $body.DeviceProfile = $deviceProfile }
    (Call POST "/Items/$item/PlaybackInfo" (Auth $device (Login $device)) $body).MediaSources[0]
}

function Is-Transcoded($source) { [bool]$source.TranscodingUrl -and -not $source.SupportsDirectPlay }

function Expect-Playback($name, $device, $item, $deviceProfile, $transcoded) {
    $source = Playback $device $item $deviceProfile
    Pass-Fail $name ((Is-Transcoded $source) -eq $transcoded) "directPlay=$($source.SupportsDirectPlay) transcode=$([bool]$source.TranscodingUrl)"
}

function Set-Mode($device, $mode) { Call POST "/Extended/Hdr/Device?mode=$mode" (Auth $device (Login $device)) $null | Out-Null }
function Get-Mode($device) { (Call GET '/Extended/Hdr/Device' (Auth $device (Login $device)) $null).Mode }

function Get-StatusCode($method, $path, $headers, $body) {
    $request = @{ Method = $method; Uri = "$base$path"; SkipHttpErrorCheck = $true; ContentType = 'application/json' }
    if ($headers) { $request.Headers = $headers }
    if ($body) { $request.Body = $body }
    [int](Invoke-WebRequest @request).StatusCode
}

if (-not (Test-Path (Join-Path $root 'artifacts/plugin/meta.json'))) { throw 'Build the plugin to artifacts/plugin first' }

docker stop $container 2>$null | Out-Null
if (Test-Path $work) { Remove-Item $work -Recurse -Force }
$version = (Get-Content (Join-Path $root 'artifacts/plugin/meta.json') -Raw | ConvertFrom-Json).version
$pluginDir = Join-Path $work "config/plugins/JellyfinExtended_$version"
$media = Join-Path $work 'media'
New-Item -ItemType Directory -Force $pluginDir, $media, (Join-Path $work 'cache') | Out-Null
Copy-Item (Join-Path $root 'artifacts/plugin/Jellyfin.Plugin.Extended.dll'), (Join-Path $root 'artifacts/plugin/meta.json') $pluginDir

docker run -d --rm --name $container -p "127.0.0.1:${Port}:8096" -v "$work/config:/config" -v "$work/cache:/cache" -v "${media}:/media" $Image | Out-Null
try {
    # Generate the clips with the server's own ffmpeg.
    $ffmpeg = 'docker', 'exec', $container, '/usr/lib/jellyfin-ffmpeg/ffmpeg', '-v', 'error', '-y', '-f', 'lavfi', '-i', 'testsrc2=size=640x360:rate=24:duration=2', '-f', 'lavfi', '-i', 'anullsrc=r=48000:cl=stereo', '-shortest', '-c:a', 'aac'
    & $ffmpeg[0] $ffmpeg[1..($ffmpeg.Length - 1)] -pix_fmt yuv420p -c:v libx264 /media/ClipSdr.mp4
    $hdr = '-pix_fmt', 'yuv420p10le', '-c:v', 'libx265', '-tag:v', 'hvc1', '-x265-params', 'log-level=error:hdr10=1:colorprim=bt2020:transfer=smpte2084:colormatrix=bt2020nc:master-display=G(13250,34500)B(7500,3000)R(34000,16000)WP(15635,16450)L(10000000,1):max-cll=1000,400', '-color_primaries', 'bt2020', '-color_trc', 'smpte2084', '-colorspace', 'bt2020nc'
    & $ffmpeg[0] $ffmpeg[1..($ffmpeg.Length - 1)] @hdr /media/ClipHdr.mp4
    & $ffmpeg[0] $ffmpeg[1..($ffmpeg.Length - 1)] @hdr /media/ClipDolbyVision.mp4
    Add-DolbyVisionRecord (Join-Path $media 'ClipDolbyVision.mp4')

    Wait-Server
    Call POST '/Startup/Configuration' (Auth 'setup') @{ UICulture = 'en-US'; MetadataCountryCode = 'US'; PreferredMetadataLanguage = 'en' } | Out-Null
    Call GET '/Startup/User' (Auth 'setup') $null | Out-Null
    Call POST '/Startup/User' (Auth 'setup') @{ Name = $user; Password = $password } | Out-Null
    Call POST '/Startup/Complete' (Auth 'setup') $null | Out-Null

    $admin = Auth 'admin' (Login 'admin')
    Invoke-RestMethod -Method POST -Uri "$base/Library/VirtualFolders?name=Clips&collectionType=movies&refreshLibrary=true" -Headers $admin -ContentType 'application/json' -Body '{"LibraryOptions":{"PathInfos":[{"Path":"/media"}]}}' | Out-Null
    # A scan already running at startup can miss the new library, so keep asking for one.
    foreach ($attempt in 1..40) {
        if ($attempt % 5 -eq 1) { Call POST '/Library/Refresh' $admin $null | Out-Null }
        Start-Sleep 3
        $items = @((Call GET '/Items?recursive=true&includeItemTypes=Movie&fields=MediaStreams' $admin $null).Items)
        if ($items.Count -ge 3) { break }
    }

    if ($items.Count -lt 3) { throw "The library scan found $($items.Count) of 3 clips" }

    $range = { param($name) ($items | Where-Object Name -eq $name).MediaStreams | Where-Object Type -eq 'Video' | Select-Object -First 1 -ExpandProperty VideoRangeType }
    $sdr = ($items | Where-Object Name -eq 'ClipSdr').Id
    $hdr10 = ($items | Where-Object Name -eq 'ClipHdr').Id
    $dovi = ($items | Where-Object Name -eq 'ClipDolbyVision').Id
    $script:userId = (Call GET '/Users/Me' $admin $null).Id

    '--- Clips'
    Pass-Fail 'SDR clip is SDR' ((& $range 'ClipSdr') -eq 'SDR') (& $range 'ClipSdr')
    Pass-Fail 'HDR clip is HDR10' ((& $range 'ClipHdr') -eq 'HDR10') (& $range 'ClipHdr')
    Pass-Fail 'Dolby Vision clip is DOVIWithHDR10' ((& $range 'ClipDolbyVision') -eq 'DOVIWithHDR10') (& $range 'ClipDolbyVision')

    '--- Before any setting: direct play'
    Expect-Playback 'Auto, web profile, HDR10' 'other' $hdr10 (Web-Profile) $false
    Expect-Playback 'Auto, web profile, Dolby Vision' 'other' $dovi (Web-Profile) $false
    Expect-Playback 'Auto, app profile, HDR10' 'other' $hdr10 (App-Profile) $false
    Expect-Playback 'Auto, app profile, Dolby Vision' 'other' $dovi (App-Profile) $false
    Pass-Fail 'New devices use Auto' ((Get-Mode 'off') -eq 'Auto') (Get-Mode 'off')

    '--- HDR off'
    Set-Mode 'off' 'Off'
    Pass-Fail 'The setting is saved' ((Get-Mode 'off') -eq 'Off') (Get-Mode 'off')
    Expect-Playback 'HDR off, web profile, HDR10' 'off' $hdr10 (Web-Profile) $true
    Expect-Playback 'HDR off, web profile, Dolby Vision' 'off' $dovi (Web-Profile) $true
    Expect-Playback 'HDR off, app profile, HDR10' 'off' $hdr10 (App-Profile) $true
    Expect-Playback 'HDR off, app profile, Dolby Vision' 'off' $dovi (App-Profile) $true
    Expect-Playback 'HDR off, web profile, SDR film not affected' 'off' $sdr (Web-Profile) $false
    Expect-Playback 'Another device not affected' 'other' $hdr10 (Web-Profile) $false

    '--- No device profile'
    $withSetting = Playback 'off' $hdr10 $null
    $without = Playback 'other' $hdr10 $null
    Pass-Fail 'HDR off, no profile: same as an unset device' (($withSetting.SupportsDirectPlay -eq $without.SupportsDirectPlay) -and ([bool]$withSetting.TranscodingUrl -eq [bool]$without.TranscodingUrl)) "directPlay=$($withSetting.SupportsDirectPlay)"

    '--- Malformed request'
    $path = "/Items/$hdr10/PlaybackInfo"
    $offStatus = Get-StatusCode POST $path (Auth 'off' (Login 'off')) '{not json'
    $otherStatus = Get-StatusCode POST $path (Auth 'other' (Login 'other')) '{not json'
    Pass-Fail 'Same response as an unset device' ($offStatus -eq $otherStatus) "off=$offStatus other=$otherStatus"
    $logs = (docker logs $container 2>&1 | Out-String)
    Pass-Fail 'A warning is logged' ($logs -match 'HDR override skipped') ''

    '--- Sign-in'
    Pass-Fail 'Reading a setting signed out is refused' ((Get-StatusCode GET '/Extended/Hdr/Device') -eq 401) (Get-StatusCode GET '/Extended/Hdr/Device')
    Pass-Fail 'Changing a setting signed out is refused' ((Get-StatusCode POST '/Extended/Hdr/Device?mode=Off') -eq 401) (Get-StatusCode POST '/Extended/Hdr/Device?mode=Off')

    '--- HDR on'
    Set-Mode 'on' 'On'
    Pass-Fail 'The setting is saved' ((Get-Mode 'on') -eq 'On') (Get-Mode 'on')
    Expect-Playback 'HDR on, web profile, HDR10' 'on' $hdr10 (Web-Profile) $false

    '--- Restart'
    docker restart $container | Out-Null
    Wait-Server
    Pass-Fail 'HDR off survives a restart' ((Get-Mode 'off') -eq 'Off') (Get-Mode 'off')
    Expect-Playback 'HDR off still transcodes after a restart' 'off' $hdr10 (Web-Profile) $true

    '--- Back to Auto'
    Set-Mode 'off' 'Auto'
    Pass-Fail 'The device is back on Auto' ((Get-Mode 'off') -eq 'Auto') (Get-Mode 'off')
    Expect-Playback 'Back on Auto: direct play' 'off' $hdr10 (Web-Profile) $false
    $saved = Get-Content (Join-Path $work 'config/plugins/configurations/Jellyfin.Plugin.Extended.xml') -Raw
    Pass-Fail 'The saved entry is cleared' ($saved -notmatch '<DeviceId>off</DeviceId>') ''
}
finally {
    docker stop $container 2>$null | Out-Null
}

"FAILURES: $script:failures"
if ($script:failures) { exit 1 }
