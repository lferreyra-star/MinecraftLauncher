$ids = @(
    'i_6b04661a83388191a7b8e34314dd5de9',
    'i_6b0466221fa48191be658806ce313dd7',
    'i_6b046627aeac8191a634527812c743e7',
    'i_6b04662af09c819196c3359125cabc3a'
)
$dir = 'C:\Users\EVIL\AppData\Local\Temp\opencode'
for ($i = 0; $i -lt $ids.Count; $i++) {
    $id = $ids[$i]
    $url = 'https://chatgpt.com/backend-api/share/post/' + $id + '/web-image/' + $id + '-attachment-0'
    $out = Join-Path $dir ('ref-' + ($i + 1) + '.png')
    Invoke-WebRequest -Uri $url -OutFile $out -UseBasicParsing
    Write-Output ($out + ' ' + (Get-Item $out).Length)
}
