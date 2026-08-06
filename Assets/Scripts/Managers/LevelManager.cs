using System.Collections.Generic;
using AfterYou.Clone;
using AfterYou.Core;
using AfterYou.Ending;
using AfterYou.Level;
using AfterYou.Replay;
using AfterYou.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AfterYou.Managers
{
    /// <summary>
    /// 레벨 = 프리팹 구조의 구동자. 씬 전환 없이 레벨 프리팹을 교체하고,
    /// 레벨 정의(LevelDefinition)의 정체성 수만큼 Player를 생성해 RoundManager에 주입한다.
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        [Tooltip("순서대로 로드될 레벨 프리팹들. 마지막 다음은 처음으로 순환한다.")]
        [SerializeField] private LevelDefinition[] _levels;

        [Tooltip("레벨마다 인원수만큼 Instantiate할 캐릭터 프리팹(Player.prefab).")]
        [SerializeField] private GameObject _playerPrefab;

        [Tooltip("생성된 캐릭터들의 부모(=== PLAYER ===). 비활성 부모 트릭에 쓴다.")]
        [SerializeField] private Transform _charactersParent;

        [Tooltip("레벨 프리팹 인스턴스의 부모(=== LEVEL ===).")]
        [SerializeField] private Transform _levelParent;

        [Tooltip("라운드 진행을 구동할 씬의 RoundManager.")]
        [SerializeField] private RoundManager _roundManager;

        [Tooltip("레벨 전환 페이드 연출. 미할당이면 페이드 없이 즉시 전환한다.")]
        [SerializeField] private ScreenFader _screenFader;

        [Tooltip("마지막 레벨 클리어 후 재생할 엔딩 연출(씬 상주).")]
        [SerializeField] private EndingSequence _endingSequence;

        [Tooltip("게임 시작 타이틀 패널(씬 Canvas 소속). 미할당이면 타이틀 없이 기존처럼 즉시 시작한다.")]
        [SerializeField] private GameObject _titlePanel;

        [Tooltip("클리어 리플레이 감독(씬 상주). 미할당이면 리플레이 없이 기존 클리어 흐름 그대로 동작한다.")]
        [SerializeField] private ReplayDirector _replayDirector;

        /// <summary>현재 로드된 캐릭터들. 다음 레벨 로드 시 이 리스트만으로 전부 파괴한다(클론으로 reparent돼도 커버).</summary>
        private readonly List<CharacterActor> _spawnedActors = new List<CharacterActor>();

        /// <summary>세션 동안 해금된 정체성 풀(해금 순서 유지 — 카드 순서가 전 레벨에서 일관된다).
        /// 레벨의 Identities에 등장하면 그 레벨 로드 시 해금되고, 이후 레벨에서도 계속 선택지에 나온다.</summary>
        private readonly List<IdentityData> _unlockedIdentities = new List<IdentityData>();

        private LevelDefinition _currentLevel;
        private int _currentIndex;

        /// <summary>엔딩 연출 진행 중 여부. 이 동안에는 라운드가 없고 Enter/N이 "처음부터 다시"로 동작한다.</summary>
        private bool _isEnding;

        /// <summary>타이틀 화면 상태 여부. 이 동안에는 레벨이 로드되지 않아 라운드/기믹/타이머가 아예 존재하지 않는다.</summary>
        private bool _isAtTitle;

        private void Start()
        {
            // 타이틀 게이트 — Start 버튼(StartGame)을 누르기 전에는 레벨을 로드하지 않는다.
            if (_titlePanel != null)
            {
                _isAtTitle = true;
                _titlePanel.SetActive(true);
            }
            else
            {
                LoadLevel(0);
            }
        }

        /// <summary>타이틀의 Start 버튼이 호출한다. 패널을 내리고 첫 레벨을 시작한다.</summary>
        public void StartGame()
        {
            if (!_isAtTitle) return;

            _isAtTitle = false;
            _titlePanel.SetActive(false);
            LoadLevel(0);
        }

        /// <summary>어느 상태에서든 게임을 걷어내고 타이틀 화면으로 돌아간다(ESC). 해금 풀까지 비운다.</summary>
        private void ReturnToTitle()
        {
            if (_screenFader != null)
            {
                if (_screenFader.IsFading) return;
                _screenFader.FadeOutThen(() => EnterTitle());
            }
            else
            {
                EnterTitle();
            }
        }

        /// <summary>타이틀 상태로 진입한다. 엔딩 정리 → 레벨/라운드 정리 → 해금 풀 비우기 순서 고정 —
        /// 엔딩 Stop이 먼저여야 봇/무대와 UI 숨김 스냅숏이 걷힌 뒤에 패널이 올라간다.</summary>
        private void EnterTitle()
        {
            if (_isEnding)
            {
                _endingSequence.Stop();
                _isEnding = false;
            }

            CleanupCurrentLevel();
            _unlockedIdentities.Clear();

            _isAtTitle = true;
            _titlePanel.SetActive(true);
        }

        /// <summary>진행 중인 라운드와 현재 레벨/캐릭터를 모두 정리한다. 레벨 교체와 엔딩 진입이 공유한다.</summary>
        private void CleanupCurrentLevel()
        {
            // 0) 리플레이를 먼저 걷어낸다. 이 경로(다음 레벨 / ESC 타이틀 / 엔딩 진입) 전부를 여기서 커버한다.
            //    파괴될 캐릭터/박스/기믹 참조를 붙잡은 채로 틱을 돌리면 다음 프레임에 죽은 참조를 만진다.
            if (_replayDirector != null)
                _replayDirector.Unbind();

            // 1) 진행 중인 라운드를 안전하게 종료한다. 참조(_characters)를 비우기 전에 RoundManager부터 정리해야
            //    전환 순간 LevelExit이 LiveCharacter를 읽어도 NRE가 나지 않는다.
            _roundManager.Teardown();

            // 2) 이전 레벨 정리 — 캐릭터(클론 포함, reparent됐어도 리스트 기반이라 커버)와 레벨 인스턴스를 파괴한다.
            for (int i = 0; i < _spawnedActors.Count; i++)
            {
                if (_spawnedActors[i] != null)
                    Destroy(_spawnedActors[i].gameObject);
            }
            _spawnedActors.Clear();

            if (_currentLevel != null)
                Destroy(_currentLevel.gameObject);
            // 엔딩처럼 새 레벨을 로드하지 않는 경로도 있으므로, 파괴된 참조를 여기서 끊는다.
            _currentLevel = null;
        }

        /// <summary>레벨을 교체한다. 이전 레벨/캐릭터를 정리하고 새 레벨을 로드해 라운드를 구동한다.</summary>
        public void LoadLevel(int index)
        {
            CleanupCurrentLevel();

            // 3) 새 레벨 프리팹 인스턴스화.
            _currentIndex = index;
            _currentLevel = Instantiate(_levels[index], _levelParent);

            IdentityData[] identities = _currentLevel.Identities;
            Vector3 spawnPos = _currentLevel.SpawnPoint != null
                ? _currentLevel.SpawnPoint.position
                : Vector3.zero;

            // 해금: 이 레벨의 Identities를 풀에 추가(중복 제거, 해금 순서 유지).
            for (int i = 0; i < identities.Length; i++)
            {
                if (!_unlockedIdentities.Contains(identities[i]))
                    _unlockedIdentities.Add(identities[i]);
            }

            // 사용 가능 종류 = 해금 풀 − 이 레벨의 금지 목록. 클론 예산은 레벨 인원수 그대로.
            // 같은 종류를 예산 한도 내에서 반복 사용할 수 있어야 하므로,
            // 종류 × 예산만큼 사전 스폰한다 — 정체성 주입은 Awake에서만 가능해 런타임 재주입이 불가하기 때문.
            IdentityData[] banned = _currentLevel.BannedIdentities;
            List<IdentityData> types = new List<IdentityData>();
            for (int i = 0; i < _unlockedIdentities.Count; i++)
            {
                bool isBanned = false;
                for (int b = 0; banned != null && b < banned.Length; b++)
                {
                    if (banned[b] == _unlockedIdentities[i]) { isBanned = true; break; }
                }
                if (!isBanned)
                    types.Add(_unlockedIdentities[i]);
            }
            int cloneBudget = identities.Length;

            // 4) 비활성 부모 트릭 — 부모를 끈 채로 캐릭터를 생성·정체성 주입한 뒤 한 번에 켠다.
            //    이렇게 해야 CharacterActor.Awake(정체성 적용)가 "주입 완료 후"에 실행된다.
            _charactersParent.gameObject.SetActive(false);

            for (int t = 0; t < types.Count; t++)
            {
                for (int b = 0; b < cloneBudget; b++)
                {
                    GameObject go = Instantiate(_playerPrefab, spawnPos, Quaternion.identity, _charactersParent);
                    go.name = $"Character_T{t + 1}_{b + 1}";
                    CharacterActor actor = go.GetComponent<CharacterActor>();
                    actor.InjectIdentity(types[t]);
                    _spawnedActors.Add(actor);
                }
            }

            _charactersParent.gameObject.SetActive(true); // 이 순간 전원의 Awake가 주입된 정체성으로 실행된다.

            // 5) 레벨 출구에 씬의 RoundManager를 주입한다(프리팹은 씬 참조를 직렬화할 수 없다).
            if (_currentLevel.LevelExit != null)
                _currentLevel.LevelExit.BindRoundManager(_roundManager);

            // 5-1) 환경 기믹 수집 + 킬존 주입. (true)로 비활성 상태로 설치된 기믹도 수집한다 — 스위치로 켜질 문 등.
            //      킬존은 LevelExit처럼 씬의 RoundManager를 직렬화 참조할 수 없어 여기서 주입한다.
            ITickGimmick[] gimmicks = _currentLevel.GetComponentsInChildren<ITickGimmick>(true);
            KillZone[] killZones = _currentLevel.GetComponentsInChildren<KillZone>(true);
            for (int i = 0; i < killZones.Length; i++)
                killZones[i].BindRoundManager(_roundManager);

            // 6) 라운드 구동 — 반드시 SetActive(true) 이후여야 한다.
            //    Awake가 끝난 뒤라야 Initialize의 OverrideSpawnPosition이 Awake의 rb.position 캡처를 덮어쓴다.
            _roundManager.Initialize(_spawnedActors.ToArray(), _currentLevel.SpawnPoint, _currentLevel.Boxes, gimmicks,
                types.ToArray(), cloneBudget);

            // 7) 클리어 리플레이 감독에 이번 레벨의 구동 대상을 주입한다.
            //    Initialize 이후여야 한다 — 캐릭터가 이미 초기 모드로 정렬된 뒤에 붙잡아야 상태가 어긋나지 않는다.
            if (_replayDirector != null)
                _replayDirector.Bind(_roundManager, _spawnedActors.ToArray(), _currentLevel.Boxes, gimmicks,
                    _currentLevel.LevelExit);
        }

        /// <summary>클리어 상태에서 다음 레벨로 넘어간다. N키와 Next 버튼(CharacterSelectUI)이 공용으로 호출한다.</summary>
        public void LoadNextLevel()
        {
            if (_roundManager.State != RoundState.Cleared) return;

            // 마지막 레벨을 클리어하면 처음으로 순환하지 않고 엔딩 연출로 넘어간다.
            bool isLastLevel = _currentIndex == _levels.Length - 1;
            int nextIndex = (_currentIndex + 1) % _levels.Length;

            if (_screenFader != null)
            {
                // 페이드 중 재호출은 ScreenFader.IsFading이 가드한다(N키 연타 등).
                if (_screenFader.IsFading) return;
                if (isLastLevel)
                    _screenFader.FadeOutThen(() => StartEnding());
                else
                    _screenFader.FadeOutThen(() => LoadLevel(nextIndex));
            }
            else
            {
                // ⚠ 페이더 미할당 폴백: 전환이 같은 프레임 안에서 끝나므로 이 프레임의 Enter 입력이
                //   엔딩 재시작 입력으로 새어 들어갈 위험이 있다(동일 프레임 Enter 누수 위험).
                if (isLastLevel)
                    StartEnding();
                else
                    LoadLevel(nextIndex);
            }
        }

        /// <summary>
        /// 엔딩 연출로 진입한다. 무대를 완전히 비운 뒤 EndingSequence에 넘긴다 —
        /// 정리를 먼저 하지 않으면 마지막 레벨 지형·캐릭터가 엔딩 배경 위에 남는다.
        /// </summary>
        private void StartEnding()
        {
            CleanupCurrentLevel();
            _isEnding = true;
            _endingSequence.Begin(_unlockedIdentities.ToArray(), _levelParent);
        }

        /// <summary>엔딩에서 첫 레벨로 재시작한다. 해금 풀까지 비워 완전한 새 세션으로 되돌린다.</summary>
        private void RestartFromEnding()
        {
            if (_screenFader != null)
            {
                if (_screenFader.IsFading) return;
                // 순서 고정: Stop → _isEnding 해제 → 해금 풀 비우기 → LoadLevel(0).
                // Stop이 반드시 먼저여야 엔딩 봇/무대가 걷힌 뒤에 새 레벨이 올라간다.
                _screenFader.FadeOutThen(() =>
                {
                    _endingSequence.Stop();
                    _isEnding = false;
                    _unlockedIdentities.Clear();
                    LoadLevel(0);
                });
            }
            else
            {
                // ⚠ 페이더 미할당 폴백: 같은 프레임에 재시작이 끝나므로 이 프레임의 Enter 입력이
                //   새 레벨의 입력으로 새어 들어갈 위험이 있다(동일 프레임 Enter 누수 위험).
                _endingSequence.Stop();
                _isEnding = false;
                _unlockedIdentities.Clear();
                LoadLevel(0);
            }
        }

        private void Update()
        {
            // 타이틀 상태 — 게임 입력(디버그 키 포함)을 전부 봉인한다. 시작은 StartGame(버튼)만.
            if (_isAtTitle) return;

            // ESC — 어느 상태에서든(플레이/녹화/클리어/엔딩) 타이틀로 복귀. 패널이 연결된 경우에만 동작한다.
            Keyboard escapeKeyboard = Keyboard.current;
            if (_titlePanel != null && escapeKeyboard != null && escapeKeyboard.escapeKey.wasPressedThisFrame)
            {
                ReturnToTitle();
                return;
            }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            // 디버그 전용 강제 스테이지 이동(임시 — 테스트 끝나면 제거 예정). 클리어 여부 무시.
            Keyboard debugKeyboard = Keyboard.current;
            if (debugKeyboard != null)
            {
                if (debugKeyboard.pageDownKey.wasPressedThisFrame)
                {
                    // 엔딩 중 강제 이동이면 엔딩 무대를 먼저 걷어낸다 — 안 그러면 봇/배경이 새 레벨 위에 남는다.
                    if (_isEnding) { _endingSequence.Stop(); _isEnding = false; }
                    LoadLevel((_currentIndex + 1) % _levels.Length);
                    return;
                }
                if (debugKeyboard.pageUpKey.wasPressedThisFrame)
                {
                    if (_isEnding) { _endingSequence.Stop(); _isEnding = false; }
                    LoadLevel((_currentIndex - 1 + _levels.Length) % _levels.Length);
                    return;
                }
            }
#endif

            // 엔딩 중에는 라운드가 Teardown된 상태라 Cleared 가드에 걸리므로, 그보다 먼저 처리한다.
            if (_isEnding)
            {
                Keyboard endingKeyboard = Keyboard.current;
                if (endingKeyboard != null && (endingKeyboard.nKey.wasPressedThisFrame
                    || endingKeyboard.enterKey.wasPressedThisFrame || endingKeyboard.numpadEnterKey.wasPressedThisFrame))
                    RestartFromEnding();
                return;
            }

            if (_roundManager.State != RoundState.Cleared) return;

            // 리플레이 중에도 Enter/N은 그대로 받는다 — 같은 프레임에 리플레이 스킵(ReplayDirector)과
            // 다음 레벨 로드가 함께 발동해 곧장 다음 스테이지로 넘어간다(사용자 확정 2026-08-06).

            // New Input System 전용 프로젝트라 구 Input.GetKeyDown은 예외를 던진다.
            Keyboard keyboard = Keyboard.current;
            // Enter는 Recording 상태의 클론 확정(RoundManager)에만 쓰여 Cleared 상태에선 충돌이 없다.
            if (keyboard != null && (keyboard.nKey.wasPressedThisFrame
                || keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame))
                LoadNextLevel();
        }
    }
}
