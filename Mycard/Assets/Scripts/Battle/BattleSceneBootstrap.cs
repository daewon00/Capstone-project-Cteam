using UnityEngine;

// 씬 조립자: 컨트롤러보다 먼저 실행되어 서비스 주입을 담당합니다.
[DefaultExecutionOrder(-9000)]
public class BattleSceneBootstrap : MonoBehaviour
{
    [Header("연결할 컨트롤러")]
    [SerializeField] private BattleController _battleController;
    [SerializeField] private HandController _handController;
    [SerializeField] private Card _cardPrefab; // 핸드에 생성할 카드 프리팹(권장: 명시 지정)

    void Awake()
    {
        // 유효성 검사: 필수 컨트롤러 레퍼런스 확인
        if (_battleController == null || _handController == null)
        {
            Debug.LogError("[BattleSceneBootstrap] 필수 컨트롤러가 인스펙터에 연결되지 않았습니다! 초기화를 중단합니다.", this);
            this.enabled = false;
            return;
        }

        var deckService = ServiceRegistry.GetRequired<IDeckService>();
        var cardCatalog = ServiceRegistry.GetRequired<ICardCatalog>();

        if (deckService == null) Debug.LogWarning("[BattleSceneBootstrap] IDeckService를 찾지 못했습니다.");
        if (cardCatalog == null) Debug.LogWarning("[BattleSceneBootstrap] ICardCatalog를 찾지 못했습니다.");
        if (deckService != null) GameServices.RegisterDeck(deckService);
        // HandServiceBinder 부착 및 초기화
        var binder = _handController.GetComponent<HandServiceBinder>();
        if (binder == null) binder = _handController.gameObject.AddComponent<HandServiceBinder>();
        // 카드 프리팹이 있다면 바인더에 설정
        if (_cardPrefab != null)
        {
            var field = typeof(HandServiceBinder).GetField("_cardPrefab", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(binder, _cardPrefab);
        }
        binder.Initialize(_handController, deckService, cardCatalog);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[BattleSceneBootstrap] HandServiceBinder initialized. hand={_handController!=null}, deckSvc={deckService!=null}, catalog={cardCatalog!=null}, cardPrefab={_cardPrefab!=null}");
#endif

        if (_battleController != null && deckService != null)
        {
            _battleController.Initialize(deckService);
            // Start에서 전투 개시(모든 Awake 완료 후)
        }
        else
        {
            if (_battleController == null) Debug.LogWarning("[BattleSceneBootstrap] BattleController가 연결되지 않았습니다.");
        }

    }

    void Start()
    {
        if (this.enabled && _battleController != null)
        {
            _battleController.StartBattle();
        }
    }
}
