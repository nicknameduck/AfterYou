using AfterYou.Clone;
using UnityEngine;

namespace AfterYou.Player
{
    /// <summary>
    /// 라이브(조작 중) 캐릭터 뒤로 이동 잔상을 남긴다. 클론/대기 모드에서는 아무것도 하지 않는다.
    /// </summary>
    [RequireComponent(typeof(CharacterActor), typeof(SpriteRenderer))]
    public class AfterimageTrail : MonoBehaviour
    {
        [Tooltip("잔상 생성 간격(초)")]
        [SerializeField] private float _spawnInterval = 0.06f;

        [Tooltip("잔상 하나가 사라지기까지 걸리는 시간(초). 클론(상시 α0.85)과 혼동되지 않게 짧게 유지.")]
        [SerializeField] private float _ghostLifetime = 0.42f;

        [Tooltip("잔상 시작 알파. 본체 바로 뒤 잔상이 이 농도로 시작해 꼬리로 갈수록 제곱 커브로 옅어진다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _startAlpha = 0.75f;

        [Tooltip("잔상 풀 크기. ghostLifetime / spawnInterval 보다 크면 재사용 끊김이 없다.")]
        [SerializeField] private int _poolSize = 8;

        [Tooltip("한 프레임에 이 거리 이상 이동하면 순간이동(포탈/되감기)으로 보고 잔상을 잇지 않는다.")]
        [SerializeField] private float _teleportThreshold = 2f;

        [Tooltip("마지막 잔상 위치에서 이만큼 이상 움직였을 때만 새 잔상을 남긴다(정지 중 스폰 방지).")]
        [SerializeField] private float _minSpawnDistance = 0.08f;

        private CharacterActor _actor;
        private SpriteRenderer _sourceRenderer;
        private Transform _ghostRoot;
        private AfterimageGhost[] _ghosts;
        private int _nextGhost;
        private float _spawnTimer;
        private Vector3 _lastPosition;      // 순간이동 감지용(직전 프레임 위치)
        private Vector3 _lastSpawnPosition; // 최소 이동 거리 판정용
        private bool _wasLive;

        private void Awake()
        {
            _actor = GetComponent<CharacterActor>();
            _sourceRenderer = GetComponent<SpriteRenderer>();

            // 잔상은 캐릭터를 따라오면 안 되므로 캐릭터 밖(씬 루트)에 둔다.
            _ghostRoot = new GameObject($"{name}_Afterimages").transform;

            _ghosts = new AfterimageGhost[_poolSize];
            for (int i = 0; i < _poolSize; i++)
            {
                var ghostObject = new GameObject($"Ghost_{i}");
                ghostObject.transform.SetParent(_ghostRoot, false);
                ghostObject.AddComponent<SpriteRenderer>();
                _ghosts[i] = ghostObject.AddComponent<AfterimageGhost>();
                ghostObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            // 캐릭터는 레벨 전환 시 파괴되므로, 씬 루트에 만든 잔상 풀도 같이 치운다.
            if (_ghostRoot != null)
                Destroy(_ghostRoot.gameObject);
        }

        private void LateUpdate()
        {
            if (_actor.Mode != CharacterMode.Live)
            {
                _wasLive = false;
                return;
            }

            // 시각 연출이므로 보간된 transform.position을 쓴다(Rigidbody2D.position 규약은 게임플레이 로직용).
            Vector3 position = transform.position;

            // 라이브 진입 첫 프레임 / 순간이동(포탈·되감기) 직후에는 잔상을 잇지 않고 기준점만 갱신한다.
            bool discontinuous = !_wasLive
                || (position - _lastPosition).sqrMagnitude > _teleportThreshold * _teleportThreshold;
            _wasLive = true;
            _lastPosition = position;

            if (discontinuous)
            {
                _lastSpawnPosition = position;
                _spawnTimer = 0f;
                return;
            }

            _spawnTimer += Time.deltaTime;
            if (_spawnTimer < _spawnInterval)
                return;

            if ((position - _lastSpawnPosition).sqrMagnitude < _minSpawnDistance * _minSpawnDistance)
                return;

            _spawnTimer = 0f;
            _lastSpawnPosition = position;
            SpawnGhost();
        }

        private void SpawnGhost()
        {
            AfterimageGhost ghost = _ghosts[_nextGhost];
            _nextGhost = (_nextGhost + 1) % _ghosts.Length;

            Color color = _sourceRenderer.color;
            color.a = _startAlpha;
            ghost.Show(_sourceRenderer, transform, color, _ghostLifetime);
        }
    }

    /// <summary>
    /// 잔상 1장. 스스로 페이드아웃 후 비활성화되므로 본체(라이브 캐릭터)가 먼저 꺼져도 잔상은 정상 소멸한다.
    /// </summary>
    public class AfterimageGhost : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private Color _startColor;
        private float _lifetime;
        private float _age;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        public void Show(SpriteRenderer source, Transform sourceTransform, Color color, float lifetime)
        {
            transform.position = sourceTransform.position;
            transform.rotation = sourceTransform.rotation;
            transform.localScale = sourceTransform.lossyScale; // 스쿼시/스트레치가 잔상에도 남는다

            _renderer.sprite = source.sprite;
            _renderer.flipX = source.flipX;
            _renderer.flipY = source.flipY;
            _renderer.sharedMaterial = source.sharedMaterial;
            _renderer.sortingLayerID = source.sortingLayerID;
            _renderer.sortingOrder = source.sortingOrder - 1; // 본체 뒤에 깔린다
            _renderer.color = color;

            _startColor = color;
            _lifetime = lifetime;
            _age = 0f;
            gameObject.SetActive(true);
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age >= _lifetime)
            {
                gameObject.SetActive(false);
                return;
            }

            // 제곱 감쇠: 초반(본체 근처)은 진하게 유지되고 꼬리로 갈수록 급격히 옅어진다.
            float remain = 1f - _age / _lifetime;
            Color color = _startColor;
            color.a = _startColor.a * remain * remain;
            _renderer.color = color;
        }
    }
}
