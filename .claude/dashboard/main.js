const { app, BrowserWindow, ipcMain } = require('electron');
const path = require('path');
const fs = require('fs');

const ROOT = path.join(__dirname, '../..');
let mainWindow;

// ── 데이터 파일 읽기 ──────────────────────────────────────

function readDashboardData() {
  const data = {
    tasks: null,
    lastHarness: null,
    discussions: { items: [] },
    todayLog: []
  };

  // TASKS.md 파싱
  try {
    const content = fs.readFileSync(path.join(ROOT, 'TASKS.md'), 'utf8');
    let completed = 0, total = 0;
    const nextTasks = [];
    let inProto = false;

    const allRemaining = [];
    for (const line of content.split('\n')) {
      if (line.startsWith('## 프로토타입')) inProto = true;
      else if (line.startsWith('## 후순위')) inProto = false;

      if (line.match(/^- \[x\]/i)) { completed++; total++; }
      else if (line.match(/^- \[ \]/)) {
        total++;
        const taskName = line.replace(/^- \[ \] /, '').split(' —')[0].trim();
        allRemaining.push(taskName);
        if (inProto && nextTasks.length < 3) nextTasks.push(taskName);
      }
    }
    data.tasks = { completed, total, nextTasks, allRemaining };
  } catch (e) {}

  // harness-last.md 파싱
  try {
    const content = fs.readFileSync(path.join(ROOT, '.claude/harness-last.md'), 'utf8');
    const taskMatch = content.match(/## 작업\n(.+)/);
    const scoreMatch = content.match(/## 점수\n(\d+)/);
    data.lastHarness = {
      task: taskMatch ? taskMatch[1].trim() : '없음',
      score: scoreMatch ? parseInt(scoreMatch[1]) : 0
    };
  } catch (e) {}

  // discussions.json 파싱
  try {
    const content = fs.readFileSync(path.join(ROOT, '.claude/discussions.json'), 'utf8');
    data.discussions = JSON.parse(content);
  } catch (e) {}

  // harness-spec.md 파싱 (Planner 스펙 요약)
  try {
    const content = fs.readFileSync(path.join(ROOT, '.claude/harness-spec.md'), 'utf8');
    data.spec = content.trim();
  } catch (e) {
    data.spec = '';
  }

  // harness-status.json 파싱
  try {
    const content = fs.readFileSync(path.join(ROOT, '.claude/harness-status.json'), 'utf8');
    data.status = JSON.parse(content);
  } catch (e) {
    data.status = { phase: 'idle', task: '', lastScore: 0, lastResult: '' };
  }

  // 오늘 로그 파싱 (최근 5개)
  try {
    const today = new Date().toISOString().slice(0, 10);
    const content = fs.readFileSync(path.join(ROOT, `.claude/harness-logs/${today}.md`), 'utf8');
    const entries = [];
    const lines = content.split('\n');

    for (let i = 0; i < lines.length; i++) {
      const m = lines[i].match(/^## \[(\d+:\d+)\] (.+)/);
      if (!m) continue;
      const chunk = lines.slice(i).join('\n');
      const scoreM = chunk.match(/점수: (\d+)\/100/);
      const passM = chunk.match(/최종: (PASS|FAIL)/);
      entries.push({
        time: m[1],
        task: m[2],
        score: scoreM ? parseInt(scoreM[1]) : null,
        result: passM ? passM[1] : null
      });
    }
    data.todayLog = entries.slice(-5).reverse();
  } catch (e) {}

  return data;
}

// ── 윈도우 생성 ──────────────────────────────────────────

function createWindow() {
  mainWindow = new BrowserWindow({
    width: 960,
    height: 640,
    resizable: false,
    frame: false,
    backgroundColor: '#2d1f0e',
    webPreferences: {
      nodeIntegration: true,
      contextIsolation: false
    }
  });

  mainWindow.loadFile('index.html');

  // 로드 완료 후 초기 데이터 전송
  mainWindow.webContents.once('did-finish-load', () => {
    mainWindow.webContents.send('data-update', readDashboardData());
  });
}

// ── 앱 시작 ──────────────────────────────────────────────

app.whenReady().then(() => {
  createWindow();

  // 3초마다 데이터 갱신
  setInterval(() => {
    if (mainWindow) {
      mainWindow.webContents.send('data-update', readDashboardData());
    }
  }, 3000);
});

app.on('window-all-closed', () => app.quit());

// ── IPC 핸들러 ────────────────────────────────────────────

ipcMain.on('window-close', () => app.quit());
ipcMain.on('window-minimize', () => mainWindow && mainWindow.minimize());

// 회의 항목 해결 처리
ipcMain.on('resolve-discussion', (event, id) => {
  const filePath = path.join(ROOT, '.claude/discussions.json');
  try {
    const data = JSON.parse(fs.readFileSync(filePath, 'utf8'));
    data.items = data.items.filter(item => item.id !== id);
    fs.writeFileSync(filePath, JSON.stringify(data, null, 2));
    event.sender.send('data-update', readDashboardData());
  } catch (e) {}
});
