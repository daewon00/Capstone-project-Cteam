using System.Collections.Generic;
using UnityEngine;

public interface ICardCatalog
{
    /// <summary>
    /// 지정된 CardId에 해당하는 카드 원본 데이터를 반환합니다. (없으면 null)
    /// </summary>
    CardScriptableObject GetCardData(string cardId);

    /// <summary>
    /// 지정된 CardId에 해당하는 카드 원본 데이터를 안전하게 조회합니다.
    /// </summary>
    bool TryGetCardData(string cardId, out CardScriptableObject cardData);

    /// <summary>
    /// 카탈로그에 로드된 전체 카드 데이터의 수입니다.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// 카탈로그에 로드된 모든 카드 ID 목록을 반환합니다.
    /// </summary>
    System.Collections.Generic.IReadOnlyList<string> GetAllCardIds();
}

public sealed class CardCatalog : ICardCatalog
{
    private readonly Dictionary<string, CardScriptableObject> _database = new Dictionary<string, CardScriptableObject>();
    public int Count => _database.Count;

    /// <summary>
    /// Resources/{resourcePath}에서 모든 CardScriptableObject를 로드하여 인덱스를 구축합니다.
    /// </summary>
    public CardCatalog(string resourcePath)
    {
        var allCardData = Resources.LoadAll<CardScriptableObject>(resourcePath);
        if (allCardData == null || allCardData.Length == 0)
        {
            Debug.LogWarning($"[CardCatalog] 지정된 경로(Resources/{resourcePath})에서 카드 데이터를 찾을 수 없습니다.");
            return;
        }

        foreach (var cardData in allCardData)
        {
            if (cardData == null) continue;

            // 프로젝트 규약: CardScriptableObject.CardId 사용
            var id = cardData.CardId;
            if (string.IsNullOrEmpty(id))
            {
                Debug.LogError($"[CardCatalog] CardId가 비어있는 카드 에셋: {cardData.name}", cardData);
                continue;
            }
            if (_database.ContainsKey(id))
            {
                Debug.LogError($"[CardCatalog] 중복된 CardId가 감지되었습니다: {id}", cardData);
                continue;
            }
            _database.Add(id, cardData);
        }

        Debug.Log($"[CardCatalog] {_database.Count}개의 카드 데이터를 로드했습니다.");
    }

    public CardScriptableObject GetCardData(string cardId)
    {
        if (cardId != null && _database.TryGetValue(cardId, out var data))
            return data;
        return null;
    }

    public bool TryGetCardData(string cardId, out CardScriptableObject cardData)
        => _database.TryGetValue(cardId, out cardData);

    public System.Collections.Generic.IReadOnlyList<string> GetAllCardIds()
        => new System.Collections.Generic.List<string>(_database.Keys);
}

