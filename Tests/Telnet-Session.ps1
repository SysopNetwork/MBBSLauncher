<#
Minimal scripted telnet client for driving the Worldgroup BBS console.
Usage: dot-source, then use Connect-Bbs / Send-Bbs / Read-Bbs / Close-Bbs.
Handles basic IAC negotiation (refuses most options) and strips ANSI for logging.
#>

$script:bbsClient = $null
$script:bbsStream = $null

function Connect-Bbs {
    param([string]$BbsHost="127.0.0.1", [int]$Port=23)
    $script:bbsClient = New-Object System.Net.Sockets.TcpClient
    $script:bbsClient.Connect($BbsHost, $Port)
    $script:bbsStream = $script:bbsClient.GetStream()
    Start-Sleep -Milliseconds 500
}

function Close-Bbs {
    if ($script:bbsStream) { $script:bbsStream.Close() }
    if ($script:bbsClient) { $script:bbsClient.Close() }
    $script:bbsStream = $null; $script:bbsClient = $null
}

# Read whatever is available within a timeout window; answer telnet IAC negotiation.
function Read-Bbs {
    param([int]$TimeoutMs=3000, [switch]$Raw)
    $buf = New-Object System.Collections.Generic.List[byte]
    $deadline = [Environment]::TickCount + $TimeoutMs
    $tmp = New-Object byte[] 4096
    while ([Environment]::TickCount -lt $deadline) {
        if ($script:bbsStream.DataAvailable) {
            $n = $script:bbsStream.Read($tmp, 0, $tmp.Length)
            for ($i=0; $i -lt $n; $i++) { $buf.Add($tmp[$i]) }
            $deadline = [Environment]::TickCount + 800   # extend while data flows
        } else {
            Start-Sleep -Milliseconds 100
        }
    }
    # Process IAC (255) negotiation: for DO(253)/WILL(251) reply refuse; strip commands
    $out = New-Object System.Collections.Generic.List[byte]
    $reply = New-Object System.Collections.Generic.List[byte]
    for ($i=0; $i -lt $buf.Count; $i++) {
        if ($buf[$i] -eq 255 -and $i+2 -lt $buf.Count) {
            $cmd = $buf[$i+1]; $opt = $buf[$i+2]
            switch ($cmd) {
                253 { $reply.AddRange([byte[]]@(255,252,$opt)) }  # DO   -> WONT
                251 { $reply.AddRange([byte[]]@(255,254,$opt)) }  # WILL -> DONT
                254 { }                                           # DONT -> ignore
                252 { }                                           # WONT -> ignore
            }
            $i += 2
        } else { $out.Add($buf[$i]) }
    }
    if ($reply.Count -gt 0) { $script:bbsStream.Write($reply.ToArray(), 0, $reply.Count); $script:bbsStream.Flush() }
    $text = [System.Text.Encoding]::ASCII.GetString($out.ToArray())
    if (-not $Raw) {
        # strip ANSI CSI sequences and other escapes for readable logging
        $text = [regex]::Replace($text, "\x1b\[[0-9;?]*[ -/]*[@-~]", "")
        $text = [regex]::Replace($text, "\x1b[@-Z\\-_]", "")
        $text = $text -replace "[\x00-\x08\x0b\x0c\x0e-\x1f]", ""
    }
    return $text
}

function Send-Bbs {
    param([string]$Text, [switch]$NoCr)
    $s = if ($NoCr) { $Text } else { $Text + "`r" }
    $bytes = [System.Text.Encoding]::ASCII.GetBytes($s)
    $script:bbsStream.Write($bytes, 0, $bytes.Length); $script:bbsStream.Flush()
    Start-Sleep -Milliseconds 300
}
