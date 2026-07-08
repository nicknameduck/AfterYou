const { ipcRenderer, clipboard } = require('electron');
const path = require('path');

// ── 스프라이트 ────────────────────────────────────────────

const SPRITE_BASE = path.join(__dirname, '../../Assets/Art/Player').replace(/\\/g, '/');
const SPRITES = {
  right: `file:///${SPRITE_BASE}/Fox_Walk/Fox_Normal_Walk_dir3.png`,
  left:  `file:///${SPRITE_BASE}/Fox_Walk/Fox_Normal_Walk_dir7.png`,
  idle:  `file:///${SPRITE_BASE}/Fox_Idle/Fox_Idle_dir1.png`,
};

// ── 말풍선 메시지 풀 ─────────────────────────────────────

const SPEECH = {
  idle: [
    '오늘도 열심히냥~',
    '버그 없겠냥?',
    '다음 작업은 뭘까냥',
    '졸리다냥... 😪',
    '커피 한 잔 하고 싶냥...',
    '이거 맞게 하는 거냥?',
  ],
  planning: [
    '기획 검토 중이냥~',
    'GDD 분석하는 중냥!',
    '어떤 기능 만들까냥?',
    '설계도 작성 중냥!',
    '스펙 꼼꼼하게 짜야냥!',
    '신중하게 설계해야냥!',
  ],
  generating: [
    '코딩 시작이냥!',
    '열심히 구현 중냥~',
    '버그 없게 만들어야냥!',
    '빨리 완성해야냥!',
    'PASS 받아야지냥!',
    '집중해야냥... 🔥',
  ],
  eval_wait: [
    '결과 기다리는 중냥...',
    '두근두근냥~',
    '몇 점 나올까냥?',
    '잘 됐겠지냥?',
  ],
  eval_low: [
    '겨우 통과냥... 아슬아슬냥',
    '70점대냥, 더 잘할 수 있냥!',
    '가까스로 PASS냥...',
  ],
  eval_mid: [
    '꽤 잘 했냥! 80점이냥~',
    '좋은 코드냥!',
    '80점 넘었냥~',
  ],
  eval_high: [
    '완벽냥!! ⭐',
    '90점 넘었냥!!!',
    '최고의 코드냥~ ✨',
  ],
  eval_fail: [
    '으... 실패냥 😿',
    'FAIL이냥... 다시 해야냥',
    '좀 더 노력해야냥...',
  ],
};

// ── 룸 X 위치 (cat-container .left 기준) ────────────────
// 각 룸 너비 ≈ (960 - 12) / 3 ≈ 316px, 중심 - 반 스프라이트(32px)
const ROOM_PLANNING   = 128;   // 기획실 입구 앞
const ROOM_COMPUTER   = 444;   // 개발실 입구 앞
const ROOM_EVALUATION = 760;   // 평가 입구 앞
const WANDER_MIN      = 20;
const WANDER_MAX      = 876;   // 960 - 64 - 20

// ── 고양이 상태 ──────────────────────────────────────────

const cat = {
  x:           ROOM_COMPUTER,
  direction:   1,
  isIdle:      false,
  idleTimer:   0,
  speechTimer: 0,
  phase:       'idle',
  targetX:     null,   // null = 배회, 숫자 = 고정 위치
  atTarget:    false,
};

const MOVE_SPEED   = 2.2;  // px/frame @60fps (룸 이동)
const WANDER_SPEED = 0.9;  // px/frame (배회)

const catContainer = document.getElementById('cat-container');
const catSprite    = document.getElementById('cat-sprite');
const speechBubble = document.getElementById('speech-bubble');
const speechText   = document.getElementById('speech-text');

function setSprite(url) {
  catSprite.style.backgroundImage = `url("${url}")`;
}
function setWalking(dir) {
  setSprite(dir > 0 ? SPRITES.right : SPRITES.left);
  catSprite.classList.remove('idle');
  catSprite.classList.add('walking');
}

setSprite(SPRITES.right);
catSprite.classList.add('walking');

// ── 말풍선 ───────────────────────────────────────────────

function randomFrom(arr) {
  return arr[Math.floor(Math.random() * arr.length)];
}

