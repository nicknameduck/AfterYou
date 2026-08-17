using System;
using System.Collections.Generic;
using AfterYou.Clone;
using AfterYou.Core;
using AfterYou.Level;
using AfterYou.Managers;
using AfterYou.Player;
using AfterYou.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AfterYou.Replay
{
    /// <summary>협력 고리의 종류. 사전 스캔이 분류하고 HUD/연출이 소비한다.</summary>
    public enum ReplayRingType
    {
        /// <summary>압력판이 눌린 순간.</summary>
        PlatePressed,

        /// <summary>연동된 문이 열린 순간(판 눌림과 같은 틱).</summary>
        DoorOpened,

        /// <summary>히어로가 클론의 등 위로 착지한 순간.</summary>
        LandedOnClone,

        /// <summary>히어로가 출구에 도달한 순간(궤적의 마지막 틱).</summary>
        Cleared
    }

    /// <summary>사전 스캔이 뽑아낸 협력 고리 1건 — 종류 + 발생 틱.</summary>
    public readonly struct ReplayRing
    {
        public readonly ReplayRingType Type;
        public readonly int Tick;

        public ReplayRing(ReplayRingType type, int tick)
        {
            Type = type;
            Tick = tick;
        }
    }

    /// <summary>
    /// 클리어 리플레이 감독. 전 클론 + 히어로의 궤적을 처음부터 동시에 되재생한다.
    /// </summary>
    /// <remarks>
    /// ── 왜 RoundManager가 아니라 별도 감독인가 ──
    /// 클리어(Cleared) 순간 RoundManager.FixedUpdate는 `_state != Recording`에서 즉시 return하므로 휴면 상태가 된다.
    /// 리플레이는 그 휴면 구간에서만 도는 "코어 밖 오버레이 모드"다. 중앙 틱 구동을 여기서 대행하면
    /// RoundManager의 라운드 로직(압사 해소 / 스폰 겹침 / 확정 스택)을 한 줄도 건드리지 않아도 된다.
    ///
    /// ── 사전 스캔을 쓰는 이유 ──
    /// 리플레이 중에는 히어로가 Clone 모드(컨트롤러 off)라 OnLandedOnClone/OnCleared를 발화할 주체가 없다.
    /// 그래서 재생 "전"에 헤드리스로 한 바퀴 돌려 고리 타임라인을 뽑아 두고, 재생 중에는 그 틱에 맞춰
    /// OnRingReached로 대행 발행한다. (압력판/문은 자체 FixedUpdate로 항상 살아 있어 실제로도 자연 발화한다 —
    /// 스캔 틱과 결정론적으로 일치한다. 아래 배치 규약 주석 참조.)
    ///
    /// ── 구독하지 않는다 ──
    /// 이 클래스는 기믹 이벤트(OnPressed/OnOpened 등)를 구독하지 않는다. 타임라인의 단일 소스는 사전 스캔이다.
    /// 실발화와 스캔을 섞으면 어느 쪽이 진실인지 알 수 없게 되고, 고리가 이중 계수된다.
    /// </remarks>
    public class ReplayDirector : MonoBehaviour
    {
        /// <summary>1패스 완주 후 반복 재생까지의 대기 시간(초).</summary>
        private const float LoopDelaySeconds = 2f;

        [Tooltip("리플레이 HUD(스킵 힌트). 미할당이면 HUD 없이 재생만 한다.")]
        [SerializeField] private ReplayHudUI _hud;

        [Tooltip("완주 후 히어로가 출구 중심으로 빨려 들어가는 시간(초).")]
        [SerializeField] private float _absorbDuration = 0.35f;

        /// <summary>고리 도달 순간 발행된다. 인자는 누적 고조 단계(1부터).</summary>
        public event Action<int> OnRingReached;

        // ── 런타임 주입(LevelManager.LoadLevel → Bind) ──
        private RoundManager _roundManager;
        private CharacterActor[] _actors = Array.Empty<CharacterActor>();
        private PushableBox[] _boxes = Array.Empty<PushableBox>();
        private ITickGimmick[] _gimmicks = Array.Empty<ITickGimmick>();

        /// <summary>이번 레벨의 출구. 완주 흡입 연출의 목표 지점이다. 레벨에 출구가 없으면 null이다.</summary>
        private LevelExit _levelExit;

        /// <summary>녹화 시간 상한(초) 캐시. 리플레이 HUD의 남은 시간 카운트다운 기준.</summary>
        private float _maxRecordSeconds;

        /// <summary>레벨의 압력판들. 판/문은 ITickGimmick이 아니라 자체 FixedUpdate로 돌기 때문에 주입 경로가 없어 직접 수집한다.</summary>
        private PressurePlate[] _plates = Array.Empty<PressurePlate>();

        /// <summary>스캔 중 판별 상태(직전 틱의 눌림 여부). t=0은 기준선만 잡고 고리로 세지 않는다.</summary>
        private bool[] _platePressedBaseline = Array.Empty<bool>();

        /// <summary>이번 리플레이의 출연진 = 확정 클론 전원 + 히어로. 마지막 원소가 히어로다.</summary>
        private readonly List<CharacterActor> _cast = new List<CharacterActor>();

        /// <summary>확정 궤적을 가진 박스만 재생 대상이다.</summary>
        private readonly List<PushableBox> _replayBoxes = new List<PushableBox>();

        /// <summary>사전 스캔 결과. 틱 오름차순이다.</summary>
        private readonly List<ReplayRing> _timeline = new List<ReplayRing>();

        /// <summary>마지막 라이브 캐릭터(= "지금의 나"). 원본 룩으로 재생된다.</summary>
        private CharacterActor _hero;

        private int _totalTicks;
        private int _replayTick;
        private int _nextRingIndex;
        private int _stage;

        private bool _isPlaying;
        private bool _hasStartedThisLevel;
        private bool _hasRunPostProcess;

        /// <summary>1패스 완주 후 반복까지의 대기 중인가. 이 동안 틱 구동은 완전히 정지한다.</summary>
        private bool _isWaitingLoop;
        private float _loopResumeTime;

        /// <summary>완주 직후 출구 흡입 연출 중인가. 이 동안 틱 구동은 정지하고 히어로 위치만 보간한다.</summary>
        private bool _isAbsorbing;
        private float _absorbElapsed;

        /// <summary>
        /// 리플레이 중인가.
        /// </summary>
        /// <remarks>
        /// 스킵 입력이 Enter/N이면 같은 프레임에 LevelManager.Update도 그 키를 봐서
        /// "스킵 + 다음 레벨 로드"가 함께 발동한다 — 이것은 의도된 동작이다(사용자 확정 2026-08-06:
        /// 클리어 후 Enter는 리플레이 관람 여부와 무관하게 곧장 다음 스테이지로 가야 한다).
        /// </remarks>
        public bool IsReplaying => _isPlaying;

        /// <summary>이번 리플레이의 총 협력 고리 수(사전 스캔 결과). 미재생이면 0.</summary>
        public int RingCount => _timeline.Count;

        /// <summary>
        /// 리플레이 경과 시간(초) — 현재 틱을 고정 스텝으로 환산한 값.
        /// </summary>
        /// <remarks>
        /// 흡입/루프 대기 중엔 _replayTick이 고정되어 시간 표시도 멈춘다.
        /// 클리어 순간 값에서 정지하는 RoundManager.ElapsedSeconds 규약과 동형이다.
        /// </remarks>
        public float ReplayElapsedSeconds => _replayTick * Time.fixedDeltaTime;

        /// <summary>리플레이 남은 시간(초) — 녹화 상한에서 경과를 뺀 값. 0 아래로는 내려가지 않는다.</summary>
        public float ReplayRemainingSeconds => Mathf.Max(0f, _maxRecordSeconds - ReplayElapsedSeconds);

        /// <summary>
        /// 레벨 로드 시 LevelManager가 이번 레벨의 구동 대상을 주입한다. 레벨마다 1회.
        /// </summary>
        public void Bind(RoundManager roundManager, CharacterActor[] actors, PushableBox[] boxes, ITickGimmick[] gimmicks,
            LevelExit levelExit)
        {
            _roundManager = roundManager;
            _actors = actors ?? Array.Empty<CharacterActor>();
            _boxes = boxes ?? Array.Empty<PushableBox>();
            _gimmicks = gimmicks ?? Array.Empty<ITickGimmick>();

            // 출구는 없을 수 있다(LevelDefinition.LevelExit 미할당) — 그 경우 흡입 연출은 건너뛰고 기존 완주 경로로 폴백한다.
            _levelExit = levelExit;
            _maxRecordSeconds = roundManager != null ? roundManager.MaxRecordSeconds : 0f;

            // 압력판은 레벨 프리팹 안에 있고 ITickGimmick이 아니므로 주입되지 않는다. 여기서 직접 수집한다.
            // 비활성 판은 제외한다 — FixedUpdate가 돌지 않아 판정 앵커(_anchorTop)가 캐시되지 않은 상태다.
            _plates = FindObjectsByType<PressurePlate>();
            _platePressedBaseline = new bool[_plates.Length];

            _isPlaying = false;
            _isWaitingLoop = false;
            _isAbsorbing = false;
            _hasStartedThisLevel = false;
            _hasRunPostProcess = false;
            _hero = null;

            _cast.Clear();
            _replayBoxes.Clear();
            _timeline.Clear();
        }

        /// <summary>
        /// 레벨 언로드 직전 LevelManager가 호출한다(다음 레벨 / ESC 타이틀 / 엔딩 진입 전부 이 경로).
        /// 재생 중이었다면 스킵과 동일하게 정지·후처리한다(StopReplay는 멱등이라 이중 호출이 무해하다).
        /// </summary>
        public void Unbind()
        {
            StopReplay();

            _roundManager = null;
            _actors = Array.Empty<CharacterActor>();
            _boxes = Array.Empty<PushableBox>();
            _gimmicks = Array.Empty<ITickGimmick>();
            _levelExit = null;
            _plates = Array.Empty<PressurePlate>();
            _platePressedBaseline = Array.Empty<bool>();

            _cast.Clear();
            _replayBoxes.Clear();
            _timeline.Clear();

            _hero = null;
            _hasStartedThisLevel = false;
            _hasRunPostProcess = false;
        }

        private void Update()
        {
            if (_roundManager == null) return;

            if (!_isPlaying)
            {
                // 레벨당 1회만 진입한다. 스킵/완주 후 Cleared가 유지되어도 다시 시작하지 않는다.
                if (!_hasStartedThisLevel && _roundManager.State == RoundState.Cleared)
                    StartReplay();

                // 시작한 프레임에는 스킵 입력을 보지 않는다 — 클리어 직전 입력이 그대로 스킵으로 새는 것을 막는다.
                return;
            }

            if (WasSkipInputPressed())
                StopReplay();
        }

        /// <summary>아무 키 또는 마우스 클릭. New Input System 전용 프로젝트라 구 Input.*은 쓰지 않는다.</summary>
        private static bool WasSkipInputPressed()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.anyKey.wasPressedThisFrame) return true;

            Mouse mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
                return true;

            return false;
        }

        /// <summary>
        /// 리플레이를 시작한다. 아래 ①~⑨ 순서는 서로 의존하므로 바꾸지 말 것(각 단계 주석에 이유가 있다).
        /// </summary>
        private void StartReplay()
        {
            // ① 히어로 = 마지막 라이브. 궤적이 비어 있으면(비정상) 리플레이를 포기하고 기존 클리어 흐름만 남긴다.
            CharacterActor hero = _roundManager.LiveCharacter;
            if (hero == null || hero.Recorder == null || hero.Recorder.FrameCount == 0)
            {
                _hasStartedThisLevel = true;
                return;
            }

            _hasStartedThisLevel = true;
            _hero = hero;
            _totalTicks = hero.Recorder.FrameCount;

            // 캐스트 식별은 공개 API만 쓴다: Clone 모드 액터 = 확정된 클론, LiveCharacter = 히어로.
            // 히어로를 마지막에 넣어 재생 순서에서도 "지금의 나"가 가장 나중에 갱신되게 한다.
            _cast.Clear();
            for (int i = 0; i < _actors.Length; i++)
            {
                CharacterActor actor = _actors[i];
                if (actor == null || actor == hero) continue;
                if (actor.Mode == CharacterMode.Clone)
                    _cast.Add(actor);
            }
            _cast.Add(hero);

            // 궤적을 가진 박스만 재생한다(소유 박스 + 이번 라이브 라운드에 밀린 미확정 박스).
            _replayBoxes.Clear();
            for (int i = 0; i < _boxes.Length; i++)
            {
                if (_boxes[i] != null && _boxes[i].TryGetRecordedPosition(0, out _))
                    _replayBoxes.Add(_boxes[i]);
            }

            // ② 사전 스캔 — 반드시 SetMode(Clone) "전"에 한다.
            //    히어로가 아직 Default 레이어라 IsCloneUnderfoot()이 자기 자신을 잡지 않는다.
            ScanTimeline();

            // ③ 히어로의 미확정 녹화를 재생 원본으로 건다.
            //    클리어 순간엔 ConfirmClone이 불리지 않아 궤적이 Recorder.Recording에만 남아 있다.
            hero.Playback.SetRecording(hero.Recorder.Recording);

            // ④ 캐스트 전원을 Clone 모드로(Kinematic + 재생 컴포넌트 on + 조작 off).
            for (int i = 0; i < _cast.Count; i++)
                _cast[i].SetMode(CharacterMode.Clone);

            // ⑤ 각자 궤적의 첫 프레임(스폰)으로 복귀 — "처음부터 동시 재생"의 출발점.
            for (int i = 0; i < _cast.Count; i++)
                _cast[i].Playback.ResetToStart();

            // ⑥ 히어로만 원본 룩. ③의 SetRecording이 목표 알파를 기본값으로 되돌리므로 반드시 그 이후여야 한다.
            hero.ApplyReplayOriginalLook();

            // ⑦ 박스는 Replay 모드 + base 위치. 궤적의 frames[0]은 정의상 base 위치다(BeginRecording이 base에서 시작).
            for (int i = 0; i < _replayBoxes.Count; i++)
            {
                _replayBoxes[i].SetMode(PushableBoxMode.Replay);
                _replayBoxes[i].ResetToBase();
            }

            // ⑧ 기믹 초기화 — 라이브 플레이가 남긴 상태(열린 문/무너진 발판 등)를 전부 되돌린다.
            for (int i = 0; i < _gimmicks.Length; i++)
                _gimmicks[i].ResetGimmick();

            // ⑨ 틱 0에서 시작 + HUD 표시.
            _replayTick = 0;
            _nextRingIndex = 0;
            _stage = 0;
            _isWaitingLoop = false;
            _isAbsorbing = false;
            _hasRunPostProcess = false;
            _isPlaying = true;

            if (_hud != null)
                _hud.Show();
        }

        /// <summary>
        /// 헤드리스 사전 스캔 — 한 프레임 안에서 전 틱을 훑어 협력 고리 타임라인을 만든다.
        /// </summary>
        /// <remarks>
        /// 부수효과 0의 질의만 쓴다(EvaluatePressed / IsCloneUnderfoot). 기믹 이벤트를 발화시키지 않으며
        /// 판/문의 상태도 건드리지 않는다. 물리 시뮬레이션은 이 사이에 돌지 않으므로(Update 내부 1프레임)
        /// 배치 → SyncTransforms → 질의가 결정론적이다.
        ///
        /// 스캔 종료 시점의 배치는 복원하지 않는다 — 호출부(StartReplay)가 곧바로
        /// ResetToStart / ResetToBase / ResetGimmick으로 전부 덮어쓴다.
        /// </remarks>
        private void ScanTimeline()
        {
            _timeline.Clear();

            PlayerController heroController = _hero.GetComponent<PlayerController>();
            CloneRecording heroRecording = _hero.Recorder.Recording;

            if (_platePressedBaseline.Length != _plates.Length)
                _platePressedBaseline = new bool[_plates.Length];

            bool wasCloneUnderfoot = false;

            for (int t = 0; t < _totalTicks; t++)
            {
                // ⚠ frames[t]로 배치한다. t+1이 아니다.
                //   실재생에서 ApplyTick은 frames[t+1]을 "목표"로 MovePosition을 건다 — 이동은 물리 스텝 "도중"에 일어나고,
                //   FixedUpdate(= 판이 자기 판정을 도는 시점)에서 캐스트가 실제로 서 있는 위치는 언제나 frames[t]다.
                //   t+1로 배치하면 스캔 틱이 실발화보다 정확히 1틱 빨라진다.
                for (int i = 0; i < _cast.Count; i++)
                {
                    CharacterActor actor = _cast[i];

                    if (actor == _hero)
                        actor.SetPosition(heroRecording.GetFrame(t).Position);
                    else if (actor.Playback.TryGetFuturePosition(t, out Vector2 clonePosition))
                        actor.SetPosition(clonePosition);
                }

                for (int i = 0; i < _replayBoxes.Count; i++)
                {
                    if (!_replayBoxes[i].TryGetRecordedPosition(t, out Vector2 boxPosition)) continue;

                    Transform boxTransform = _replayBoxes[i].transform;
                    boxTransform.position = new Vector3(boxPosition.x, boxPosition.y, boxTransform.position.z);
                }

                // AutoSyncTransforms가 꺼져 있어 배치만으로는 물리 질의가 옛 위치를 본다.
                Physics2D.SyncTransforms();

                for (int i = 0; i < _plates.Length; i++)
                {
                    if (_plates[i] == null) continue;

                    bool isPressed = _plates[i].EvaluatePressed();

                    // t=0은 기준선만 잡는다 — 시작부터 눌려 있던 판을 "이번 라운드의 고리"로 세지 않기 위해서다.
                    if (t >= 1 && isPressed && !_platePressedBaseline[i])
                    {
                        _timeline.Add(new ReplayRing(ReplayRingType.PlatePressed, t));

                        // 연동 문은 판이 눌린 그 FixedUpdate 안에서 SetOpen된다 → 같은 틱이다.
                        if (_plates[i].LinkedDoor != null)
                            _timeline.Add(new ReplayRing(ReplayRingType.DoorOpened, t));
                    }

                    _platePressedBaseline[i] = isPressed;
                }

                if (heroController != null)
                {
                    bool isCloneUnderfoot = heroController.IsCloneUnderfoot();

                    if (t >= 1 && isCloneUnderfoot && !wasCloneUnderfoot)
                        _timeline.Add(new ReplayRing(ReplayRingType.LandedOnClone, t));

                    wasCloneUnderfoot = isCloneUnderfoot;
                }
            }

            // 정렬 후에 Cleared를 붙인다 — 모든 고리의 틱은 T-1 이하이므로 이 순서가 곧 오름차순이고,
            // 동률 틱에서 불안정 정렬이 "탈출"을 중간으로 밀어넣는 일도 없다.
            _timeline.Sort(CompareRingByTick);
            _timeline.Add(new ReplayRing(ReplayRingType.Cleared, _totalTicks - 1));
        }

        /// <summary>틱 오름차순 비교자. 매 스캔마다 람다를 새로 만들지 않도록 정적 캐시로 둔다.</summary>
        private static readonly Comparison<ReplayRing> CompareRingByTick = (a, b) => a.Tick.CompareTo(b.Tick);

        private void FixedUpdate()
        {
            if (!_isPlaying) return;

            // 완주 후 반복까지의 대기 — 틱 구동을 완전히 정지한 채 실시간으로만 센다.
            // ⚠ ITickGimmick의 "실시간 API 금지" 제약은 틱으로 구동되는 대상에만 적용된다.
            //   이 대기는 틱 밖의 순수 연출 지연(구동 자체가 멈춰 있다)이라 재생 결정성에 영향을 주지 않는다.
            if (_isWaitingLoop)
            {
                if (Time.realtimeSinceStartup >= _loopResumeTime)
                    ResetForLoop();
                return;
            }

            // 완주 직후 출구 흡입 — 대기 분기와 마찬가지로 틱 구동 "앞"의 early-return이어야 한다.
            // ⚠ 여기서 아래로 흘려보내면 _replayTick == _totalTicks 상태로 ApplyTick이 불려
            //   frames[tick+1] 규약이 궤적 범위 밖 프레임을 조회한다.
            if (_isAbsorbing)
            {
                DriveAbsorb();
                return;
            }

            // RoundManager.FixedUpdate와 동일한 순서(박스 → 기믹 → 캐스트)를 그대로 대행한다.
            // 순서가 다르면 같은 틱에서 기믹이 보는 박스/캐스트 위치가 라이브 플레이와 달라진다.
            for (int i = 0; i < _replayBoxes.Count; i++)
                _replayBoxes[i].ApplyBoxTick(_replayTick);

            for (int i = 0; i < _gimmicks.Length; i++)
                _gimmicks[i].DriveGimmickTick(_replayTick);

            for (int i = 0; i < _cast.Count; i++)
                _cast[i].Playback.ApplyTick(_replayTick);

            // 타임라인 고조: 이번 틱까지 도달한 고리를 전부 소비한다(같은 틱에 여러 고리가 있을 수 있다).
            while (_nextRingIndex < _timeline.Count && _timeline[_nextRingIndex].Tick <= _replayTick)
            {
                _nextRingIndex++;
                _stage++;

                OnRingReached?.Invoke(_stage);
            }

            _replayTick++;

            if (_replayTick >= _totalTicks)
            {
                RunPostProcessOnce();

                BeginAbsorb();
            }
        }

        /// <summary>
        /// 완주 시점의 페이드아웃 연출을 개시한다. 히어로나 출구가 없으면 연출 없이 곧장 루프 대기로 넘어간다.
        /// </summary>
        /// <remarks>
        /// 이전의 "출구 중심 흡입"(위치 보간)은 출구를 세로로 키운 뒤 히어로가 문 한가운데로 떠오르는
        /// 부작용이 있어 폐기(2026-08-17 사용자 결정) — 문에 닿은 그 자리에서 점점 사라지는 것으로 대체.
        /// 반복 재생이라 패스마다 다시 불린다 — 경과를 매번 새로 잡아야 두 번째 패스부터 즉시 끝나지 않는다.
        /// </remarks>
        private void BeginAbsorb()
        {
            if (_hero == null || _levelExit == null)
            {
                _isWaitingLoop = true;
                _loopResumeTime = Time.realtimeSinceStartup + LoopDelaySeconds;
                return;
            }

            _absorbElapsed = 0f;
            _isAbsorbing = true;
        }

        /// <summary>페이드아웃 1스텝. 틱 구동 밖의 순수 알파 보간이라 클론 재생 결정성에 영향을 주지 않는다.</summary>
        private void DriveAbsorb()
        {
            // 시간 소스는 fixedDeltaTime으로 통일한다 — realtimeSinceStartup과 섞으면 스텝 누적이 어긋난다.
            _absorbElapsed += Time.fixedDeltaTime;

            if (_absorbElapsed >= _absorbDuration)
            {
                _hero.Playback.SetDisplayAlpha(0f); // 보간 잔차 없이 완전히 사라진 상태로 스냅.

                _isAbsorbing = false;
                _isWaitingLoop = true;
                _loopResumeTime = Time.realtimeSinceStartup + LoopDelaySeconds;
                return;
            }

            float t = Mathf.SmoothStep(0f, 1f, _absorbElapsed / _absorbDuration);
            _hero.Playback.SetDisplayAlpha(1f - t);
        }

        /// <summary>반복 재생 준비 — 기믹/캐스트/박스를 전부 처음 상태로 되돌린다.</summary>
        private void ResetForLoop()
        {
            // 완주 페이드아웃으로 투명해진 히어로를 되살린다 — 위치는 ResetToStart가 되돌리지만
            // 표시 알파는 별도 채널이라 명시 복원이 필요하다.
            if (_hero != null)
                _hero.Playback.SetDisplayAlpha(1f);

            for (int i = 0; i < _gimmicks.Length; i++)
                _gimmicks[i].ResetGimmick();

            for (int i = 0; i < _cast.Count; i++)
                _cast[i].Playback.ResetToStart();

            for (int i = 0; i < _replayBoxes.Count; i++)
                _replayBoxes[i].ResetToBase();

            _replayTick = 0;
            _nextRingIndex = 0;
            _stage = 0;
            _isWaitingLoop = false;
        }

        /// <summary>
        /// 리플레이를 즉시 끝낸다. 스킵 입력과 Unbind가 공유하는 유일한 정지 경로다(멱등).
        /// </summary>
        /// <remarks>
        /// 캐스트는 현 위치에 그대로 동결된다 — 이미 Clone 모드(Kinematic)라 틱 구동만 멈추면 멈춘다.
        /// RoundState는 Cleared 그대로 두므로 Next 버튼 / N / Enter / ESC 기존 흐름이 즉시 정상 동작한다.
        /// </remarks>
        public void StopReplay()
        {
            if (!_isPlaying) return;

            _isPlaying = false;
            _isWaitingLoop = false;
            _isAbsorbing = false;

            // 페이드 도중/완료 상태에서 스킵해도 히어로가 반투명·투명으로 남지 않게 복원한다.
            if (_hero != null)
                _hero.Playback.SetDisplayAlpha(1f);

            RunPostProcessOnce();

            if (_hud != null)
                _hud.Hide();
        }

        /// <summary>종료 후처리 — 완주와 스킵이 동일해야 하고, 반복 재생 때문에 여러 번 불려도 1회만 수행해야 한다.</summary>
        private void RunPostProcessOnce()
        {
            if (_hasRunPostProcess) return;
            _hasRunPostProcess = true;

            if (_roundManager != null)
                _roundManager.ResetUndoStack();
        }
    }
}
