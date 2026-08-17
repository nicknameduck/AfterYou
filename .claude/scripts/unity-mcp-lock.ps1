# Unity MCP 세션 잠금 훅 — 멀티 세션이 동시에 Unity 에디터를 조작해 상태가 꼬이는 것을 방지한다.
# (2026-08-18 실측 배경: 두 세션 동시 조작 → 플레이 토글 충돌·이중 레벨 로드·리컴파일로 인한 상태 초기화)
#
# - PreToolUse(mcp__UnityMCP__*): 락이 없거나 내 것이면 갱신 후 통과, 다른 세션의 유효 락이면 차단(exit 2)
# - Stop/SessionEnd(-Release): 내 락이면 삭제 → 턴이 끝나는 즉시 다른 세션이 이어받을 수 있다
# - TTL(15분): 크래시로 남은 락은 무시하고 넘겨받는다
param(
    [switch]$Release
)

$ErrorActionPreference = 'Stop'

$lockPath = Join-Path (Split-Path $PSScriptRoot -Parent) 'unity-mcp.lock'
$ttlMinutes = 15

# 훅 입력(JSON)에서 세션 ID를 읽는다.
$raw = [Console]::In.ReadToEnd()
$sessionId = ''
try {
    $inputJson = $raw | ConvertFrom-Json
    if ($inputJson -and $inputJson.session_id) { $sessionId = [string]$inputJson.session_id }
} catch {}
if (-not $sessionId) { exit 0 }  # 세션 식별 불가면 개입하지 않는다(안전한 무동작)

if ($Release) {
    if (Test-Path $lockPath) {
        try {
            $lock = Get-Content $lockPath -Raw | ConvertFrom-Json
            if ($lock.session_id -eq $sessionId) { Remove-Item $lockPath -Force -Confirm:$false }
        } catch { try { Remove-Item $lockPath -Force -Confirm:$false } catch {} }
    }
    exit 0
}

# 획득/검사
if (Test-Path $lockPath) {
    $lock = $null
    try { $lock = Get-Content $lockPath -Raw | ConvertFrom-Json } catch {}

    if ($lock -and $lock.session_id -and $lock.session_id -ne $sessionId) {
        $ageMinutes = $ttlMinutes + 1
        try { $ageMinutes = ((Get-Date) - [DateTime]::Parse($lock.timestamp)).TotalMinutes } catch {}

        if ($ageMinutes -lt $ttlMinutes) {
            $shortId = $lock.session_id.Substring(0, [Math]::Min(8, $lock.session_id.Length))
            [Console]::Error.WriteLine("Unity MCP 잠금: 다른 세션($shortId)이 Unity 에디터를 사용 중입니다(마지막 활동 $([int]$ageMinutes)분 전). Unity 조작은 보류하세요 — 파일/문서 작업을 먼저 진행하거나, 사용자에게 'Unity는 다른 세션 점유 중'이라 보고 후 대기하세요. 잠금은 상대 세션의 턴 종료 시 자동 해제되며, 이후 재시도하면 통과합니다.")
            exit 2
        }
        # TTL 초과 → 잔존 락으로 간주하고 넘겨받는다.
    }
}

@{ session_id = $sessionId; timestamp = (Get-Date).ToString('o') } | ConvertTo-Json | Set-Content $lockPath -Encoding utf8
exit 0
