using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 동료 정의에 포함된 시작 카드 목록을 덱 프리뷰 데이터로 변환하는 헬퍼입니다.
/// </summary>
public static class CompanionDeckPreviewBuilder
{
    public static IReadOnlyList<DeckPreviewEntry> BuildEntries(CompanionDefinition companion)
    {
        if (companion?.StartingCardIds == null || companion.StartingCardIds.Count == 0)
            return Array.Empty<DeckPreviewEntry>();

        var result = new List<DeckPreviewEntry>();

        foreach (var group in companion.StartingCardIds
                     .Where(id => !string.IsNullOrWhiteSpace(id))
                     .GroupBy(id => id))
        {
            result.Add(new DeckPreviewEntry(group.Key, group.Count()));
        }

        return result;
    }
}
