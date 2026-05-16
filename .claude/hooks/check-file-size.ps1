# check-file-size.ps1 — PostToolUse (Edit|Write)
# Warns when a file exceeds Claude-friendly line limits.

param()

$json = [Console]::In.ReadToEnd()
if (-not $json) { exit 0 }

try {
    $data  = $json | ConvertFrom-Json
    $path  = $data.tool_input.file_path
} catch { exit 0 }

if (-not $path -or -not (Test-Path $path -PathType Leaf)) { exit 0 }

$ext   = [System.IO.Path]::GetExtension($path).ToLower()
$lines = (Get-Content $path -ErrorAction SilentlyContinue | Measure-Object -Line).Lines
if ($null -eq $lines) { exit 0 }

if ($ext -eq ".md") {
    $warn = 150; $hard = 250
} else {
    $warn = 300; $hard = 500
}

$rel = $path -replace [regex]::Escape((Get-Location).Path + "\"), ""

if ($lines -gt $hard) {
    Write-Output "[FILE-SIZE] HARD LIMIT: $rel has $lines lines (max $hard). Split this file now."
} elseif ($lines -gt $warn) {
    Write-Output "[FILE-SIZE] WARNING: $rel has $lines lines (warn at $warn). Consider splitting."
}

exit 0
