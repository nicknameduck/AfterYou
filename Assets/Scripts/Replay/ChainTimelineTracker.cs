using System.Collections.Generic;
using AfterYou.Clone;
using AfterYou.Level;
using AfterYou.Managers;
using AfterYou.Player;
using UnityEngine;

namespace AfterYou.Replay
{
    /// <summary>협력 고리의 종류. 같은 틱에 겹친 고리를 인과 순서대로 세우는 정렬 2차 키로도 쓴다.</summary>
    public enum ChainEventType
    {
        /// <summary>압력판이 눌렸다.</summary>
        PlatePressed,

        /// <summary>문이 열렸다(압력판 연동 Door / 스위치 연동 TimedDoor 공용).</summary>
        DoorOpened,

        /// <summary>라이브가 클론 위로 착지했다.</summary>
        LandedOnClone,

        /// <summary>라이브가 출구에 도달했다.</summary>
        Cleared
    }

    /// <summary>타임라인 1건. 틱 스탬프 + 종류 + 귀속 슬롯만 갖는 값 타입이다.</summary>
    public readonly struct ChainEvent
    {
        /// <summary>이 고리가 발생한 중앙 틱.</summary>
        public readonly int Tick;

        /// <summary>고리 종류.</summary>
        public readonly ChainEventType Type;

        /// <summary>착지 고리의 귀속 슬롯(되감기 시 그 슬롯분만 걷어내기 위한 꼬리표). 그 외 고리는 -1.</summary>
        public readonly int Slot;

        public ChainEvent(int tick, ChainEventType type, int slot)
        {
            Tick = tick;
            Type = type;
            Slot = slot;
        }
    }

    /// <summary>
    /// 클리어 리플레이용 "협력 고리" 수집기. 씬 상주이며 레벨마다 기믹/캐릭터에 재바인딩된다.
    /// </summary>
    /// <remarks>
    /// 왜 기하 재계산이 아니라 이벤트 로깅인가:
    ///  압력판/문 판정을 리플레이용으로 다시 계산하면 기믹 로직이 이중 소스가 되어 언젠가 반드시 어긋난다.
    ///  기믹은 "매 테이크 리셋 + 클론이 궤적으로 재현"이라는 규약 덕에 클리어 테이크에서 전부 다시 발화하므로,
    ///  클리어 테이크의 발화 로그가 곧 리플레이 타임라인이다 — 재계산이 아예 필요 없다.
    ///
    /// 그래서 로그의 수명이 두 갈래다:
    ///  - 기믹 로그: 테이크가 시작될 때마다 통째로 비운다(마지막 = 클리어 테이크분만 남는다).
    ///  - 착지 로그: 라이브 전용 이벤트라 클론이 재현해 주지 않는다. 테이크별로 모아 두었다가
    ///    확정되면 슬롯에 귀속시켜 누적하고, 되감기면 그 슬롯분을 걷어낸다.
    /// </remarks>
    public class ChainTimelineTracker : MonoBehaviour
    {
        [Tooltip("틱 스탬프와 기록 게이트(Recording 여부)를 읽어올 라운드 매니저.")]
        [SerializeField] private RoundManager _roundManager;

        /// <summary>클리어 테이크의 기믹 고리 로그. TakeStarted마다 비운다.</summary>
        private readonly List<ChainEvent> _gimmickLog = new List<ChainEvent>();

        /// <summary>확정이 끝난 테이크들의 착지 고리(슬롯 귀속 완료분).</summary>
        private readonly List<ChainEvent> _confirmedLandings = new List<ChainEvent>();

        /// <summary>이번 테이크의 임시 착지 고리. 확정되면 위로 옮기고, 되감기면 버린다.</summary>
        private readonly List<ChainEvent> _pendingLandings = new List<ChainEvent>();