function showSpeech(text) {
  speechText.textContent = text;
  speechBubble.classList.remove('hidden');
  speechBubble.style.animation = 'none';
  speechBubble.offsetHeight;
  speechBubble.style.animation = '';
  cat.speechTimer = 3500;
}

function hideSpeech() {
  speechBubble.classList.add('hidden');
  cat.speechTimer = 0;
}

let _lastStatus = null;

function pickSpeech() {
  const phase  = _lastStatus?.phase  || 'idle';
  const score  = _lastStatus?.lastScore  || 0;
  const result = _lastStatus?.lastResult || '';

  // 전광판 구역(X>700)에 있을 때는 점수 반응 우선
  if (cat.x > 700 && score > 0) {
    if (result === 'FAIL')  return randomFrom(SPEECH.eval_fail);
    if (score >= 90)        return randomFrom(SPEECH.eval_high);
    if (score >= 80)        return randomFrom(SPEECH.eval_mid);
    return randomFrom(SPEECH.eval_low);
  }

  switch (phase) {
    case 'planning':   return randomFrom(SPEECH.planning);
    case 'generating': return randomFrom(SPEECH.generating);
    case 'evaluating': return randomFrom(SPEECH.eval_wait);
    default:           return randomFrom(SPEECH.idle);
  }
}

// ── idle 진입 ────────────────────────────────────────────

function enterIdle(speechChance = 0.4) {
  cat.isIdle    = true;
  cat.idleTimer = 1200 + Math.random() * 2200;
  setSprite(SPRITES.idle);
  catSprite.classList.remove('walking');
  catSprite.classList.add('idle');
  if (Math.random() < speechChance) showSpeech(pickSpeech());
}

function exitIdle() {
  cat.isIdle = false;
  setWalking(cat.direction);
}

// 룸 도착 시 — 항상 말풍선 표시
function enterIdleAtRoom() {
  cat.isIdle    = true;
  cat.idleTimer = 1500 + Math.random() * 1000;
  setSprite(SPRITES.idle);
  catSprite.classList.remove('walking');
  catSprite.classList.add('idle');
  showSpeech(pickSpeech());
}

// 룸 앞 대기 중 — 가끔 말풍선, 위치 고정
function stayAtTarget() {
  cat.isIdle    = true;
  cat.idleTimer = 2500 + Math.random() * 3500;
  setSprite(SPRITES.idle);
  catSprite.classList.remove('walking');
  catSprite.classList.add('idle');
  if (Math.random() < 0.4) showSpeech(pickSpeech());
}

// ── tick ─────────────────────────────────────────────────

let lastTime = performance.now();

function tick(now) {
  const dt = Math.min(now - lastTime, 50);
  lastTime  = now;
  const ff  = dt / 16.67;

  if (cat.speechTimer > 0) {
    cat.speechTimer -= dt;
    if (cat.speechTimer <= 0) hideSpeech();
  }

  if (cat.isIdle) {
    cat.idleTimer -= dt;
    if (cat.idleTimer <= 0) {
      if (cat.targetX !== null && cat.atTarget) {
        stayAtTarget();   // 룸 앞 — 계속 대기
      } else {
        exitIdle();       // 배회 재개
      }
    }
  } else {
    if (cat.targetX !== null && !cat.atTarget) {
      // 목적지로 이동 중
      const diff = cat.targetX - cat.x;
      cat.direction = diff > 0 ? 1 : -1;
      const step = MOVE_SPEED * ff;
      if (Math.abs(diff) <= step) {
        cat.x = cat.targetX;
        cat.atTarget = true;
        enterIdleAtRoom();    // 도착
      } else {
        cat.x += cat.direction * step;
        setWalking(cat.direction);
      }

    } else if (cat.targetX !== null && cat.atTarget) {
      // 이동 완료 직후 stayAtTarget으로 연결 (isIdle=false 짧은 틈)
      stayAtTarget();

    } else {
      // 배회 (idle 페이즈)
      cat.x += cat.direction * WANDER_SPEED * ff;
      if (cat.x >= WANDER_MAX) {
        cat.x = WANDER_MAX; cat.direction = -1; enterIdle(0.35);
      } else if (cat.x <= WANDER_MIN) {
        cat.x = WANDER_MIN; cat.direction = 1;  enterIdle(0.35);
      } else if (Math.random() < 0.00012 * ff) {
        enterIdle(0.35);
      } else {
        setWalking(cat.direction);
      }
    }
  }

  catContainer.style.left = Math.round(cat.x) + 'px';
  requestAnimationFrame(tick);
}

