using UnityEngine;

/// <summary>
/// 덱 프리뷰(런 시작 전 등)에서 필요한 최소 카드 데이터를 묶어 전달하기 위한 구조체입니다.
/// </summary>
public readonly struct DeckPreviewEntry
{
    public DeckPreviewEntry(string cardId, int count, bool upgraded = false)
    {
        CardId = cardId;
        Count = Mathf.Max(1, count);
        Upgraded = upgraded;
    }

    /// <summary>
    /// 카드 리소스를 찾기 위한 ID.
    /// </summary>
    public string CardId { get; }

    /// <summary>
    /// 동일 카드가 몇 장 포함되어 있는지.
    /// </summary>
    public int Count { get; }

    /// <summary>
    /// 추후 확장을 위한 업그레이드 여부(기본값 false).
    /// </summary>
    public bool Upgraded { get; }
}
