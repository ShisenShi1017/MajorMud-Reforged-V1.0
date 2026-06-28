$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $Root
$HostName = "127.0.0.1"
$Port = 2323
function Read-UntilPrompt($stream) {
  $buffer = New-Object byte[] 1
  $sb = New-Object System.Text.StringBuilder
  $stream.ReadTimeout = 30000
  while ($true) {
    try {
      $n = $stream.Read($buffer, 0, 1)
      if ($n -le 0) { break }
      $ch = [Text.Encoding]::ASCII.GetString($buffer, 0, $n)
      [void]$sb.Append($ch)
      Write-Host -NoNewline $ch
      if ($sb.ToString().EndsWith("> ")) { break }
    } catch [System.IO.IOException] {
      Write-Host ""
      Write-Host "Connection timed out waiting for server response."
      break
    }
  }
}
Write-Host "Connecting to $($HostName):$Port ..."
$client = New-Object System.Net.Sockets.TcpClient
$client.Connect($HostName, $Port)
Write-Host "Connected. Waiting for game prompt..."
$stream = $client.GetStream()
$writer = New-Object System.IO.StreamWriter($stream, [Text.Encoding]::ASCII)
$writer.NewLine = "`r`n"
$writer.AutoFlush = $true
try {
  while ($client.Connected) {
    Read-UntilPrompt $stream
    $line = Read-Host
    $writer.WriteLine($line)
    $writer.Flush()
    if ($line.Trim().ToLowerInvariant() -eq "quit") { Start-Sleep -Milliseconds 300; break }
  }
} finally { $client.Close(); Write-Host "Disconnected." }