requestAnimationFrame(tick);

// ── 페이즈 변경 처리 ─────────────────────────────────────

function applyPhase(phase) {
  if (phase === cat.phase) return;
  cat.phase = phase;

  const targets = {
    planning:   ROOM_PLANNING,
    generating: ROOM_COMPUTER,
    evaluating: ROOM_EVALUATION,
  };

  if (phase === 'idle') {
    // 배회 모드로 전환 — 현재 위치에서 바로 걷기 시작
    cat.targetX  = null;
    cat.atTarget = false;
    if (cat.isIdle) exitIdle();
    return;
  }

  const newTarget = targets[phase];
  if (newTarget === undefined) return;

  cat.targetX  = newTarget;
  cat.atTarget = false;
  if (cat.isIdle) exitIdle();
}

// ── 데이터 수신 & UI 갱신 ────────────────────────────────

let _lastTasks = null;

ipcRenderer.on('data-update', (_event, data) => {
  _lastStatus = data.status || null;
  _lastTasks  = data.tasks  || null;

  if (data.status)      applyPhase(data.status.phase || 'idle');
  renderTopBar(data.tasks, data.status);
  if (data.lastHarness) renderMonitor(data.lastHarness);
  if (data.tasks)       renderNextTasks(data.tasks);
  renderWhiteboard(data.spec || '', data.discussions);
  renderDiscussionPopup(data.discussions);
  renderTodayLog(data.todayLog);
  renderEvalBoard(data.status, data.lastHarness);
  renderPhaseBadge(data.status?.phase || 'idle');
  renderRemainingCount(data.tasks);
});

// ── 상단 진행률 바 ────────────────────────────────────────

function renderTopBar(tasks, status) {
  const phase    = status?.phase || 'idle';
  const fillEl   = document.getElementById('progress-fill');
  const textEl   = document.getElementById('progress-text');

  if (phase !== 'idle') {
    // 하네스 실행 중: 단계 진행률 표시
    const PHASE_STEPS = { planning: 1, generating: 2, evaluating: 3 };
    const PHASE_LABEL = { planning: '기획 중', generating: '개발 중', evaluating: '평가 중' };
    const step = PHASE_STEPS[phase] || 1;
    const pct  = [33, 66, 95][step - 1];
    const task = status?.task ? `  —  ${status.task}` : '';
    textEl.textContent = `${PHASE_LABEL[phase]} (${step}/3)${task}`;
    fillEl.style.width = pct + '%';
    fillEl.classList.add('running');
  } else {
    // 유휴: TASKS.md 전체 진행률
    fillEl.classList.remove('running');
    if (!tasks) return;
    const pct = tasks.total > 0 ? Math.round((tasks.completed / tasks.total) * 100) : 0;
    textEl.textContent = `전체 진행률  ${tasks.completed} / ${tasks.total}  (${pct}%)`;
    fillEl.style.width = pct + '%';
  }
}

// ── 개발실 모니터 ────────────────────────────────────────

function renderMonitor(last) {
  document.getElementById('monitor-task').textContent = last.task || '—';
  const scoreEl = document.getElementById('monitor-score');
  if (last.score) {
    scoreEl.textContent = `점수: ${last.score}/100`;
    scoreEl.style.color = last.score >= 70 ? '#66cc88' : '#ff6666';
  } else {
    scoreEl.textContent = '';
  }
}

// ── 다음 작업 목록 ────────────────────────────────────────

function renderNextTasks(tasks) {
  const list = document.getElementById('next-tasks-list');
  if (!tasks.nextTasks || tasks.nextTasks.length === 0) {
    list.innerHTML = '<li style="color:#8a6a50;font-size:10px">모든 프로토타입 완료!</li>';
    return;
  }
  list.innerHTML = tasks.nextTasks.map(t => `<li>${escapeHtml(t)}</li>`).join('');
}

