using TMPro;
using UnityEngine;

/// <summary>
/// 이벤트 씬에서 런 체력/골드/마나 정보를 표시하는 HUD 오버레이입니다.
/// </summary>
public sealed class EventRunStatOverlay : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text maxHpText;
    [SerializeField] private TMP_Text manaText;
    [SerializeField] private TMP_Text goldText;

    private IEventManager _eventManager;
    private IWalletService _wallet;

    private void Awake()
    {
        ServiceRegistry.Register<EventRunStatOverlay>(this);
    }

    private void OnDestroy()
    {
        if (ServiceRegistry.Get<EventRunStatOverlay>() == this)
        {
            ServiceRegistry.Register<EventRunStatOverlay>(null);
        }
    }

    private void OnEnable()
    {
        _wallet = ServiceRegistry.Get<IWalletService>();
        if (_wallet != null)
        {
            _wallet.OnGoldChanged += HandleGoldChanged;
        }
    }

    private void OnDisable()
    {
        if (_wallet != null)
        {
            _wallet.OnGoldChanged -= HandleGoldChanged;
            _wallet = null;
        }
    }

    private void Start()
    {
        RefreshFromCache();
    }

    public void Refresh(EventRunSnapshot snapshot)
    {
        UpdateTexts(snapshot);
    }

    public void RefreshFallback()
    {
        RefreshFromCache();
    }

    private void RefreshFromCache()
    {
        if (_eventManager == null)
        {
            _eventManager = ServiceRegistry.Get<IEventManager>();
        }

        if (_eventManager != null && _eventManager.TryGetRunSnapshot(out var snapshot))
        {
            UpdateTexts(snapshot);
            return;
        }

        // 캐시에 없으면 DB에서 직접 로드 (이어하기로 이벤트만 열렸을 때 대비)
        string runId = GameContext.I != null && !string.IsNullOrEmpty(GameContext.I.RunId)
            ? GameContext.I.RunId
            : PlayerPrefs.GetString("lastRunId", string.Empty);

        if (string.IsNullOrEmpty(runId))
        {
            UpdateTexts(default);
            return;
        }

        try
        {
            var loaded = DatabaseManager.Instance.LoadCurrentRun(runId);
            var run = loaded?.Run;
            if (run == null)
            {
                UpdateTexts(default);
                return;
            }

            snapshot = new EventRunSnapshot
            {
                RunId = run.RunId,
                CurrentHp = run.CurrentHp,
                MaxHpBase = run.MaxHpBase,
                MaxHpFromPerks = run.MaxHpFromPerks,
                MaxHpFromRelics = run.MaxHpFromRelics,
                EnergyMax = run.EnergyMax,
                Gold = run.Gold
            };
            UpdateTexts(snapshot);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[EventRunStatOverlay] LoadCurrentRun 실패: {e.Message}");
            UpdateTexts(default);
        }
    }

    private void UpdateTexts(EventRunSnapshot snapshot)
    {
        int maxHp = Mathf.Max(1, snapshot.MaxHpBase + snapshot.MaxHpFromPerks + snapshot.MaxHpFromRelics);

        if (hpText != null)
        {
            hpText.text = snapshot.CurrentHp > 0 ? snapshot.CurrentHp.ToString() : "0";
        }

        if (maxHpText != null)
        {
            maxHpText.text = maxHp.ToString();
        }

        if (manaText != null)
        {
            int energy = snapshot.EnergyMax > 0 ? snapshot.EnergyMax : 0;
            manaText.text = energy.ToString();
        }

        if (goldText != null)
        {
            goldText.text = snapshot.Gold.ToString();
        }
    }

    private void HandleGoldChanged(int gold)
    {
        if (goldText != null)
        {
            goldText.text = Mathf.Max(0, gold).ToString();
        }
    }
}