        // 구독 해제를 위해 바인딩한 소스를 그대로 보관한다. 레벨 인스턴스는 통째로 파괴되지만
        // 파괴 전에 명시적으로 끊어야 다음 레벨에서 이중 구독/유령 참조가 남지 않는다.
        private readonly List<PressurePlate> _plates = new List<PressurePlate>();
        private readonly List<Door> _doors = new List<Door>();
        private readonly List<TimedDoor> _timedDoors = new List<TimedDoor>();
        private readonly List<PlayerController> _controllers = new List<PlayerController>();
        private LevelExit _levelExit;

        private void OnEnable()
        {
            if (_roundManager == null) return;

            _roundManager.TakeStarted += OnTakeStarted;
            _roundManager.TakeConfirmed += OnTakeConfirmed;
            _roundManager.SlotRewound += OnSlotRewound;
            _roundManager.Cleared += OnRoundCleared;
        }

        private void OnDisable()
        {
            if (_roundManager == null) return;

            _roundManager.TakeStarted -= OnTakeStarted;
            _roundManager.TakeConfirmed -= OnTakeConfirmed;
            _roundManager.SlotRewound -= OnSlotRewound;
            _roundManager.Cleared -= OnRoundCleared;
        }

        /// <summary>
        /// 새 레벨의 기믹/캐릭터에 구독을 건다. LevelManager가 라운드 구동(Initialize) 직전에 호출한다.
        /// </summary>
        /// <remarks>비활성 상태로 설치된 기믹(스위치로 켜지는 문 등)도 잡도록 includeInactive를 켠다.</remarks>
        public void BindLevel(LevelDefinition level, IList<CharacterActor> actors)
        {
            UnbindLevel();

            if (level != null)
            {
                _plates.AddRange(level.GetComponentsInChildren<PressurePlate>(true));
                _doors.AddRange(level.GetComponentsInChildren<Door>(true));
                _timedDoors.AddRange(level.GetComponentsInChildren<TimedDoor>(true));

                for (int i = 0; i < _plates.Count; i++) _plates[i].OnPressed += OnPlatePressed;
                for (int i = 0; i < _doors.Count; i++) _doors[i].OnOpened += OnDoorOpened;
                for (int i = 0; i < _timedDoors.Count; i++) _timedDoors[i].OnOpened += OnDoorOpened;

                _levelExit = level.LevelExit;
                if (_levelExit != null)
                    _levelExit.OnCleared += OnExitReached;
            }

            if (actors != null)
            {
                for (int i = 0; i < actors.Count; i++)
                {
                    if (actors[i] == null) continue;

                    PlayerController controller = actors[i].GetComponent<PlayerController>();
                    if (controller == null) continue;

                    controller.OnLandedOnClone += OnLandedOnClone;
                    _controllers.Add(controller);
                }
            }
        }

        /// <summary>구독을 전부 끊고 로그를 비운다. 레벨 정리(LevelManager.CleanupCurrentLevel)가 호출한다.</summary>
        public void UnbindLevel()
        {
            for (int i = 0; i < _plates.Count; i++)
            {
                if (_plates[i] != null) _plates[i].OnPressed -= OnPlatePressed;
            }
            for (int i = 0; i < _doors.Count; i++)
            {
                if (_doors[i] != null) _doors[i].OnOpened -= OnDoorOpened;
            }
            for (int i = 0; i < _timedDoors.Count; i++)
            {
                if (_timedDoors[i] != null) _timedDoors[i].OnOpened -= OnDoorOpened;
            }
            for (int i = 0; i < _controllers.Count; i++)
            {
                if (_controllers[i] != null) _controllers[i].OnLandedOnClone -= OnLandedOnClone;
            }

            if (_levelExit != null)
                _levelExit.OnCleared -= OnExitReached;
            _levelExit = null;

            _plates.Clear();
            _doors.Clear();
            _timedDoors.Clear();
            _controllers.Clear();

            _gimmickLog.Clear();
            _confirmedLandings.Clear();
            _pendingLandings.Clear();
        }

