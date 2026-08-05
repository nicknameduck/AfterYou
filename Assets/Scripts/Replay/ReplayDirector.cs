using System.Collections;
using System.Collections.Generic;
using AfterYou.Clone;
using AfterYou.Level;
using AfterYou.Managers;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AfterYou.Replay
{
    /// <summary>
    /// 클리어 직후 "지금까지의 전 클론 + 방금 클리어한 라이브"를 동시에 되재생하는 연출 구동자.
    /// </summary>
    /// <remarks>
    /// 설계: RoundManager의 상태 머신 밖에 있는 독립 오버레이다. 리플레이 동안 상태는 Cleared로 잔존하므로
    /// RoundManager의 FixedUpdate / Update / Rewind가 전부 휴면 상태이며, 재생 대상은 전원 Kinematic이라
    /// 물리 간섭이 0이다. 그래서 코어에 새 상태(enum)를 추가하지 않고도 안전하게 얹힌다.
    ///
    /// 틱 소유권도 동일한 규약을 따른다 — 리플레이 동안의 중앙 틱은 이 클래스의 _replayTick 하나뿐이고,
    /// 구동 순서(박스 → 기믹 → 클론)는 RoundManager.FixedUpdate와 같아야 재현이 어긋나지 않는다.
    /// </remarks>
    public class ReplayDirector : MonoBehaviour
    {
        [Tooltip("클리어 통지를 받고 재생 대상을 읽어올 라운드 매니저.")]
        [SerializeField] private RoundManager _roundManager;

        [Tooltip("협력 고리 타임라인 수집기. 리플레이 시작 직전 타임라인을 확정받는다.")]
        [SerializeField] private ChainTimelineTracker _chainTracker;

        [Tooltip("고조 연출 UI(단계 카운트 / 화면 펄스 / 스킵 힌트). 미할당이면 연출 없이 재생만 한다.")]
        [SerializeField] private ReplayFlourishUI _flourishUI;

        [Tooltip("클리어 순간부터 리플레이가 시작되기까지의 대기 시간(초).")]
        [SerializeField] private float _startDelay = 0.6f;

        [Tooltip("리플레이 시작 직후 스킵 입력을 무시하는 유예 시간(초). 클리어 순간에 누른 키가 그대로 새는 것을 막는다.")]
        [SerializeField] private float _skipGrace = 0.25f;

        /// <summary>재생 대상 복사본. RoundManager의 확정 스택은 종료 시 리셋되므로 여기서 절연해 들고 있는다.</summary>
        private readonly List<CharacterActor> _actors = new List<CharacterActor>();

        /// <summary>확정된 타임라인. Begin에서 한 번 채우고 재생 중에는 읽기만 한다.</summary>
        private readonly List<ChainEvent> _timeline = new List<ChainEvent>();

        private PushableBox[] _boxes = System.Array.Empty<PushableBox>();
        private ITickGimmick[] _gimmicks = System.Array.Empty<ITickGimmick>();

        /// <summary>클리어 → Begin 사이의 대기 코루틴. null이 아니면 "곧 시작한다"는 뜻이다.</summary>
        private Coroutine _pendingRoutine;

        private int _replayTick;
        private int _replayLength;
        private int _nextEventIndex;
        private int _firedChainCount;
        private float _elapsedSinceBegin;

        /// <summary>리플레이가 끝난 프레임 번호. 같은 프레임의 Enter가 다음 레벨로 새는 것을 막는 데 쓴다.</summary>
        private int _endedFrame = -1;

        /// <summary>리플레이 재생 중인가(대기 구간은 제외).</summary>
        public bool IsReplaying { get; private set; }

        /// <summary>
        /// 클리어 상태의 "다음 레벨" 입력(N/Enter/Next 버튼)을 삼켜야 하는가.
        /// </summary>
        /// <remarks>
        /// 재생 중만으로는 부족하다 — ① 대기 구간에 Enter가 새면 레벨이 먼저 언로드돼 Begin이 파괴된 액터를 잡고,
        /// ② 스킵한 프레임에 실행 순서가 어긋나면 같은 입력이 레벨 전환으로 이어진다. 세 구간 모두 막는다.
        /// </remarks>
        public bool BlocksClearedInput => _pendingRoutine != null || IsReplaying || Time.frameCount == _endedFrame;

        private void OnEnable()
        {
            if (_roundManager != null)
                _roundManager.Cleared += OnRoundCleared;
        }

        private void OnDisable()
        {
            if (_roundManager != null)
                _roundManager.Cleared -= OnRoundCleared;
        }

        /// <summary>새 레벨의 박스/기믹을 주입한다. LevelManager가 라운드 구동 직전에 호출한다.</summary>
        public void BindLevel(PushableBox[] boxes, ITickGimmick[] gimmicks)
        {
            _boxes = boxes ?? System.Array.Empty<PushableBox>();
            _gimmicks = gimmicks ?? System.Array.Empty<ITickGimmick>();
        }

        /// <summary>
        /// 재생과 대기를 즉시 중단하고 연출 잔재를 걷는다. 레벨 정리(ESC/디버그 이동/레벨 교체)가 호출한다.
        /// </summary>
        /// <remarks>확정 스택 리셋은 하지 않는다 — 이 경로에서는 RoundManager가 곧 Teardown되기 때문이다.</remarks>
        public void StopImmediate()
        {
            if (_pendingRoutine != null)
            {
                StopCoroutine(_pendingRoutine);
                _pendingRoutine = null;
            }

            Finish();
        }

        private void OnRoundCleared()
        {
            if (IsReplaying || _pendingRoutine != null) return;

            // ⚠ 이 통지는 LevelExit의 물리 트리거 콜백 스택 안에서 온다. 여기서 곧장 SetMode/bodyType을 만지면
            //   물리 스텝 도중 콜라이더 구성이 바뀐다. 딜레이가 0이어도 최소 1프레임은 미룬다.
            _pendingRoutine = StartCoroutine(BeginAfterDelayRoutine());
        }

        private IEnumerator BeginAfterDelayRoutine()
        {
            float elapsed = 0f;
            while (elapsed < _startDelay)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            // 딜레이가 0으로 설정돼 있어도 물리 콜백 스택을 확실히 벗어나도록 한 프레임을 더 둔다.
            yield return null;

            _pendingRoutine = null;
            Begin();
        }

        /// <summary>보드를 시작 상태로 되돌리고 전원을 클론으로 세워 재생을 개시한다.</summary>
        private void Begin()
        {
            if (_roundManager == null) return;

            _replayTick = 0;
            _nextEventIndex = 0;
            _firedChainCount = 0;
            _elapsedSinceBegin = 0f;

            // ① 재생 대상 복사(절연) — 확정 클론 전부.
            _actors.Clear();
            for (int i = 0; i < _roundManager.ConfirmedCount; i++)
            {
                CharacterActor clone = _roundManager.GetConfirmedActor(i);
                if (clone == null) continue;

                clone.Playback.ResetToStart();
                _actors.Add(clone);
            }

            // ② 클리어 테이크의 라이브를 클론으로 전환한다.
            //    ⚠ 순서 엄수: SetMode(Clone) → SetRecording → ResetToStart.
            //    SetRecording 전에는 FrameCount가 0이라 ResetToStart가 통째로 no-op이 되어 시작 지점으로 돌아가지 않는다.
            CharacterActor live = _roundManager.LiveCharacter;
            if (live != null)
            {
                live.SetMode(CharacterMode.Clone);
                live.Playback.SetRecording(live.Recorder.Recording);
                live.Playback.ResetToStart();
                _actors.Add(live);
            }

            // ③ 리플레이 길이 = 클리어 테이크의 기록 길이. 확정 클론들은 재생이 끝나면 마지막 프레임을 홀드한다
            //    (녹화 중에도 그랬으므로 클리어 순간의 그림이 그대로 재현된다).
            _replayLength = live != null ? live.Playback.FrameCount : 0;
            if (_replayLength <= 0)
            {
                // 재생할 것이 없으면 연출을 건너뛰되 종료 후처리(확정 스택 리셋)는 동일하게 밟는다.
                EndReplay();
                return;
            }

            // ④ 기믹 초기화. 압력판은 ITickGimmick이 아니지만 매 FixedUpdate 자기 치유형이라
            //    보드가 비워지는 다음 스텝에 자동으로 떼어짐 상태가 된다.
            for (int i = 0; i < _gimmicks.Length; i++)
                _gimmicks[i].ResetGimmick();

            // ⑤ 박스 초기화 — EnterSelecting과 같은 2패스다. 먼저 전부 Frozen + base로 되돌린 뒤
            //    소유(확정 궤적이 있는) 박스만 Replay로 올린다. 미이동으로 기록을 버린 박스가 Replay에 걸리면 안 된다.
            for (int i = 0; i < _boxes.Length; i++)
            {
                _boxes[i].SetMode(PushableBoxMode.Frozen);
                _boxes[i].ResetToBase();
            }
            for (int i = 0; i < _boxes.Length; i++)
            {
                if (_boxes[i].OwnerSlot >= 0)
                    _boxes[i].SetMode(PushableBoxMode.Replay);
            }

            // ⑥ 타임라인 확정 — 시작 시점에 고리 수 N이 고정되고, 이후에는 읽기만 한다.
            _timeline.Clear();
            if (_chainTracker != null)
                _chainTracker.BuildTimeline(_replayLength - 1, _timeline);

            IsReplaying = true;

            if (_flourishUI != null)
                _flourishUI.BeginReplay(_timeline.Count);
        }

        private void FixedUpdate()
        {
            if (!IsReplaying) return;

            // 구동 순서는 RoundManager.FixedUpdate와 동일해야 한다: 박스 → 기믹 → 클론.
            for (int i = 0; i < _boxes.Length; i++)
            {
                PushableBox box = _boxes[i];
                if (box != null && box.OwnerSlot >= 0 && box.Mode == PushableBoxMode.Replay)
                    box.ApplyBoxTick(_replayTick);
            }

            for (int i = 0; i < _gimmicks.Length; i++)
                _gimmicks[i].DriveGimmickTick(_replayTick);

            for (int i = 0; i < _actors.Count; i++)
            {
                if (_actors[i] != null)
                    _actors[i].Playback.ApplyTick(_replayTick);
            }

            FireDueChains();

            _replayTick++;

            if (_replayTick >= _replayLength)
                EndReplay();
        }

        /// <summary>
        /// 이번 틱까지 도달한 고리를 전부 발화한다.
        /// </summary>
        /// <remarks>
        /// ⚠ 등호(==) 비교가 아니라 소급(&lt;=) 방식이다. 기믹 이벤트의 틱 스탬프는 컴포넌트 실행 순서에 따라
        /// ±1틱 흔들릴 수 있는데, 정확히 일치하는 틱만 보면 그 고리가 통째로 유실되어
        /// "고리 N개 = 고조 N단계" 정합이 무너진다.
        /// </remarks>
        private void FireDueChains()
        {
            while (_nextEventIndex < _timeline.Count && _timeline[_nextEventIndex].Tick <= _replayTick)
            {
                _nextEventIndex++;
                _firedChainCount++;

                if (_flourishUI != null)
                    _flourishUI.PlayChainStep(_firedChainCount);
            }
        }

        private void Update()
        {
            if (!IsReplaying) return;

            _elapsedSinceBegin += Time.deltaTime;
            if (_elapsedSinceBegin < _skipGrace) return;

            if (IsSkipPressed())
                EndReplay();
        }

        /// <summary>아무 키/클릭이 스킵이다. 단, 창 전환·타이틀 복귀용 키는 스킵으로 치지 않는다.</summary>
        private bool IsSkipPressed()
        {
            Mouse mouse = Mouse.current;
            if (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame))
                return true;

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return false;
            if (!keyboard.anyKey.wasPressedThisFrame) return false;

            // ESC는 타이틀 복귀 전용이고, Alt/Tab/Win은 Alt-Tab·윈도우 키 조합이 유령 스킵으로 새는 통로다.
            if (keyboard.escapeKey.isPressed) return false;
            if (keyboard.leftAltKey.isPressed || keyboard.rightAltKey.isPressed) return false;
            if (keyboard.tabKey.isPressed) return false;
            if (keyboard.leftMetaKey.isPressed || keyboard.rightMetaKey.isPressed) return false;

            return true;
        }

        /// <summary>완주/스킵 공통 종료 경로. 연출을 걷고 되돌리기 이력까지 정리한다.</summary>
        private void EndReplay()
        {
            Finish();

            if (_roundManager != null)
                _roundManager.ResetUndoStack();
        }

        /// <summary>종료 공통 후처리. 재생 플래그/연출/복사본을 원위치시킨다(확정 스택은 건드리지 않는다).</summary>
        private void Finish()
        {
            IsReplaying = false;
            _endedFrame = Time.frameCount;
            _actors.Clear();
            _timeline.Clear();

            if (_flourishUI != null)
                _flourishUI.EndReplay();
        }
    }
}
