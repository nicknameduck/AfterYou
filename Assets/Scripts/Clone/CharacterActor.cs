using AfterYou.Player;
using UnityEngine;

namespace AfterYou.Clone
{
    public enum CharacterMode
    {
        /// <summary>대기: 비활성. 아직 선택되지 않았거나 되감기로 되돌아온 상태.</summary>
        Idle,

        /// <summary>라이브: 플레이어가 직접 조작 + 녹화 중. 유일하게 Dynamic 바디.</summary>
        Live,

        /// <summary>클론: 확정된 궤적을 재생 중. 라이브가 밟고 올라설 수 있어야 한다.</summary>
        Clone
    }

    /// <summary>
    /// 캐릭터 1기의 모드 전환을 캡슐화한다.
    /// bodyType / layer / 컴포넌트 on-off / 활성화를 한 곳에서만 만져 상태 불일치를 막는다.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class CharacterActor : MonoBehaviour
    {
        /// <summary>클론(과거의 나)의 스프라이트 알파. 라이브와 한눈에 구분되어야 퍼즐이 읽힌다.</summary>
        private const float CloneAlpha = 0.5f;

        private Rigidbody2D _rigidbody;
        private Collider2D _collider;
        private PlayerController _playerController;
        private CharacterRecorder _recorder;
        private ClonePlayback _playback;
        private SpriteRenderer _spriteRenderer;

        private Vector2 _spawnPosition;
        private int _defaultLayer;
        private int _groundLayer;

        public CharacterRecorder Recorder => _recorder;
        public ClonePlayback Playback => _playback;
        public Collider2D Collider => _collider;
        public CharacterMode Mode { get; private set; } = CharacterMode.Idle;
        public Vector2 SpawnPosition => _spawnPosition;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider2D>();
            _playerController = GetComponent<PlayerController>();
            _recorder = GetComponent<CharacterRecorder>();
            _playback = GetComponent<ClonePlayback>();
            _spriteRenderer = GetComponent<SpriteRenderer>();

            // 씬에 배치된 초기 위치가 이 캐릭터의 스폰 지점이다.
            _spawnPosition = _rigidbody.position;

            _defaultLayer = LayerMask.NameToLayer("Default");
            _groundLayer = LayerMask.NameToLayer("Ground");
        }

        /// <summary>
        /// 모드 전환. 순서가 곧 안전장치다 — 아래 주석의 1~5 순서를 바꾸지 말 것.
        /// </summary>
        public void SetMode(CharacterMode mode)
        {
            Mode = mode;
            bool shouldBeActive = mode != CharacterMode.Idle;

            // 비활성 오브젝트의 Rigidbody2D는 시뮬레이션에서 빠져 있어 속도/bodyType 조작이 먹지 않는다.
            // 활성이 필요한 모드는 물리 상태를 만지기 전에 먼저 켠다.
            if (shouldBeActive && !gameObject.activeSelf)
                gameObject.SetActive(true);

            // 1) 잔류 속도 제거 — bodyType을 바꾸기 "전"에.
            //    Kinematic 바디도 linearVelocity가 남아 있으면 계속 관성 이동해 MovePosition과 싸우며 지터를 만든다.
            //    Kinematic → Dynamic 복귀에도 동일하게 필요하다(안 하면 리셋 직후 순간이동).
            _rigidbody.linearVelocity = Vector2.zero;
            _rigidbody.angularVelocity = 0f;

            // 2) bodyType — 라이브만 물리에 반응하는 Dynamic. 클론/대기는 궤적이 곧 권위이므로 Kinematic.
            _rigidbody.bodyType = mode == CharacterMode.Live
                ? RigidbodyType2D.Dynamic
                : RigidbodyType2D.Kinematic;

            // 3) layer — 클론만 Ground. PlayerController의 접지 체크가 _groundLayer(Ground 단독)만 보므로,
            //    클론이 Ground 레이어여야 그 위에서 _isGrounded가 서고 점프가 된다("클론 밟고 올라서기").
            //    ⚠ 라이브를 Ground로 두면 안 된다: QueriesStartInColliders=1이라 GroundCheck 박스가
            //    자기 자신의 콜라이더를 잡아 공중에서도 무한 점프가 된다. 반드시 Default로 되돌린다.
            gameObject.layer = mode == CharacterMode.Clone ? _groundLayer : _defaultLayer;

            // 4) 컴포넌트 on/off — 라이브만 조작, 클론만 재생.
            _playerController.enabled = mode == CharacterMode.Live;
            _playback.enabled = mode == CharacterMode.Clone;

            // 라이브는 스프라이트를 뒤집지 않는다(PlayerController에 flip 로직 없음).
            // 클론으로 쓰이며 뒤집힌 채 남은 flipX를 여기서 정리한다.
            if (mode == CharacterMode.Live)
                _spriteRenderer.flipX = false;

            // 클론은 반투명 — "지금 조작 중인 나"와 "재생 중인 과거의 나"를 시각적으로 분리한다.
            Color color = _spriteRenderer.color;
            color.a = mode == CharacterMode.Clone ? CloneAlpha : 1f;
            _spriteRenderer.color = color;

            // 5) 활성화 — 대기는 씬에서 감춘다.
            if (!shouldBeActive)
                gameObject.SetActive(false);
        }

        /// <summary>현재 물리 위치(Rigidbody2D 기준. transform.position은 보간값이라 권위가 아니다).</summary>
        public Vector2 Position => _rigidbody.position;

        /// <summary>
        /// 물리 위치를 직접 옮긴다(압사 해소용).
        /// </summary>
        /// <remarks>
        /// 속도가 아니라 위치를 직접 옮기는 이유: PlayerController.FixedUpdate가 매 스텝
        /// linearVelocity.x를 통째로 덮어쓰므로, 속도로 밀어내면 실행 순서에 따라 지워진다.
        /// </remarks>
        public void SetPosition(Vector2 position)
        {
            _rigidbody.position = position;

            // AutoSyncTransforms가 꺼져 있어 Rigidbody2D.position만 바꾸면 렌더 위치가 한 스텝 어긋난다.
            transform.position = new Vector3(position.x, position.y, transform.position.z);
        }

        /// <summary>깔려 있는 동안 아래로 밀려 내려가지 않게 수직 속도의 하강 성분만 지운다.</summary>
        public void StopFalling()
        {
            Vector2 velocity = _rigidbody.linearVelocity;
            if (velocity.y < 0f)
            {
                velocity.y = 0f;
                _rigidbody.linearVelocity = velocity;
            }
        }

        /// <summary>스폰 지점으로 순간 복귀. 라운드 리셋/되감기에 쓰인다.</summary>
        public void ResetToSpawn()
        {
            _rigidbody.linearVelocity = Vector2.zero;
            _rigidbody.angularVelocity = 0f;
            _rigidbody.position = _spawnPosition;

            // AutoSyncTransforms가 꺼져 있어 Rigidbody2D.position만 바꾸면 다음 물리 스텝까지 렌더 위치가 어긋난다.
            transform.position = new Vector3(_spawnPosition.x, _spawnPosition.y, transform.position.z);
        }
    }
}
