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

public enum DrawReason { Unknown, TurnStart, CardEffect, Mulligan }

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

public interface IDeckService
{
    // 이벤트(컨텍스트 포함). 구현은 Phase 2에서 점진 추가 예정.
    event System.Action<PlayResult> OnCardPlayed;
    event System.Action<DrawResult> OnCardsDrawn;
    event System.Action<PileCounts> OnPileCountsChanged;

    // 현재 런의 덱 상태를 로드/백필/저장까지 준비합니다.
    void LoadAndPrepareDeck(string runId);

    // --- 전투용 핵심 API ---
    void SetHandLimit(int limit);
    DrawResult DrawCards(int amount, DrawReason reason = DrawReason.Unknown);
    PlayResult PlayCard(string instanceId);

    // --- UI 조회용 API ---
    int GetPileCount(CardLocation location);
    IReadOnlyList<CardRuntimeState> GetHandSnapshot();
    PileCounts GetPileCounts();

    /// <summary>
    /// 카드 ID로 새 카드를 생성하여 덱에 추가합니다. (기본: DiscardPile 상단)
    /// </summary>
    void AddCardToDeckById(string cardId, bool isUpgraded = false);
}
