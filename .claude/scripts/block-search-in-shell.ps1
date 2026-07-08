# PreToolUse hook: redirect shell-based file search/read to dedicated tools.
# Reads the hook JSON from stdin, inspects tool_input.command, and emits a
# PreToolUse "deny" decision when the command performs a file search/read that
# should instead use the Grep / Read / Glob tools. Command-position detection
# means pipeline filtering (e.g. "git log | Select-String x") is left allowed.

$raw = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($raw)) { exit 0 }

try { $data = $raw | ConvertFrom-Json } catch { exit 0 }

$cmd = [string]$data.tool_input.command
if ([string]::IsNullOrWhiteSpace($cmd)) { exit 0 }

# True when $Token appears as a leading command word (start of the command or
# right after ; & ( { newline, or after '||'), but NOT when piped into via a
# single '|' (that is output filtering, which the dedicated tools cannot do).
function Test-CommandPosition {
    param([string]$Command, [string]$Token)
    $rx = [regex]::new('(?i)(?<![\w-])' + [regex]::Escape($Token) + '(?![\w-])')
    foreach ($m in $rx.Matches($Command)) {
        $j = $m.Index - 1
        while ($j -ge 0 -and ($Command[$j] -eq ' ' -or $Command[$j] -eq "`t")) { $j-- }
        if ($j -lt 0) { return $true }
        $prev = $Command[$j]
        if ($prev -eq '|') {
            if ($j -ge 1 -and $Command[$j - 1] -eq '|') { return $true }  # '||' chain
            continue                                                       # single pipe -> allow
        }
        if ($prev -eq ';' -or $prev -eq '&' -or $prev -eq '(' -or $prev -eq '{' -or $prev -eq "`n" -or $prev -eq "`r") {
            return $true
        }
        # any other preceding char means mid-argument (e.g. inside a path) -> not a command
    }
    return $false
}

$reason = $null

foreach ($t in @('Get-Content', 'gc', 'cat', 'head', 'tail')) {
    if (Test-CommandPosition -Command $cmd -Token $t) {
        $reason = "Use the Read tool instead of '$t' to read files (project rule: file reads go through Read, not the shell)."
        break
    }
}

if (-not $reason) {
    foreach ($t in @('Select-String', 'findstr', 'grep', 'rg')) {
        if (Test-CommandPosition -Command $cmd -Token $t) {
            $reason = "Use the Grep tool instead of '$t' to search file contents (project rule: content search goes through Grep, not the shell)."
            break
        }
    }
}

if (-not $reason) {
    if (Test-CommandPosition -Command $cmd -Token 'find') {
        $reason = "Use the Glob tool instead of 'find' to locate files (project rule: file lookup goes through Glob, not the shell)."
    }
    elseif ($cmd -match '(?i)-Recurse') {
        foreach ($t in @('Get-ChildItem', 'gci', 'ls', 'dir')) {
            if (Test-CommandPosition -Command $cmd -Token $t) {
                $reason = "Use the Glob tool instead of '$t -Recurse' to enumerate files (project rule: file lookup goes through Glob, not the shell)."
                break
            }
        }
    }
}

if ($reason) {
    $out = @{
        hookSpecificOutput = @{
            hookEventName            = 'PreToolUse'
            permissionDecision       = 'deny'
            permissionDecisionReason = $reason
        }
    }
    $out | ConvertTo-Json -Compress -Depth 5
}

exit 0
