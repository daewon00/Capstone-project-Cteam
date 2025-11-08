using TMPro;
using UnityEngine;

/// <summary>
/// 씬에서 런 체력/골드/마나 정보를 표시하는 HUD 오버레이입니다.
/// </summary>
public sealed class RunStatOverlay : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private TMP_Text maxHpText;
    [SerializeField] private TMP_Text manaText;
    [SerializeField] private TMP_Text goldText;
    [SerializeField] private RollingNumberAnimator goldAnimator = new RollingNumberAnimator();

    [Header("Optional Fill Targets")]
    [SerializeField] private UnityEngine.UI.Image hpFillImage;
    [SerializeField] private UnityEngine.UI.Slider hpSlider;
    [SerializeField] private UnityEngine.UI.Image manaFillImage;
    [SerializeField] private UnityEngine.UI.Slider manaSlider;

    private IEventManager _eventManager;
    private IWalletService _wallet;

    private void Awake()
    {
        ServiceRegistry.Register<RunStatOverlay>(this);
    }

    private void OnDestroy()
    {
        if (ServiceRegistry.Get<RunStatOverlay>() == this)
        {
            ServiceRegistry.Register<RunStatOverlay>(null);
        }
    }

    private void OnEnable()
    {
        if (goldAnimator == null)
        {
            goldAnimator = new RollingNumberAnimator();
        }
        goldAnimator.Bind(this, goldText);

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

        goldAnimator.Unbind();
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
            Debug.LogWarning($"[RunStatOverlay] LoadCurrentRun 실패: {e.Message}");
            UpdateTexts(default);
        }
    }

    private void UpdateTexts(EventRunSnapshot snapshot)
    {
        int currentHp = Mathf.Max(0, snapshot.CurrentHp);
        int maxHp = Mathf.Max(1, snapshot.MaxHpBase + snapshot.MaxHpFromPerks + snapshot.MaxHpFromRelics);

        if (hpText != null)
        {
            hpText.text = maxHp > 0 ? $"{currentHp}/{maxHp}" : currentHp.ToString();
        }

        if (maxHpText != null)
        {
            maxHpText.text = maxHp.ToString();
        }

        float hpRatio = maxHp > 0 ? Mathf.Clamp01((float)currentHp / maxHp) : 0f;
        if (hpFillImage != null)
        {
            hpFillImage.fillAmount = hpRatio;
        }
        if (hpSlider != null)
        {
            hpSlider.minValue = 0f;
            hpSlider.maxValue = maxHp;
            hpSlider.value = Mathf.Clamp(currentHp, 0, maxHp);
        }

        int energyMax = snapshot.EnergyMax > 0 ? snapshot.EnergyMax : 0;
        if (manaText != null)
        {
            manaText.text = energyMax.ToString();
        }
        if (manaSlider != null)
        {
            manaSlider.minValue = 0;
            manaSlider.maxValue = energyMax;
            manaSlider.value = energyMax;
        }
        if (manaFillImage != null)
        {
            manaFillImage.fillAmount = energyMax > 0 ? 1f : 0f;
        }

        goldAnimator.SetInstant(Mathf.Max(0, snapshot.Gold));
    }

    private void HandleGoldChanged(int gold)
    {
        goldAnimator.AnimateTo(Mathf.Max(0, gold));
    }
}
