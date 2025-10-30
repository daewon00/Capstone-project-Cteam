using System.Collections.Generic;
using Game.Save;

#region Data Transfer Objects (DTOs)

/// <summary>
/// 각 더미의 카드 수 스냅샷입니다.
/// </summary>
public struct PileCounts
{
    public int Hand { get; set; }
    public int Draw { get; set; }
    public int Discard { get; set; }
    public int Exhaust { get; set; }
}

public sealed class PlayResult
{
    public enum ResultCode { Success, CardNotInHand, NotEnoughEnergy, CannotPlay }
    public ResultCode Code { get; set; }
    public string PlayedInstanceId { get; set; }
    public CardLocation TargetPile { get; set; }
    public PileCounts FinalCounts { get; set; }
}

public enum DrawReason { Unknown, TurnStart, CardEffect, Mulligan, ManualButton, Relic }

public sealed class DrawResult
{
    public IReadOnlyList<CardRuntimeState> DrawnCards { get; set; }
    public int DrawnCountRequested { get; set; }
    public int DrawnCountActual { get; set; }
    public bool DidReshuffle { get; set; }
    public PileCounts FinalCounts { get; set; }
    public DrawReason Reason { get; set; }
}

#endregion

/// <summary>
/// 런 덱의 런타임 상태를 제어하고 전투 중 카드 이동을 지원하는 서비스 계약입니다.
/// </summary>
public interface IDeckService
{
    // 이벤트(컨텍스트 포함). 구현은 점진적으로 확장될 예정입니다.
    event System.Action<PlayResult> OnCardPlayed;
    event System.Action<DrawResult> OnCardsDrawn;
    event System.Action<PileCounts> OnPileCountsChanged;

    // 현재 런의 덱 상태를 로드/백필/저장까지 준비합니다.
    void LoadAndPrepareDeck(string runId);

    // --- 전투용 핵심 API ---
    void SetHandLimit(int limit);
    DrawResult DrawCards(int amount, DrawReason reason = DrawReason.Unknown);
    PlayResult PlayCard(string instanceId);

    // --- 전투 수명주기 API ---
    /// <summary>
    /// 새 전투를 준비합니다. 전투 종료 시점의 모든 더미(Hand/Discard/Exhaust 포함)를 DrawPile로 모으고 셔플합니다.
    /// </summary>
    void PrepareNewCombat();

    /// <summary>
    /// 전투 종료 정리 단계에서 호출합니다. 남아 있는 Hand를 Discard로 이동하는 등 마무리를 수행합니다.
    /// </summary>
    void CleanupAfterCombat();

    // --- UI 조회용 API ---
    int GetPileCount(CardLocation location);
    IReadOnlyList<CardRuntimeState> GetHandSnapshot();
    PileCounts GetPileCounts();

    /// <summary>
    /// 카드 ID로 새 카드를 생성하여 덱에 추가합니다. (기본: DiscardPile 상단)
    /// </summary>
    void AddCardToDeckById(string cardId, bool isUpgraded = false);

    /// <summary>
    /// 현재 런의 카드 런타임 상태 전체를 복사본으로 반환합니다.
    /// UI가 필터링하거나 정렬할 때 사용할 수 있습니다.
    /// </summary>
    IReadOnlyList<CardRuntimeState> GetAllCardsSnapshot();

    /// <summary>
    /// 특정 카드 인스턴스의 강화 상태를 변경합니다.
    /// </summary>
    /// <returns>강화 상태가 실제로 변경되면 true, 그렇지 않으면 false</returns>
    bool SetCardUpgradeState(string instanceId, bool upgraded);

    IReadOnlyList<CardRuntimeState> GetCardsInLocation(CardLocation location);
    CardRuntimeState GetCardByInstanceId(string instanceId);
    void UpdateBattleCardState(BattleSnapshot.BattleCardState state, CardLocation location);

    /// <summary>
    /// 지정된 수만큼 덱의 카드를 새 카드 ID로 변환합니다.
    /// </summary>
    /// <returns>변환에 성공한 카드 수</returns>
    int TransformCards(string targetCardId, int count = 1, bool upgrade = false);
}