// ── 남은 작업 수 배지 ─────────────────────────────────────

function renderRemainingCount(tasks) {
  const badge = document.getElementById('btn-remaining')?.querySelector('.count-badge');
  if (!badge) return;
  if (!tasks || tasks.total === 0) { badge.textContent = '0%'; return; }
  const pct = Math.round((tasks.completed / tasks.total) * 100);
  badge.textContent = pct + '%';
}

// ── 기획실 화이트보드 ─────────────────────────────────────

function renderWhiteboard(spec, discussions) {
  const wbEl = document.getElementById('wb-content');
  const items = discussions?.items || [];

  if (spec) {
    // spec 내용: 첫 5줄만 표시, 나머지는 생략
    const lines = spec.split('\n').filter(l => l.trim());
    const preview = lines.slice(0, 5).join('\n');
    const truncated = lines.length > 5 ? preview + '\n...' : preview;
    wbEl.textContent = truncated;
  } else if (items.length > 0) {
    wbEl.textContent = `⚠️ ${items[0].title}${items.length > 1 ? ` 외 ${items.length - 1}건` : ''}`;
  } else {
    wbEl.textContent = '안건 없음 ✓';
  }
}

function renderDiscussionPopup(discussions) {
  const items      = discussions?.items || [];
  const countBadge = document.getElementById('discussion-count');
  const btn        = document.getElementById('btn-discussion');

  if (items.length === 0) {
    countBadge.classList.add('hidden');
    btn.classList.remove('active');
  } else {
    countBadge.textContent = items.length;
    countBadge.classList.remove('hidden');
    btn.classList.add('active');
  }

  const popupList = document.getElementById('discussion-popup-list');
  if (!popupList) return;
  if (items.length === 0) {
    popupList.innerHTML = '<div style="font-size:11px;color:#9a7a5a;text-align:center;padding:8px">안건 없음 ✓</div>';
    return;
  }
  popupList.innerHTML = items.map(item => `
    <div class="discussion-item">
      <button class="discussion-resolve" data-id="${item.id}">해결</button>
      <div class="discussion-title">⚠️ ${escapeHtml(item.title)}</div>
      <div class="discussion-task">${escapeHtml(item.task || '')}</div>
      <div class="discussion-content">${escapeHtml(item.content || '')}</div>
    </div>
  `).join('');
  popupList.querySelectorAll('.discussion-resolve').forEach(b =>
    b.addEventListener('click', () => ipcRenderer.send('resolve-discussion', b.dataset.id))
  );
}

// ── 오늘 로그 ────────────────────────────────────────────

function renderTodayLog(logs) {
  const listEl = document.getElementById('today-log-list');
  if (!logs || logs.length === 0) {
    listEl.innerHTML = '<div style="font-size:10px;color:#8a6a50;">오늘 기록 없음</div>';
    return;
  }
  listEl.innerHTML = logs.map(e => `
    <div class="log-entry">
      <span class="log-time">${e.time}</span>
      <span class="log-task">${escapeHtml(e.task)}</span>
      ${e.result ? `<span class="log-result ${e.result.toLowerCase()}">${e.result}${e.score ? ` ${e.score}` : ''}</span>` : ''}
    </div>
  `).join('');
}

// ── 평가 전광판 ──────────────────────────────────────────

