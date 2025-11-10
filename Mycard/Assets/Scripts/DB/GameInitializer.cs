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

        // 전체 게임에서 멀티 터치를 사용하지 않도록 전역 입력 설정을 비활성화합니다.
        Input.multiTouchEnabled = false;

        // 새 게임을 시작하거나 씬을 다시 로드할 때를 대비해, 보관소를 항상 깨끗하게 비웁니다.
        ServiceRegistry.ClearAll();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GameLog.Info("[BossFlow][GI] ServiceRegistry cleared. Bootstrapping...");
#endif

        // 1. [기반 시스템 준비] 데이터베이스에 먼저 연결합니다.
        DatabaseManager.Instance.Connect();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GameLog.Info("[BossFlow][GI] DB connected.");
#endif

        // 1.5. 카드 카탈로그 서비스 등록 (Resources/Cards)
        var cardCatalog = new CardCatalog("Cards");
        if (cardCatalog.Count == 0)
        {
            GameLog.Warn("[GameInitializer] CardCatalog가 비어있습니다. Resources/Cards 경로 또는 에셋 구성을 확인하세요.");
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GameLog.Info($"[GameInitializer] CardCatalog load complete. count={cardCatalog.Count}");
#endif
        ServiceRegistry.Register<ICardCatalog>(cardCatalog);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GameLog.Info($"[BossFlow][GI] Registered ICardCatalog (count={cardCatalog.Count}).");
#endif

        // 2. [부품 생성] '가벽' 역할을 할 DatabaseFacade를 생성합니다.
        var dbFacade = new DatabaseFacade();
        //    '보관소'에 IDatabase라는 이름으로 등록하여, 다른 전문가들이 찾아 쓸 수 있게 합니다.
        ServiceRegistry.Register<IDatabase>(dbFacade);
        _db = dbFacade;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GameLog.Info("[BossFlow][GI] Registered IDatabase.");
#endif

        // 3. 러닝 컨텍스트(runId) 확보
        // 런 ID가 DB에 실제로 존재하는지 sanity 체크까지 수행합니다.
        var runId = PlayerPrefs.GetString("lastRunId", "");
        RunLoadResult runData = null;
        if (!string.IsNullOrEmpty(runId))
        {
            try
            {
                runData = DatabaseManager.Instance.LoadCurrentRun(runId);
                if (runData == null || runData.Run == null)
                {
                    GameLog.Warn($"[GameInitializer] lastRunId에 해당하는 CurrentRun이 없어 초기화합니다: {runId}");
                    PlayerPrefs.DeleteKey("lastRunId");
                    PlayerPrefs.Save();
                    runId = string.Empty;
                    runData = null;
                }
            }
            catch (System.Exception e)
            {
                GameLog.Warn($"[GameInitializer] lastRunId 확인 중 오류: {e.Message}");
            }
        }

        var lifecycleService = new RunLifecycleService(dbFacade);
        ServiceRegistry.Register<IRunLifecycleService>(lifecycleService);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GameLog.Info($"[BossFlow][GI] Registered IRunLifecycleService (runId='{runId}').");
#endif
        if (!string.IsNullOrEmpty(runId))
        {
            var companionId = runData?.Run?.CompanionId;
            if (string.IsNullOrEmpty(companionId))
            {
                companionId = PlayerPrefs.GetString("selectedCompanionId", string.Empty);
            }
            lifecycleService.RegisterNewRun(runId, companionId);
        }
        else
        {
            lifecycleService.ResetActiveRun();
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GameLog.Info("[BossFlow][GI] Registered IRngService.");
#endif

        // 5. 월렛(지갑) 서비스 등록: DB-우선 골드 관리 + 브로드캐스트
        var wallet = new WalletService(dbFacade, runId);
        ServiceRegistry.Register<IWalletService>(wallet);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GameLog.Info($"[BossFlow][GI] Registered IWalletService (runId='{runId}').");
#endif

        var stageService = new RunStageService(dbFacade);
        stageService.RebindRun(runId);
        ServiceRegistry.Register<IRunStageService>(stageService);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GameLog.Info($"[BossFlow][GI] Registered IRunStageService (runId='{runId}').");
#endif

        // 5.5 특전/모디파이어/업적 서비스 등록
        var perkService = new PerkService(dbFacade);
        ServiceRegistry.Register<IPerkService>(perkService);
        var modifierService = new ModifierService(dbFacade);
        ServiceRegistry.Register<IModifierService>(modifierService);
        modifierService.RebindRun(runId);
        var achievementService = new AchievementService(dbFacade);
        achievementService.RebindProfile(GameContext.I != null ? GameContext.I.ProfileId : "P1");
        ServiceRegistry.Register<IAchievementService>(achievementService);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GameLog.Info("[BossFlow][GI] Registered Perk/Modifier/Achievement services.");
#endif

        var tutorialService = new TutorialService(dbFacade);
        var profileId = GameContext.I != null ? GameContext.I.ProfileId : "P1";
        tutorialService.RebindProfile(profileId);
        bool isTutorialRun = runData?.Run?.IsTutorialRun ?? false;
        if (!string.IsNullOrEmpty(runId))
        {
            tutorialService.BindRun(runId, isTutorialRun);
        }
        ServiceRegistry.Register<ITutorialService>(tutorialService);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GameLog.Info($"[BossFlow][GI] Registered ITutorialService (runId='{runId}', tutorialRun={isTutorialRun}).");
#endif

        // 5.6 업적 이벤트 구독: 게임 이벤트 허브 → 업적 서비스
        // 전투 승리: '첫 전투 승리' 진행도 증가 및 해금 시도
        MetaEvents.OnCombatVictory += payload =>
        {
            try
            {
                var ach = ServiceRegistry.Get<IAchievementService>();
                ach?.ReportProgress("ACH_FIRST_BATTLE", 1);
                ach?.UnlockIfEligible("ACH_FIRST_BATTLE");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                GameLog.Info("[BossFlow][GI] OnCombatVictory handler executed.");
#endif
            }
            catch { }
        };

        // 런 종료: 클리어 시 '첫 승리' 진행+해금, 안전하게 Flush
        MetaEvents.OnRunEnded += payload =>
        {
            try
            {
                var ach = ServiceRegistry.Get<IAchievementService>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                GameLog.Info($"[BossFlow][GI] OnRunEnded handler: cleared={payload.Cleared}");
#endif
                if (payload.Cleared)
                {
                    ach?.ReportProgress("ACH_FIRST_WIN", 1);
                    ach?.UnlockIfEligible("ACH_FIRST_WIN");
                }
                ach?.Flush();
            }
            catch { }
        };

        // 적 카드 파괴 카운트 → 티어 업적 진행/해금
        MetaEvents.OnEnemyCardDestroyed += payload =>
        {
            try
            {
                var ach = ServiceRegistry.Get<IAchievementService>();
                ach?.ReportProgress("ACH_DESTROY_ENEMY_CARDS", 1);
                ach?.UnlockIfEligible("ACH_DESTROY_ENEMY_CARDS");
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                GameLog.Info("[BossFlow][GI] OnEnemyCardDestroyed handler executed.");
#endif
            }
            catch { }
        };

        // 층 도달/골드 변경 등은 이후 확장 시 매핑을 추가합니다.

        // 6. EventManager 등록: runId가 있을 때만 등록(클리어 직후 등 런이 없을 때는 건너뜀)
        if (!string.IsNullOrEmpty(runId))
        {
            var eventManager = new EventManager(dbFacade, runId);
            ServiceRegistry.Register<IEventManager>(eventManager);
        }

        // 7. 덱 서비스 등록 + 현재 런 덱 준비(백필/초기 셔플 포함 가능)
        var rngService = ServiceRegistry.Get<IRngService>();
        var deckService = new DeckService(dbFacade, rngService);
        deckService.LoadAndPrepareDeck(runId);
        ServiceRegistry.Register<IDeckService>(deckService);

        var cardEffectService = new CardEffectService();
        ServiceRegistry.Register<ICardEffectService>(cardEffectService);

        // relicserivce
        //var relicService = new RelicService(dbFacade);
        //ServiceRegistry.Register<IRelicService>(relicService);
        //relicService.LoadAndPrepareRelics(runId);

        // 7.5. 런 서비스 등록: 전투 결과 커밋/라우팅 담당 (카탈로그 주입)
        var runService = new RunService(dbFacade, _rng, cardCatalog);
        ServiceRegistry.Register<IRunService>(runService);
        if (!string.IsNullOrEmpty(runId))
        {
            runService.RebindRun(runId);
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        GameLog.Info($"[BossFlow][GI] Registered IRunService (runId='{runId}').");
#endif

        // 8. 초기화 과정 중 변경되었을 수 있는 RNG 상태를 한 번 더 저장하여 정합성 보장
        if (!string.IsNullOrEmpty(runId) && rngService != null)
        {
            _db.UpsertRngStates(runId, rngService.GetStatesForSave());
        }

        GameLog.Info("GameInitializer: 모든 시스템 조립 및 등록이 완료되었습니다.");

        // 9. 업적 토스트 컨트롤러를 보장(중복 생성 방지용으로 1개만 유지)
        try
        {
            if (FindObjectsOfType<AchievementsToastController>(true).Length == 0)
            {
                gameObject.AddComponent<AchievementsToastController>();
            }
        }
        catch { }
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
        if (pause)
        {
            PersistRng();
            ServiceRegistry.Get<IAchievementService>()?.Flush();
        }
    }

    private void OnApplicationQuit()
    {
        PersistRng();
        ServiceRegistry.Get<IAchievementService>()?.Flush();
    }

    private void PersistRng()
    {
        var runId = PlayerPrefs.GetString("lastRunId", "");
        if (string.IsNullOrEmpty(runId)) return;
        var states = _rng?.GetStatesForSave();
        if (states == null) return;
        _db?.UpsertRngStates(runId, states);
        GameLog.Info("[GameInitializer] RNG states persisted.");
    }
}
