try {
    $raw = [Console]::In.ReadToEnd()
    $data = $raw | ConvertFrom-Json
    $file = $data.tool_input.file_path
    if ($file -match '\.cs$') {
        dotnet format 'api/src/Neuracode.Crm.Api' --verbosity quiet 2>$null
    }
} catch {}
exit 0
