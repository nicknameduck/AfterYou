using AfterYou.Clone;
using UnityEngine;

namespace AfterYou.Player
{
    /// <summary>
    /// 라이브(조작 중) 캐릭터 뒤로 몸 전체 크기 스탬프를 조밀하게 찍어 연속 트레일을 만든다.
    /// 리본(TrailRenderer) 방식은 방향 전환·점프에서 띠가 접혀 주름이 생기고, 몸이 반 캐릭터 이상
    /// 이동해야 보이기 시작하는 한계가 있어 스탬프 방식으로 재작성했다. 겹침 이중 누적은
    /// TrailStamp 셰이더의 스텐실 선점(픽셀당 최신 스탬프 1장)이 차단한다.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class CharacterTrail : MonoBehaviour
    {
        [Tooltip("스탬프 하나가 완전히 사라지기까지 걸리는 시간(초). 꼬리 길이 = 이동 속도 × 이 값 (속도 6 기준 0.5s ≈ 캐릭터 3개 분량).")]
        [SerializeField] private float _trailTime = 0.5f;

        [Tooltip("머리(본체 쪽) 시작 알파. 꼬리로 갈수록 선형으로 옅어진다.")]
        [Range(0f, 1f)]
        [SerializeField] private float _startAlpha = 0.75f;

        [Tooltip("이 거리(유닛)마다 스탬프 1장. 작을수록 매끈하지만 풀 소모가 커진다.")]
        [SerializeField] private float _stampSpacing = 0.08f;

        [Tooltip("스탬프 풀 크기. 최고 이동 속도 × trailTime ÷ spacing 보다 크면 재사용 끊김이 없다 (7×0.5÷0.08≈44).")]
        [SerializeField] private int _poolSize = 48;

        [Tooltip("한 프레임에 이 거리 이상 이동하면 순간이동(포탈/되감기)으로 보고 트레일을 잇지 않는다.")]
        [SerializeField] private float _teleportThreshold = 2f;

        [Tooltip("스탬프 셰이더(AfterYou/TrailStamp). 에셋 참조여야 빌드에 포함된다 — Shader.Find는 참조 없는 셰이더가 빌드에서 스트리핑되어 null을 반환한다(실측).")]
        [SerializeField] private Shader _stampShader;

        // 스탬프 정렬 순서 상한. 본체(0)보다 뒤, 배경(-100)보다 앞 범위(-10 ~ -10-풀 크기)에 머문다.
        // 최신 스탬프일수록 낮은 순서 = 먼저 그려져 스텐실을 선점한다.
        private const int OrderMax = -10;

        private CharacterActor _actor;
        private SpriteRenderer _sourceRenderer;
        private Transform _stampRoot;
        private Material _stampMaterial;
        private TrailStamp[] _stamps;
        private int _nextStamp;
        private Vector3 _lastPosition;      // 순간이동 감지용(직전 프레임 위치)
        private Vector3 _lastStampPosition; // 스탬프 간격 판정용
        private bool _wasLive;

        private void Awake()
        {
            _actor = GetComponent<CharacterActor>();
            _sourceRenderer = GetComponent<SpriteRenderer>();

            // 본체가 꺼져도 남은 스탬프는 스스로 소멸해야 하므로 캐릭터 밖(씬 루트)에 둔다.
            _stampRoot = new GameObject($"{name}_Trail").transform;
            _stampMaterial = new Material(_stampShader);

            _stamps = new TrailStamp[_poolSize];
            for (int i = 0; i < _poolSize; i++)
            {
                var stampObject = new GameObject($"Stamp_{i}");
                stampObject.transform.SetParent(_stampRoot, false);
                var stampRenderer = stampObject.AddComponent<SpriteRenderer>();
                stampRenderer.sharedMaterial = _stampMaterial;
                _stamps[i] = stampObject.AddComponent<TrailStamp>();
                stampObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            // 캐릭터는 레벨 전환 시 파괴되므로, 씬 루트에 만든 스탬프 풀도 같이 치운다.
            if (_stampRoot != null)
                Destroy(_stampRoot.gameObject);
            if (_stampMaterial != null)
                Destroy(_stampMaterial);
        }

        private void LateUpdate()
        {
            // CharacterActor가 없는 오브젝트(엔딩 봇 등)는 라이브/클론 개념이 없으므로 항상 트레일을 남긴다.
            if (_actor != null && _actor.Mode != CharacterMode.Live)
            {
                _wasLive = false;
                return;
            }

            // 시각 연출이므로 보간된 transform.position을 쓴다(Rigidbody2D.position 규약은 게임플레이 로직용).
            Vector3 position = transform.position;

            // 라이브 진입 첫 프레임 / 순간이동(포탈·되감기) 직후에는 잇지 않고 기준점만 갱신한다.
            bool discontinuous = !_wasLive
                || (position - _lastPosition).sqrMagnitude > _teleportThreshold * _teleportThreshold;
            _wasLive = true;
            _lastPosition = position;

            if (discontinuous)
            {
                _lastStampPosition = position;
                return;
            }

            // 마지막 스탬프에서 spacing 이상 벌어진 만큼 경로를 따라 채운다.
            // 프레임 시간이 튀어도(저프레임) 스탬프 간격이 일정해 트레일 밀도가 흔들리지 않는다.
            bool spawned = false;
            while ((position - _lastStampPosition).magnitude >= _stampSpacing)
            {
                _lastStampPosition = Vector3.MoveTowards(_lastStampPosition, position, _stampSpacing);
                SpawnStamp(_lastStampPosition);
                spawned = true;
            }

            if (spawned)
                ReassignSortingOrders();
        }

        private void SpawnStamp(Vector3 position)
        {
            TrailStamp stamp = _stamps[_nextStamp];
            _nextStamp = (_nextStamp + 1) % _stamps.Length;

            Color color = _sourceRenderer.color;
            color.a = _startAlpha;

            stamp.Show(_sourceRenderer, position, transform.lossyScale, color, _trailTime);
        }

        /// <summary>
        /// 풀은 스폰 순서대로 순환하므로 활성 스탬프는 항상 "최근 창"을 이룬다. 그 순서를 그대로
        /// 이용해 최신 → 낮은 정렬 순서를 재부여한다. 고정 순환 카운터는 한 바퀴를 돌 때
        /// 죽어가는 옛 스탬프가 새 스탬프보다 먼저 그려져(스텐실 선점) 트레일에 빈 틈을 만들었다.
        /// </summary>
        private void ReassignSortingOrders()
        {
            // 낮은 정렬 순서 = 먼저 그려짐 = 스텐실 선점. 따라서 최신 스탬프가 가장 낮은 값을 받아야
            // 겹침에서 항상 최신 알파가 보인다(오래된 순서로 먼저 그리면 죽어가는 스탬프가 픽셀을 가림).
            int active = 0;
            for (int k = 0; k < _stamps.Length; k++)
                if (_stamps[k].gameObject.activeSelf)
                    active++;

            int rank = 0; // 0 = 최신
            for (int k = 0; k < _stamps.Length; k++)
            {
                int index = (_nextStamp - 1 - k + _stamps.Length * 2) % _stamps.Length;
                TrailStamp stamp = _stamps[index];
                if (!stamp.gameObject.activeSelf)
                    continue;
                stamp.SetSortingOrder(OrderMax - (active - 1) + rank++);
            }
        }
    }

    /// <summary>
    /// 트레일 스탬프 1장. 스스로 선형 페이드아웃 후 비활성화되므로 본체가 먼저 꺼져도 정상 소멸한다.
    /// </summary>
    public class TrailStamp : MonoBehaviour
    {
        private SpriteRenderer _renderer;
        private Color _startColor;
        private float _lifetime;
        private float _age;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        public void Show(SpriteRenderer source, Vector3 position, Vector3 scale, Color color, float lifetime)
        {
            transform.position = position;
            transform.localScale = scale; // 스쿼시/스트레치가 트레일에도 남는다

            _renderer.sprite = source.sprite;
            _renderer.flipX = source.flipX;
            _renderer.flipY = source.flipY;
            _renderer.sortingLayerID = source.sortingLayerID;
            _renderer.color = color;

            _startColor = color;
            _lifetime = lifetime;
            _age = 0f;
            gameObject.SetActive(true);
        }

        public void SetSortingOrder(int sortingOrder)
        {
            _renderer.sortingOrder = sortingOrder;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_age >= _lifetime)
            {
                gameObject.SetActive(false);
                return;
            }

            // 선형 감쇠: 트레일 전체 길이가 끝까지 고르게 읽히도록 한다(제곱 커브는 체감 길이 절반).
            Color color = _startColor;
            color.a = _startColor.a * (1f - _age / _lifetime);
            _renderer.color = color;
        }
    }
}
