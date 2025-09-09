using UnityEngine;
using Game.Save;

/// <summary>
/// 게임이 시작될 때, DatabaseManager와 같은 핵심 시스템을 깨우고 초기화하는 역할을 합니다.
/// </summary>
[DefaultExecutionOrder(-10000)] // 이 스크립트가 가장 먼저 실행되도록 보장합니다.
public class GameInitializer : MonoBehaviour
{
    [SerializeField] private string[] requiredRngDomains = { "deck-shuffle", "reward-generation" };

    private IDatabase _db;
    private IRngService _rng;
    private static bool _bootstrapped;
    
    void Awake()
    {
        
        if (_bootstrapped) { Destroy(gameObject); return; } // ← 중복 방지 가드
        _bootstrapped = true;
        DontDestroyOnLoad(gameObject); // (선택) 씬이 바뀌어도 조립 담당자가 사라지지 않게 함

        // 새 게임을 시작하거나 씬을 다시 로드할 때를 대비해, 보관소를 항상 깨끗하게 비웁니다.
        ServiceRegistry.ClearAll();

        // 1. [기반 시스템 준비] 데이터베이스에 먼저 연결합니다.
        DatabaseManager.Instance.Connect();

        // 1.5. 카드 카탈로그 서비스 등록 (Resources/Cards)
        var cardCatalog = new CardCatalog("Cards");
        if (cardCatalog.Count == 0)
        {
            Debug.LogWarning("[GameInitializer] CardCatalog가 비어있습니다. Resources/Cards 경로 또는 에셋 구성을 확인하세요.");
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[GameInitializer] CardCatalog load complete. count={cardCatalog.Count}");
#endif
        ServiceRegistry.Register<ICardCatalog>(cardCatalog);

        // 2. [부품 생성] '가벽' 역할을 할 DatabaseFacade를 생성합니다.
        var dbFacade = new DatabaseFacade();
        //    '보관소'에 IDatabase라는 이름으로 등록하여, 다른 전문가들이 찾아 쓸 수 있게 합니다.
        ServiceRegistry.Register<IDatabase>(dbFacade);
        _db = dbFacade;

        // 3. 러닝 컨텍스트(runId) 확보
        // 런 ID가 DB에 실제로 존재하는지 sanity 체크까지 수행합니다.
        var runId = PlayerPrefs.GetString("lastRunId", "");
        if (!string.IsNullOrEmpty(runId))
        {
            try
            {
                var sanity = DatabaseManager.Instance.LoadCurrentRun(runId);
                if (sanity == null || sanity.Run == null)
                {
                    Debug.LogWarning($"[GameInitializer] lastRunId에 해당하는 CurrentRun이 없어 초기화합니다: {runId}");
                    PlayerPrefs.DeleteKey("lastRunId");
                    PlayerPrefs.Save();
                    runId = string.Empty;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[GameInitializer] lastRunId 확인 중 오류: {e.Message}");
            }
        }

        // 4. RNG 서비스 등록: 기존 상태를 불러오고, 필수 도메인 시드가 없으면 RunId 기반으로 보정
        var loadedRngStates = string.IsNullOrEmpty(runId) ? null : _db.LoadRngStates(runId);
        _rng = new RngService(loadedRngStates);
        if (!string.IsNullOrEmpty(runId))
        {
            foreach (var domain in requiredRngDomains)
            {
                TryEnsureSeeded(_rng, domain, runId);
            }
        }
        ServiceRegistry.Register<IRngService>(_rng);

        // 5. 월렛(지갑) 서비스 등록: DB-우선 골드 관리 + 브로드캐스트
        var wallet = new WalletService(dbFacade, runId);
        ServiceRegistry.Register<IWalletService>(wallet);

        // 6. '이어하기'든 '새 게임'이든 상관없이 항상 EventManager를 등록합니다.
        var eventManager = new EventManager(dbFacade, runId);
        ServiceRegistry.Register<IEventManager>(eventManager);

        // 7. 덱 서비스 등록 + 현재 런 덱 준비(백필/초기 셔플 포함 가능)
        var rngService = ServiceRegistry.Get<IRngService>();
        var deckService = new DeckService(dbFacade, rngService);
        deckService.LoadAndPrepareDeck(runId);
        ServiceRegistry.Register<IDeckService>(deckService);

        // 7.5. 런 서비스 등록: 전투 결과 커밋/라우팅 담당 (카탈로그 주입)
        var runService = new RunService(dbFacade, _rng, cardCatalog);
        ServiceRegistry.Register<IRunService>(runService);
        if (!string.IsNullOrEmpty(runId))
        {
            runService.RebindRun(runId);
        }

        // 8. 초기화 과정 중 변경되었을 수 있는 RNG 상태를 한 번 더 저장하여 정합성 보장
        if (!string.IsNullOrEmpty(runId) && rngService != null)
        {
            _db.UpsertRngStates(runId, rngService.GetStatesForSave());
        }

        Debug.Log("GameInitializer: 모든 시스템 조립 및 등록이 완료되었습니다.");
    }

    private static void TryEnsureSeeded(IRngService rng, string domain, string runId)
    {
        try
        {
            // 이미 시드된 경우라면 호출이 성공하며, 아닌 경우 예외가 발생함
            rng.NextUInt(domain);
        }
        catch
        {
            rng.Seed(domain, HashRunIdToSeed(runId, domain));
        }
    }

    private static uint HashRunIdToSeed(string runId, string domain)
    {
        unchecked
        {
            uint h = 2166136261u; // FNV-1a basis
            if (!string.IsNullOrEmpty(runId))
            {
                foreach (char c in runId) { h ^= c; h *= 16777619u; }
            }
            if (!string.IsNullOrEmpty(domain))
            {
                foreach (char c in domain) { h ^= c; h *= 16777619u; }
            }
            if (h == 0u) h = 1u; // Unity.Mathematics.Random은 0 시드 금지
            return h;
        }
    }

    private void OnApplicationPause(bool pause)
    {
        if (pause) PersistRng();
    }

    private void OnApplicationQuit()
    {
        PersistRng();
    }

    private void PersistRng()
    {
        var runId = PlayerPrefs.GetString("lastRunId", "");
        if (string.IsNullOrEmpty(runId)) return;
        var states = _rng?.GetStatesForSave();
        if (states == null) return;
        _db?.UpsertRngStates(runId, states);
        Debug.Log("[GameInitializer] RNG states persisted.");
    }
}