function renderEvalBoard(status, lastHarness) {
  const starsEl  = document.getElementById('eval-stars');
  const scoreEl  = document.getElementById('eval-score');
  const resultEl = document.getElementById('eval-result');
  const taskEl   = document.getElementById('eval-task');

  const phase  = status?.phase      || 'idle';
  const score  = status?.lastScore  || 0;
  const result = status?.lastResult || '';

  if (phase === 'evaluating') {
    starsEl.textContent  = '⌛';
    scoreEl.textContent  = '?';
    scoreEl.style.color  = '#ff9800';
    resultEl.textContent = '평가 중';
    resultEl.className   = 'eval-result evaluating';
    taskEl.textContent   = status?.task || '';
    return;
  }

  if (score > 0) {
    scoreEl.textContent = score;
    taskEl.textContent  = status?.task || lastHarness?.task || '';
    if (result === 'FAIL') {
      starsEl.textContent  = '💀';
      scoreEl.style.color  = '#ff5252';
      resultEl.textContent = 'FAIL';
      resultEl.className   = 'eval-result fail';
    } else if (score >= 90) {
      starsEl.textContent  = '⭐⭐⭐';
      scoreEl.style.color  = '#00e676';
      resultEl.textContent = 'PASS';
      resultEl.className   = 'eval-result pass';
    } else if (score >= 80) {
      starsEl.textContent  = '⭐⭐☆';
      scoreEl.style.color  = '#00e676';
      resultEl.textContent = 'PASS';
      resultEl.className   = 'eval-result pass';
    } else {
      starsEl.textContent  = '⭐☆☆';
      scoreEl.style.color  = '#ffcc00';
      resultEl.textContent = 'PASS';
      resultEl.className   = 'eval-result pass';
    }
    return;
  }

  starsEl.textContent  = '☆☆☆';
  scoreEl.textContent  = '—';
  scoreEl.style.color  = '#4a6a8a';
  resultEl.textContent = '대기 중';
  resultEl.className   = 'eval-result waiting';
  taskEl.textContent   = '';
}

// ── 페이즈 배지 ──────────────────────────────────────────

function renderPhaseBadge(phase) {
  const badge = document.getElementById('phase-badge');
  const map = {
    planning:   { label: '🎨 기획 중', cls: 'planning' },
    generating: { label: '💻 개발 중', cls: 'generating' },
    evaluating: { label: '📊 평가 중', cls: 'evaluating' },
  };
  const entry = map[phase];
  if (!entry) { badge.classList.add('hidden'); return; }
  badge.textContent = entry.label;
  badge.className   = `phase-badge ${entry.cls}`;
}

// ── 팝업 토글 ────────────────────────────────────────────

function setupPopup(btnId, popupId, closeId) {
  const btn   = document.getElementById(btnId);
  const popup = document.getElementById(popupId);
  const close = document.getElementById(closeId);
  btn.addEventListener('click', () => popup.classList.toggle('hidden'));
  close.addEventListener('click', () => popup.classList.add('hidden'));
}

setupPopup('btn-discussion', 'discussion-popup', 'close-discussion');
setupPopup('btn-remaining',  'remaining-popup',  'close-remaining');

// 남은 작업 팝업 내용 갱신 (버튼 클릭 시 allRemaining 전체 표시)
document.getElementById('btn-remaining').addEventListener('click', () => {
  const popupList = document.getElementById('remaining-popup-list');
  const items = _lastTasks?.allRemaining || [];
  if (items.length === 0) {
    popupList.innerHTML = '<div style="font-size:11px;color:#9a7a5a;text-align:center">남은 작업 없음 ✓</div>';
    return;
  }
  popupList.innerHTML = items.map(t => `<div class="remaining-item">${escapeHtml(t)}</div>`).join('');
});

// ── XSS 방어 ─────────────────────────────────────────────

function escapeHtml(str) {
  return String(str)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

// ── 명령 입력 ────────────────────────────────────────────

const commandInput = document.getElementById('command-input');
const btnCopy      = document.getElementById('btn-copy');

function copyCommand() {
  const cmd = commandInput.value.trim();
  if (!cmd) return;
  clipboard.writeText(cmd);
  btnCopy.textContent = '복사됨!';
  btnCopy.classList.add('copied');
  showSpeech('명령 전달했냥! 🐾');
  setTimeout(() => {
    btnCopy.textContent = '복사';
    btnCopy.classList.remove('copied');
  }, 2000);
}

btnCopy.addEventListener('click', copyCommand);
commandInput.addEventListener('keydown', e => { if (e.key === 'Enter') copyCommand(); });

// ── 창 컨트롤 ────────────────────────────────────────────

document.getElementById('btn-close').addEventListener('click', () => ipcRenderer.send('window-close'));
document.getElementById('btn-minimize').addEventListener('click', () => ipcRenderer.send('window-minimize'));
