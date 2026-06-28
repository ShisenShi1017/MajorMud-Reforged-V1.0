# MajorMUD Reforged Editor Server Bridge v3.5
# This helper resolves the root Server.ps1 and shared Data folder from Editor/config.json.

function Get-EditorConfig {
    $configPath = Join-Path $PSScriptRoot 'config.json'
    if (!(Test-Path $configPath)) {
        return [pscustomobject]@{
            serverScriptPath = '..\..\Server.ps1'
            dataPath = '..\..\Data'
            rootPath = '..\..'
            serverHost = '127.0.0.1'
            serverPort = 2323
        }
    }
    return Get-Content $configPath -Raw | ConvertFrom-Json
}

function Resolve-EditorPath([string]$relativePath) {
    if ([System.IO.Path]::IsPathRooted($relativePath)) { return $relativePath }
    return [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot $relativePath))
}

function Get-MajorMUDRootPath {
    $cfg = Get-EditorConfig
    return Resolve-EditorPath $cfg.rootPath
}

function Get-MajorMUDServerScriptPath {
    $cfg = Get-EditorConfig
    return Resolve-EditorPath $cfg.serverScriptPath
}

function Get-MajorMUDDataPath {
    $cfg = Get-EditorConfig
    return Resolve-EditorPath $cfg.dataPath
}

function Test-MajorMUDServerPath {
    $server = Get-MajorMUDServerScriptPath
    return (Test-Path $server)
}

function Start-MajorMUDRootServer {
    $server = Get-MajorMUDServerScriptPath
    $root = Get-MajorMUDRootPath
    if (!(Test-Path $server)) {
        [System.Windows.Forms.MessageBox]::Show("Server.ps1 not found:`n$server", "MajorMUD Editor") | Out-Null
        return
    }
    Start-Process powershell -ArgumentList @('-NoProfile','-ExecutionPolicy','Bypass','-File', $server) -WorkingDirectory $root
}