        /// <summary>
        /// 수집된 고리를 하나의 타임라인으로 병합해 results에 채운다. 리플레이 시작 직전 1회 호출된다.
        /// </summary>
        /// <param name="maxTick">리플레이의 마지막 틱. 이보다 늦게 스탬프된 고리는 이 틱으로 당겨 붙인다.</param>
        /// <param name="results">호출자가 소유하는 결과 버퍼(GC 회피를 위해 재사용된다).</param>
        /// <remarks>
        /// 왜 초과 틱을 버리지 않고 당기는가: 클리어 고리는 물리 트리거 스택에서 스탬프되므로
        /// 리플레이 길이(라이브 기록 프레임 수)를 1~2틱 넘어설 수 있다. 버리면 마지막 고조가 통째로 사라진다.
        /// </remarks>
        public void BuildTimeline(int maxTick, List<ChainEvent> results)
        {
            results.Clear();

            AppendClamped(_gimmickLog, maxTick, results);
            AppendClamped(_confirmedLandings, maxTick, results);

            // (틱, 종류) 순 정렬 — 같은 틱에 겹친 고리는 "밟음 → 문 열림 → 착지 → 클리어" 인과 순서로 세운다.
            results.Sort((a, b) => a.Tick != b.Tick ? a.Tick.CompareTo(b.Tick) : ((int)a.Type).CompareTo((int)b.Type));
        }

        private static void AppendClamped(List<ChainEvent> source, int maxTick, List<ChainEvent> results)
        {
            for (int i = 0; i < source.Count; i++)
            {
                ChainEvent chainEvent = source[i];
                int tick = Mathf.Clamp(chainEvent.Tick, 0, Mathf.Max(0, maxTick));
                results.Add(new ChainEvent(tick, chainEvent.Type, chainEvent.Slot));
            }
        }

        /// <summary>녹화 중에만 기록한다. 리플레이 재생이 만드는 재발화를 로그에 섞지 않기 위한 유일한 게이트다.</summary>
        private bool CanRecord => _roundManager != null && _roundManager.State == RoundState.Recording;

        private void OnPlatePressed()
        {
            if (!CanRecord) return;
            _gimmickLog.Add(new ChainEvent(_roundManager.CurrentTick, ChainEventType.PlatePressed, -1));
        }

        private void OnDoorOpened()
        {
            if (!CanRecord) return;
            _gimmickLog.Add(new ChainEvent(_roundManager.CurrentTick, ChainEventType.DoorOpened, -1));
        }

        private void OnLandedOnClone()
        {
            if (!CanRecord) return;
            _pendingLandings.Add(new ChainEvent(_roundManager.CurrentTick, ChainEventType.LandedOnClone, -1));
        }

        private void OnExitReached()
        {
            if (!CanRecord) return;
            _gimmickLog.Add(new ChainEvent(_roundManager.CurrentTick, ChainEventType.Cleared, -1));
        }

        private void OnTakeStarted()
        {
            // 기믹은 테이크마다 리셋 후 클론이 다시 재현하므로, 이전 테이크의 로그는 통째로 무효다.
            _gimmickLog.Clear();
            _pendingLandings.Clear();
        }

        private void OnTakeConfirmed(int slot)
        {
            for (int i = 0; i < _pendingLandings.Count; i++)
            {
                ChainEvent landing = _pendingLandings[i];
                _confirmedLandings.Add(new ChainEvent(landing.Tick, landing.Type, slot));
            }
            _pendingLandings.Clear();
        }

        private void OnSlotRewound(int slot)
        {
            for (int i = _confirmedLandings.Count - 1; i >= 0; i--)
            {
                if (_confirmedLandings[i].Slot == slot)
                    _confirmedLandings.RemoveAt(i);
            }
            _pendingLandings.Clear();
        }

        private void OnRoundCleared()
        {
            // 클리어 테이크에는 TakeConfirmed가 오지 않는다. 여기서 임시 착지 로그를 확정분으로 귀속시키지 않으면
            // 클리어 테이크의 "클론 밟기" 고리가 통째로 누락된다. 되감기 대상이 아니므로 슬롯 꼬리표는 -1.
            for (int i = 0; i < _pendingLandings.Count; i++)
                _confirmedLandings.Add(_pendingLandings[i]);

            _pendingLandings.Clear();
        }
    }
}
