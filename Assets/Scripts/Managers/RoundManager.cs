using System.Collections;
using System.Collections.Generic;
using AfterYou.Clone;
using AfterYou.Core;
using AfterYou.Level;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AfterYou.Managers
{
    public enum RoundState
    {
        /// <summary>다음에 조작할 캐릭터를 고르는 중.</summary>
        Selecting,

        /// <summary>라이브 캐릭터를 조작 + 녹화 중. 확정된 클론들은 동시에 재생된다.</summary>
        Recording,

        /// <summary>라이브 캐릭터가 출구에 도달했다.</summary>
        Cleared
    }

    /// <summary>
    /// 라운드 진행의 심장부. "중앙 틱"의 유일한 소유자다.
    /// </summary>
    /// <remarks>
    /// 아키텍처 규약: 클론은 자체 재생 인덱스를 갖지 않는다.
    /// RoundManager가 FixedUpdate마다 _tick을 1 올리고, 모든 클론이 그 하나의 _tick을 참조한다.
    /// (클론마다 자체 인덱스를 두면 컴포넌트 실행 순서/활성화 타이밍에 따라 서로 어긋난다)
    /// </remarks>
    public class RoundManager : MonoBehaviour
    {
        // _characters / _spawnPoint는 런타임 주입이다 (LevelManager.Initialize).
        // 레벨 = 프리팹 구조라 씬의 RoundManager가 레벨별 캐릭터/스폰을 직렬화 참조할 수 없다.
        private CharacterActor[] _characters;
        private Transform _spawnPoint;

        // 레벨의 밀 수 있는 박스들(런타임 주입). 라운드마다 중앙 틱으로 구동해 기록/재생한다.
        private PushableBox[] _boxes;

        // 레벨의 환경 기믹들(런타임 주입). 라운드마다 중앙 틱으로 구동/리셋한다. 비활성 상태로 설치된 기믹도 틱을 받는다.
        private ITickGimmick[] _gimmicks;

        [Header("Recording")]
        [Tooltip("한 테이크의 시간 상한. 초과하면 자동 확정된다.")]
        [SerializeField] private float _maxRecordSeconds = 15f;

        [Header("Hierarchy")]
        [Tooltip("확정된 클론을 이 아래로 reparent해 하이어라키를 정리한다(=== CLONES ===). 되감기 시 원래 부모로 복구.")]
        [SerializeField] private Transform _clonesParent;

        [Header("Crush Resolve")]
        [Tooltip("겹친 채로 옆으로 밀려나는 속도(유닛/초). 겹침이 오래 보이면 올리고, 너무 홱 빠지면 내린다.")]
        [SerializeField] private float _crushEjectSpeed = 10f;

        [Tooltip("클론 X 범위를 벗어난 뒤 추가로 확보할 여유 거리.")]
        [SerializeField] private float _crushSkin = 0.05f;

        /// <summary>확정된 슬롯 스택. 되감기는 최근 → 과거 순서로만 가능하므로 말단 push/pop만 쓴다.</summary>
        private readonly List<int> _confirmedSlots = new List<int>();

        /// <summary>되감기 시 클론을 원래 부모(=== PLAYER ===)로 되돌리기 위한 백업.</summary>
        private readonly Dictionary<int, Transform> _originalParents = new Dictionary<int, Transform>();

        /// <summary>
        /// 스폰 순간 겹친 라이브↔클론 콜라이더 쌍. 충돌을 일시 해제해 "와르르"를 막고,
        /// 클론이 분리되면 복구한다. GC 회피를 위해 재사용하는 리스트다.
        /// </summary>
        private readonly List<(Collider2D live, Collider2D clone)> _ignoredSpawnPairs = new List<(Collider2D, Collider2D)>();

        /// <summary>박스가 "실제로 밀렸다"고 인정하는 최소 이동 거리. 이 미만이면 기록을 버린다(소유 미승격).</summary>
        private const float BoxDisplacementEpsilon = 0.05f;

        private int _tick;
        private int _liveIndex = -1;
        private RoundState _state = RoundState.Selecting;

        /// <summary>LevelManager.Initialize가 끝났는가. false면 입력/틱/재생을 전부 무시한다(레벨 전환 중 안전장치).</summary>
        private bool _isInitialized;

        /// <summary>킬존이 이번 틱에 라이브 사망을 통지했는가. FixedUpdate가 중앙 틱 진입 "전"에 이 플래그를 보고 재시작한다.</summary>
        private bool _isDeathPending;

        /// <summary>현재 라이브를 덮치고 있는 클론 슬롯. 완전히 빠져나올 때까지 래치된다(-1 = 없음).</summary>
        private int _crushSlot = -1;

        /// <summary>래치된 탈출 방향(-1 = 왼쪽 / +1 = 오른쪽). 밀려나는 도중 방향이 뒤집히지 않게 고정한다.</summary>
        private float _crushDirection;

        /// <summary>깔리기 시작한 순간의 Y. 빠져나오는 동안 이 아래로는 내려가지 않게 붙잡아 바닥 파고듦을 막는다.</summary>
        private float _crushLockY;

        public RoundState State => _state;

        public int ConfirmedCount => _confirmedSlots.Count;

        public int SlotCount => _characters != null ? _characters.Length : 0;

        /// <summary>현재 조작 중인 캐릭터. LevelExit이 "클리어는 라이브만" 판정에 쓴다. 없으면 null.</summary>
        public CharacterActor LiveCharacter =>
            _liveIndex >= 0 && _liveIndex < _characters.Length ? _characters[_liveIndex] : null;

        /// <summary>시간 상한을 틱 수로 환산. Fixed Timestep이 바뀌어도 따라가도록 하드코딩하지 않는다.</summary>
        private int MaxTicks => Mathf.CeilToInt(_maxRecordSeconds / Time.fixedDeltaTime);

        public bool IsSlotConfirmed(int index) => _confirmedSlots.Contains(index);

        public bool IsSlotLive(int index) => _state == RoundState.Recording && index == _liveIndex;

        /// <summary>
        /// LevelManager가 레벨 로드 시 캐릭터/스폰을 주입하고 라운드를 구동한다.
        /// RoundManager는 스스로 시작하지 않는다 — 반드시 이 메서드로만 시작된다.
        /// </summary>
        /// <remarks>
        /// 순서 엄수: 이전 레벨 잔재(_confirmedSlots 등)를 먼저 비운 뒤에야 originalParents를 기록하고
        /// 스폰을 통일한다. 잔재를 남기면 다음 레벨의 Rewind가 파괴된 Transform으로 SetParent를 시도한다.
        /// </remarks>
        public void Initialize(CharacterActor[] characters, Transform spawnPoint, PushableBox[] boxes, ITickGimmick[] gimmicks)
        {
            _characters = characters;
            _spawnPoint = spawnPoint;
            _boxes = boxes ?? System.Array.Empty<PushableBox>();
            _gimmicks = gimmicks ?? System.Array.Empty<ITickGimmick>();

            _confirmedSlots.Clear();
            _originalParents.Clear();
            _ignoredSpawnPairs.Clear();
            RestoreAllSpawnOverlaps();

            // 이전 레벨 잔재 방지: 박스 소유/기록을 전부 비운다.
            for (int i = 0; i < _boxes.Length; i++)
                _boxes[i].ClearOwnership();

            // 기믹도 초기 상태로 리셋한다(이전 레벨 잔재 방지).
            for (int i = 0; i < _gimmicks.Length; i++)
                _gimmicks[i].ResetGimmick();

            _tick = 0;
            _liveIndex = -1;
            _crushSlot = -1;
            _isDeathPending = false;

            for (int i = 0; i < _characters.Length; i++)
                _originalParents[i] = _characters[i].transform.parent;

            // 공유 SpawnPoint가 지정돼 있으면 전원의 스폰을 그 한 점으로 통일한다(미지정이면 각자 초기 위치 폴백).
            if (_spawnPoint != null)
            {
                for (int i = 0; i < _characters.Length; i++)
                    _characters[i].OverrideSpawnPosition(_spawnPoint.position);
            }

            _isInitialized = true;
            EnterSelecting();
        }

        /// <summary>
        /// 레벨 언로드 직전 LevelManager가 호출한다. 다음 레벨 로드 전 모든 상태를 안전하게 비운다.
        /// </summary>
        /// <remarks>
        /// ⚠ _liveIndex = -1을 _characters = null보다 반드시 먼저 한다.
        /// LevelExit이 전환 순간 LiveCharacter를 읽으면 _liveIndex>=0 && _characters.Length에서 NRE가 난다.
        /// </remarks>
        public void Teardown()
        {
            _isInitialized = false;
            RestoreAllSpawnOverlaps();
            _confirmedSlots.Clear();
            _originalParents.Clear();
            _ignoredSpawnPairs.Clear();
            _tick = 0;
            _liveIndex = -1;
            _crushSlot = -1;
            _isDeathPending = false;
            _state = RoundState.Selecting;
            _characters = null;
            _boxes = null;
            _gimmicks = null;
        }

        private void OnEnable()
        {
            StartCoroutine(PostPhysicsRoutine());
        }

        private void OnDisable()
        {
            StopAllCoroutines();
        }

        /// <summary>
        /// 물리 시뮬레이션 "직후, 렌더링 직전"에 압사를 해소한다.
        /// </summary>
        /// <remarks>
        /// Unity의 스텝 순서는 [FixedUpdate → 물리 시뮬레이션 → (WaitForFixedUpdate) → 렌더링] 이다.
        /// FixedUpdate에서 위치를 보정하면 바로 뒤의 시뮬레이션이 라이브를 다시 바닥으로 밀어넣어
        /// 보정이 무효화되고, 파묻힌 상태 그대로 화면에 그려진다.
        /// WaitForFixedUpdate 이후는 시뮬레이션이 끝난 시점이라, 여기서 보정하면
        /// 솔버가 만든 파고듦을 되돌린 뒤 렌더링된다 → 파묻힘이 눈에 보이지 않는다.
        /// </remarks>
        private IEnumerator PostPhysicsRoutine()
        {
            WaitForFixedUpdate waitForPhysics = new WaitForFixedUpdate();

            while (true)
            {
                yield return waitForPhysics;

                if (_isInitialized && _state == RoundState.Recording)
                    ResolveCrush();
            }
        }

        private void FixedUpdate()
        {
            if (!_isInitialized) return;
            if (_state != RoundState.Recording) return;

            // 스폰 겹침 복구: 살짝(0.05 초과) 떨어진 쌍부터 충돌을 되살린다.
            // 겹친 채로 복구하면 솔버가 라이브를 홱 튕겨내므로 여유 마진을 둔다.
            for (int i = _ignoredSpawnPairs.Count - 1; i >= 0; i--)
            {
                (Collider2D live, Collider2D clone) pair = _ignoredSpawnPairs[i];
                if (pair.live == null || pair.clone == null)
                {
                    _ignoredSpawnPairs.RemoveAt(i);
                    continue;
                }

                if (pair.live.Distance(pair.clone).distance > 0.05f)
                {
                    Physics2D.IgnoreCollision(pair.live, pair.clone, false);
                    _ignoredSpawnPairs.RemoveAt(i);
                }
            }

            // 박스 구동: 틱 증가 전 현재 _tick 값으로 재생/기록한다 → 클론과 동일 틱을 공유.
            DriveBoxes(_tick);

            // 기믹 구동: 박스와 동일 틱을 공유하도록 틱 증가 "이전"에 호출한다.
            DriveGimmicks(_tick);

            // 사망 처리: 킬존이 DriveGimmicks 순회 중 세운 플래그를, 중앙 틱(재생/기록/틱 증가) 진입 "전"에 처리한다.
            // 루프 안에서 곧장 RestartTake하면 킬존 인덱스 뒤의 기믹만 같은 FixedUpdate에서 한 번 더 구동돼
            // 1틱 위상 desync가 난다. 여기서 return하면 이번 틱의 재생/기록/증가가 통째로 스킵돼 desync 0.
            if (_isDeathPending)
            {
                _isDeathPending = false;
                RestartTake();
                return;
            }

            // 순서 엄수: 재생 → 기록 → 틱 증가.
            // 같은 _tick에서 클론과 라이브가 동일한 물리 스텝을 공유해야 궤적이 재현된다.
            //
            // 압사 해소(ResolveCrush)는 여기가 아니라 "물리 시뮬레이션 이후"에 돈다(PostPhysicsRoutine).
            // FixedUpdate는 물리 이전이라, 여기서 위치를 보정해봤자 곧바로 이어지는 시뮬레이션이
            // 라이브를 다시 바닥으로 밀어넣어 되돌려버린다 → 파묻힌 상태가 그대로 렌더링된다.
            //
            // 보정이 물리 이후에 적용되므로, 그 결과가 다음 틱 시작 위치가 되고
            // 아래 2)의 CaptureTick이 그 보정된 위치를 기록한다 → 재생 시에도 동일하게 재현된다.

            // 1) 확정된 클론들을 중앙 틱에 맞춰 재생
            for (int i = 0; i < _confirmedSlots.Count; i++)
                _characters[_confirmedSlots[i]].Playback.ApplyTick(_tick);

            // 2) 라이브 캐릭터의 현재 상태를 기록
            if (_liveIndex >= 0)
                _characters[_liveIndex].Recorder.CaptureTick(_tick);

            // 3) 틱 증가
            _tick++;

            // 4) 시간 상한 도달 시 자동 확정
            if (_tick >= MaxTicks)
                ConfirmClone();
        }

        /// <summary>
        /// 위에서 내려오는 클론에 깔린 라이브를, 겹친 채로 좌/우로 밀어낸다.
        /// </summary>
        /// <remarks>
        /// 왜 필요한가: 클론은 Kinematic + MovePosition이라 "무조건 목표 위치로" 간다. 라이브(Dynamic)가
        /// 바닥과 클론 사이에 끼면 물리 솔버가 탈출로를 못 찾아 그대로 파고든다.
        /// 그래서 탈출 방향을 코드가 직접 정해준다.
        ///
        /// 설계: 겹침을 "막지 않고" 허용한다. 잠깐 겹쳐 보이더라도 일정한 속도로 스르륵 빠져나오는 쪽이,
        /// 닿기도 전에 미리 비켜서거나(부자연스러움) 홱 튕겨 나가는 것보다 낫다.
        ///
        /// 래치가 핵심: 한 번 깔리면 완전히 빠져나올 때까지 계속 밀어낸다.
        /// 클론이 덮친 뒤 그 자리에서 멈추면(녹화 종료) 하강 속도가 0이 되는데,
        /// 그때 밀어내기를 멈추면 라이브가 영원히 파묻힌 채 갇힌다.
        /// 방향도 함께 래치해 밀려나는 도중에 좌/우가 뒤집히지 않게 한다.
        ///
        /// 깔림을 "새로" 인정하는 조건:
        ///  (a) 실제로 겹쳤다 — 닿기 전에는 건드리지 않는다.
        ///  (b) 클론이 라이브보다 "위" — 클론 위에 올라선 경우(클론이 아래)는 제외.
        ///      클론이 점프해 위에 탄 라이브를 들어올리는 건 정상 동작(엘리베이터)이다.
        ///  (c) 클론이 "내려오는 중" — 라이브가 스스로 클론에 뛰어올라 타려는 걸 밀어내면 안 된다.
        /// </remarks>
        private void ResolveCrush()
        {
            if (_liveIndex < 0)
            {
                _crushSlot = -1;
                return;
            }

            CharacterActor live = _characters[_liveIndex];
            Collider2D liveCollider = live.Collider;
            if (liveCollider == null) return;

            // 이미 깔려 있던 상태라면, 완전히 빠져나올 때까지 래치된 방향으로 계속 민다.
            if (_crushSlot >= 0)
            {
                CharacterActor latched = _characters[_crushSlot];
                Collider2D latchedCollider = latched.Collider;

                if (latchedCollider != null && latchedCollider.enabled &&
                    liveCollider.Distance(latchedCollider).isOverlapped)
                {
                    Push(live, liveCollider.bounds, latchedCollider.bounds, _crushDirection);
                    return;
                }

                // 빠져나왔다.
                _crushSlot = -1;
            }

            // 새로 깔리는 클론을 찾는다.
            for (int i = 0; i < _confirmedSlots.Count; i++)
            {
                int slot = _confirmedSlots[i];
                CharacterActor clone = _characters[slot];
                Collider2D cloneCollider = clone.Collider;
                if (cloneCollider == null || !cloneCollider.enabled) continue;

                // (a) 닿았을 때만
                if (!liveCollider.Distance(cloneCollider).isOverlapped) continue;

                Bounds liveBounds = liveCollider.bounds;
                Bounds cloneBounds = cloneCollider.bounds;

                // (b) 클론이 위에 있을 때만
                if (cloneBounds.center.y <= liveBounds.center.y) continue;

                // (c) 클론이 내려오는 중일 때만 — 궤적이 확정돼 있으니 다음 틱 위치로 하강 여부를 본다.
                if (!clone.Playback.TryGetFuturePosition(_tick + 1, out Vector2 nextOrigin)) continue;
                if (nextOrigin.y >= clone.transform.position.y) continue;

                // 좌/우 중 짧은 쪽 = 라이브가 이미 치우쳐 있는 쪽 = 최소 이동으로 탈출
                float escapeLeft = liveBounds.max.x - cloneBounds.min.x;
                float escapeRight = cloneBounds.max.x - liveBounds.min.x;

                _crushSlot = slot;
                _crushDirection = escapeLeft <= escapeRight ? -1f : 1f;

                // 깔리기 시작한 높이를 붙잡아 둔다. 여기서부터 아래로는 내려가지 않는다.
                _crushLockY = live.Position.y;

                Push(live, liveBounds, cloneBounds, _crushDirection);
                return;
            }
        }

        /// <summary>
        /// 겹친 상태에서 한 스텝만큼 옆으로 밀어낸다. 그 동안 바닥으로는 파고들지 않게 Y를 붙잡는다.
        /// </summary>
        /// <remarks>
        /// Y를 붙잡는 이유: 클론(Kinematic)이 위에서 누르면 물리 솔버가 라이브(Dynamic)를 바닥 쪽으로
        /// 밀어넣어 지면에 파고든다. 깔린 동안엔 "옆으로만" 빠져나가야 하므로,
        /// 깔리기 시작한 높이(_crushLockY) 아래로는 내려가지 못하게 하고 하강 속도도 지운다.
        /// 위로 올라가는 것은 막지 않는다(클론이 들어올리는 정상 동작).
        /// </remarks>
        private void Push(CharacterActor live, Bounds liveBounds, Bounds cloneBounds, float direction)
        {
            float remaining = direction < 0f
                ? liveBounds.max.x - cloneBounds.min.x
                : cloneBounds.max.x - liveBounds.min.x;

            remaining += _crushSkin;

            Vector2 position = live.Position;

            if (remaining > 0f)
                position.x += direction * Mathf.Min(_crushEjectSpeed * Time.fixedDeltaTime, remaining);

            // 바닥으로 가라앉는 것만 막는다(위로는 자유).
            if (position.y < _crushLockY)
                position.y = _crushLockY;

            live.SetPosition(position);
            live.StopFalling();
        }

        private void Update()
        {
            if (!_isInitialized) return;
            if (_state == RoundState.Cleared) return;

            // New Input System 전용 프로젝트(activeInputHandler=1)라 구 Input.GetKeyDown은 예외를 던진다.
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (_state == RoundState.Selecting)
            {
                if (keyboard.digit1Key.wasPressedThisFrame) SelectCharacter(0);
                else if (keyboard.digit2Key.wasPressedThisFrame) SelectCharacter(1);
                else if (keyboard.digit3Key.wasPressedThisFrame) SelectCharacter(2);
            }
            else if (_state == RoundState.Recording)
            {
                if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
                    ConfirmClone();
                else if (keyboard.rKey.wasPressedThisFrame)
                    RestartTake();
            }

            if (keyboard.backspaceKey.wasPressedThisFrame)
                Rewind();
        }

        /// <summary>캐릭터를 골라 새 테이크를 시작한다. UI 버튼과 1/2/3 키가 공유하는 진입점.</summary>
        public void SelectCharacter(int index)
        {
            if (!_isInitialized) return;
            if (_state != RoundState.Selecting) return;
            if (index < 0 || index >= _characters.Length) return;
            if (IsSlotConfirmed(index)) return;

            // ── 2-패스 강제 ──
            // 1패스에서 전원을 확실히 내린 뒤에야 2패스에서 새 라이브를 올린다.
            // PlayerController가 액션 에셋을 Instantiate해 인스턴스별 사본을 갖게 됐지만,
            // Enable/Disable 순서에 의존하지 않는 구조를 방어적으로 유지한다.
            for (int i = 0; i < _characters.Length; i++)
                _characters[i].SetMode(CharacterMode.Idle);

            for (int i = 0; i < _confirmedSlots.Count; i++)
            {
                CharacterActor clone = _characters[_confirmedSlots[i]];
                clone.SetMode(CharacterMode.Clone);
                clone.Playback.ResetToStart();
            }

            // 2패스: 선택된 캐릭터만 라이브로 올린다.
            CharacterActor live = _characters[index];
            live.SetMode(CharacterMode.Live);
            live.ResetToSpawn();
            live.Recorder.BeginRecording(index);

            // 스폰 순간 라이브와 겹친 클론만 충돌을 일시 해제한다("와르르" 방지). 분리되면 FixedUpdate가 복구.
            Collider2D liveCollider = live.Collider;
            for (int i = 0; i < _confirmedSlots.Count; i++)
            {
                Collider2D cloneCollider = _characters[_confirmedSlots[i]].Collider;
                if (liveCollider == null || cloneCollider == null) continue;

                if (liveCollider.Distance(cloneCollider).isOverlapped)
                {
                    Physics2D.IgnoreCollision(liveCollider, cloneCollider, true);
                    _ignoredSpawnPairs.Add((liveCollider, cloneCollider));
                }
            }

            _liveIndex = index;
            _tick = 0;
            _crushSlot = -1; // 라운드가 바뀌면 이전 깔림 상태는 무효다.

            // 박스 준비: 라이브가 운반형이면 미소유 박스를 Record(Dynamic)로 열고, 소유 박스는 Replay로 건다.
            PrepareBoxesForRound(index);

            _state = RoundState.Recording;
        }

        /// <summary>현재 테이크를 확정해 클론으로 승격시킨다. Enter 또는 시간 상한 도달 시.</summary>
        public void ConfirmClone()
        {
            if (!_isInitialized) return;
            if (_state != RoundState.Recording || _liveIndex < 0) return;

            RestoreAllSpawnOverlaps();

            CharacterActor actor = _characters[_liveIndex];
            CloneRecording recording = actor.Recorder.Recording;
            actor.Playback.SetRecording(recording);

            _confirmedSlots.Add(_liveIndex);

            // 하이어라키 가독성: 확정된 클론은 === CLONES === 아래로 모은다.
            if (_clonesParent != null)
                actor.transform.SetParent(_clonesParent, worldPositionStays: true);

            // 박스 커밋: 라이브가 운반형이면, 이번 라운드에 민 박스(Record 모드)의 궤적을 확정한다.
            // 실제로 움직인 박스만 이 슬롯 소유로 승격하고, 안 움직인 박스는 기록을 버린다.
            if (actor.Identity != null && actor.Identity.CanManipulateObjects)
            {
                for (int i = 0; i < _boxes.Length; i++)
                {
                    PushableBox box = _boxes[i];
                    if (box.Mode != PushableBoxMode.Record) continue;

                    if (box.HasDisplacement(BoxDisplacementEpsilon))
                        box.CommitRecording(_liveIndex);
                    else
                        box.DiscardRecording();
                }
            }

            _liveIndex = -1;
            EnterSelecting();
        }

        /// <summary>현재 테이크만 폐기하고 같은 캐릭터로 다시 녹화한다. 확정 스택은 건드리지 않는다.</summary>
        public void RestartTake()
        {
            if (!_isInitialized) return;
            if (_state != RoundState.Recording || _liveIndex < 0) return;

            // RestartTake는 EnterSelecting을 거치지 않고 곧장 SelectCharacter로 재진입하므로,
            // 여기서 직접 복구하지 않으면 이번 테이크의 겹침 쌍이 남아 다음 등록과 중복된다.
            RestoreAllSpawnOverlaps();

            int index = _liveIndex;

            // SelectCharacter의 Selecting 가드를 통과시키기 위해 먼저 상태를 되돌린다.
            _liveIndex = -1;
            _state = RoundState.Selecting;

            // 죽음→재시작 시에도 기믹을 초기화해야 결정성이 유지된다.
            // RestartTake는 EnterSelecting을 우회하므로(위 주석) 여기서 직접 리셋한다.
            for (int i = 0; i < _gimmicks.Length; i++)
                _gimmicks[i].ResetGimmick();

            SelectCharacter(index);
        }

        /// <summary>확정 스택 최상단 1개만 되돌린다(스택형 되돌리기 — 최근 → 과거 순서만 허용).</summary>
        public void Rewind()
        {
            if (!_isInitialized) return;
            if (_state == RoundState.Cleared) return;

            RestoreAllSpawnOverlaps();

            // 녹화 중이었다면 현재 테이크는 버린다.
            if (_liveIndex >= 0)
            {
                _characters[_liveIndex].SetMode(CharacterMode.Idle);
                _liveIndex = -1;
            }

            if (_confirmedSlots.Count > 0)
            {
                int lastIndex = _confirmedSlots.Count - 1;
                int slot = _confirmedSlots[lastIndex];
                _confirmedSlots.RemoveAt(lastIndex);

                CharacterActor actor = _characters[slot];
                actor.Playback.SetRecording(null);
                actor.SetMode(CharacterMode.Idle);

                if (_originalParents.TryGetValue(slot, out Transform originalParent))
                    actor.transform.SetParent(originalParent, worldPositionStays: true);

                // 되감긴 슬롯이 소유하던 박스는 소유를 풀어 base 상태로 되돌린다(EnterSelecting이 Frozen+Reset 처리).
                for (int i = 0; i < _boxes.Length; i++)
                {
                    if (_boxes[i].OwnerSlot == slot)
                        _boxes[i].ClearOwnership();
                }
            }

            EnterSelecting();
        }

        /// <summary>라이브 캐릭터가 출구에 도달했을 때 LevelExit이 호출한다.</summary>
        public void OnLevelCleared()
        {
            if (_state == RoundState.Cleared) return;

            _state = RoundState.Cleared;
            Debug.Log($"[RoundManager] LEVEL CLEARED — 사용한 클론 {_confirmedSlots.Count}기");
        }

        /// <summary>
        /// 킬존(KillZone)이 라이브의 사망을 통지할 때 호출한다. 녹화 중이면 현재 테이크를 재시작한다.
        /// </summary>
        /// <remarks>
        /// 즉시 RestartTake하지 않고 플래그만 세운다 — 이 메서드는 DriveGimmicks 순회 도중 불리므로,
        /// 여기서 곧장 재시작하면 킬존 인덱스 뒤의 기믹이 같은 FixedUpdate에서 한 번 더 구동돼 위상이 어긋난다.
        /// FixedUpdate가 중앙 틱 진입 전에 이 플래그를 보고 재시작한다.
        /// </remarks>
        public void OnLiveDied()
        {
            if (!_isInitialized) return;
            if (_state != RoundState.Recording) return;

            _isDeathPending = true;
        }

        /// <summary>
        /// 남은 스폰 겹침 쌍의 충돌을 전부 복구하고 리스트를 비운다.
        /// 복구를 빠뜨리면 이후 "클론 밟기"가 영영 안 되므로 상태 전환마다 반드시 호출한다.
        /// </summary>
        private void RestoreAllSpawnOverlaps()
        {
            for (int i = 0; i < _ignoredSpawnPairs.Count; i++)
            {
                (Collider2D live, Collider2D clone) pair = _ignoredSpawnPairs[i];
                if (pair.live != null && pair.clone != null)
                    Physics2D.IgnoreCollision(pair.live, pair.clone, false);
            }

            _ignoredSpawnPairs.Clear();
        }

        /// <summary>보드를 초기 상태로 되돌리고 선택 대기로 진입한다. 확정 클론은 궤적의 첫 프레임으로 복귀.</summary>
        private void EnterSelecting()
        {
            RestoreAllSpawnOverlaps();

            for (int i = 0; i < _characters.Length; i++)
            {
                if (IsSlotConfirmed(i)) continue;

                _characters[i].SetMode(CharacterMode.Idle);
                _characters[i].ResetToSpawn();
            }

            for (int i = 0; i < _confirmedSlots.Count; i++)
            {
                CharacterActor clone = _characters[_confirmedSlots[i]];
                clone.SetMode(CharacterMode.Clone);
                clone.Playback.ResetToStart();
            }

            // 박스도 초기 상태로 되돌린다: 전부 Frozen + base 위치. 소유 박스는 다음 SelectCharacter에서 Replay로 전환.
            for (int i = 0; i < _boxes.Length; i++)
            {
                _boxes[i].SetMode(PushableBoxMode.Frozen);
                _boxes[i].ResetToBase();
            }

            // 기믹도 초기 상태로 되돌린다.
            for (int i = 0; i < _gimmicks.Length; i++)
                _gimmicks[i].ResetGimmick();

            _tick = 0;
            _state = RoundState.Selecting;
        }

        /// <summary>
        /// 중앙 틱에 맞춰 박스를 구동한다. Record 박스는 현재 물리 위치를 기록하고, 소유(Replay) 박스는 궤적을 재생한다.
        /// </summary>
        /// <remarks>클론 재생/라이브 기록과 동일한 _tick을 공유하도록 FixedUpdate의 틱 증가 "이전"에 호출된다.</remarks>
        private void DriveBoxes(int tick)
        {
            for (int i = 0; i < _boxes.Length; i++)
            {
                PushableBox box = _boxes[i];

                if (box.Mode == PushableBoxMode.Record)
                    box.CaptureBoxTick(tick);
                else if (box.OwnerSlot >= 0 && box.Mode == PushableBoxMode.Replay)
                    box.ApplyBoxTick(tick);
            }
        }

        /// <summary>
        /// 중앙 틱에 맞춰 환경 기믹을 구동한다. 박스/클론과 동일한 _tick을 공유하도록 틱 증가 "이전"에 호출된다.
        /// </summary>
        private void DriveGimmicks(int tick)
        {
            for (int i = 0; i < _gimmicks.Length; i++)
                _gimmicks[i].DriveGimmickTick(tick);
        }

        /// <summary>
        /// 새 라운드 시작 시 박스의 구동 모드를 정한다.
        /// </summary>
        /// <remarks>
        /// 소유된 박스는 누가 라이브든 항상 Replay(확정 궤적 재생)다.
        /// 미소유 박스는 라이브가 운반형(CanManipulateObjects)일 때만 Record(Dynamic)로 열려 밀 수 있고,
        /// 운반형이 아니면 Frozen 그대로 둔다 — 순서를 강제하는 핵심 규칙이다.
        /// </remarks>
        private void PrepareBoxesForRound(int index)
        {
            IdentityData identity = _characters[index].Identity;
            bool canManipulate = identity != null && identity.CanManipulateObjects;

            for (int i = 0; i < _boxes.Length; i++)
            {
                PushableBox box = _boxes[i];

                if (box.OwnerSlot >= 0)
                {
                    box.SetMode(PushableBoxMode.Replay);
                }
                else if (canManipulate)
                {
                    box.SetMode(PushableBoxMode.Record);
                    box.BeginRecording(index);
                }
                else
                {
                    box.SetMode(PushableBoxMode.Frozen);
                }
            }
        }
    }
}
