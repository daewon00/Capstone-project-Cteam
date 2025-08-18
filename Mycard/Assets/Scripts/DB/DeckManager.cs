// DeckManager.cs
// 이 스크립트는 게임 한 판(Run) 동안 플레이어의 모든 카드를 관리하는 '총괄 셰프'입니다.
// - 카드 인스턴스 생성(고유 시리얼 부여)
// - 런 초기화/복원
// - 저장 스냅샷 변환
// - (선택) 전투용 도우미: 섞기/드로우/디스카드/소멸

using System;
using System.Collections.Generic;
using System.Globalization;   // 숫자 형식을 변환하기 위해 필요합니다.
using System.Threading;       // 여러 작업이 동시에 실행될 때 안전하게 숫자를 다루기 위해 필요합니다.
using UnityEngine;
using Game.Save;              // SaveData.cs에 정의된 데이터 구조를 사용하기 위해 필요합니다.

public class DeckManager : MonoBehaviour
{
    [Header("디버그 설정")]
    [SerializeField] private bool verboseLogs = false; // 체크하면, 콘솔에 더 자세한 작동 로그를 표시합니다.

    // --- 카드 시리얼 번호 발급을 위한 도구들 ---
    private long _nextCardInstanceCounter = 0; // 새 카드를 만들 때마다 1씩 증가하는 카운터입니다.
    private string _runId; // 현재 진행 중인 판의 고유 ID입니다.
    public string CurrentRunId => _runId; // 다른 스크립트에서 현재 RunId를 읽을 수 있게 해줍니다.

    // --- 현재 런의 카드 보관함들 ---
    /// <summary>
    /// 플레이어가 이번 판에서 보유한 모든 카드의 '원본 목록'입니다. (전투가 끝나도 유지됨)
    /// </summary>
    public readonly List<CardInstance> MasterDeck  = new List<CardInstance>();
    /// <summary>
    /// 전투 시 카드를 뽑을 '뽑을 牌 더미'입니다. (전투 중에만 사용)
    /// </summary>
    public readonly List<CardInstance> DrawPile    = new List<CardInstance>();
    /// <summary>
    /// 사용한 카드가 쌓이는 '버린 牌 더미'입니다. (전투 중에만 사용)
    /// </summary>
    public readonly List<CardInstance> DiscardPile = new List<CardInstance>();
    /// <summary>
    /// 게임에서 제외된 카드가 쌓이는 '소멸 더미'입니다. (전투 중에만 사용)
    /// </summary>
    public readonly List<CardInstance> ExhaustPile = new List<CardInstance>();

    /// <summary>
    /// 새로운 판을 시작하거나 "이어하기"로 데이터를 불러올 때 호출됩니다.
    /// DB에서 읽어온 '저장용 데이터(CardInDeck)'를 '실제 게임용 카드(CardInstance)'로 복원합니다.
    /// </summary>
    public void InitForRun(string runId, IEnumerable<CardInDeck> persistedCards)
    {
        // runId가 없으면 시스템이 꼬일 수 있으므로, 에러를 발생시켜 문제를 즉시 알립니다.
        if (string.IsNullOrEmpty(runId))
            throw new ArgumentException("InitForRun: runId가 비어있습니다.");

        _runId = runId;
        _nextCardInstanceCounter = 0; // 새 판이므로 카드 번호표 카운터를 0으로 초기화합니다.

        // 모든 카드 보관함을 깨끗이 비웁니다.
        MasterDeck.Clear();
        DrawPile.Clear();
        DiscardPile.Clear();
        ExhaustPile.Clear();

        if (persistedCards != null)
        {
            foreach (var row in persistedCards)
            {
                // 저장용 데이터를 실제 게임용 카드로 변환하여 MasterDeck에 추가합니다.
                var card = new CardInstance(row.InstanceId, row.CardId, row.IsUpgraded);
                MasterDeck.Add(card);

                // 저장된 시리얼 번호(예: "RUNID-0000000A")에서 가장 큰 숫자를 찾아,
                // 카운터가 그 숫자부터 다시 시작하도록 복원합니다. (ID 중복 방지)
                var parts = row.InstanceId.Split('-');
                var lastPart = parts[parts.Length - 1];
                if (long.TryParse(lastPart, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var parsedId))
                    _nextCardInstanceCounter = Math.Max(_nextCardInstanceCounter, parsedId);
            }
        }

        if (verboseLogs)
            Debug.Log($"[DeckManager] 런 초기화 완료: runId={_runId}, 보유 카드={MasterDeck.Count}, 다음 카드 번호=0x{_nextCardInstanceCounter:X}");
    }

    /// <summary>
    /// 새로운 카드 인스턴스를 생성하고 MasterDeck에 추가하는 '공장' 함수입니다.
    /// </summary>
    public CardInstance CreateNewCardInstance(string baseCardId, bool upgraded)
    {
        // InitForRun이 먼저 호출되지 않으면, 어떤 판에 속한 카드인지 알 수 없으므로 에러를 발생시킵니다.
        if (string.IsNullOrEmpty(_runId))
            throw new InvalidOperationException("CreateNewCardInstance가 InitForRun보다 먼저 호출되었습니다.");

        var instanceId = AllocateInstanceId(); // 고유 시리얼 번호를 발급받습니다.
        var card = new CardInstance(instanceId, baseCardId, upgraded);
        MasterDeck.Add(card); // 생성된 카드는 우선 '마스터 덱'에 보관합니다.

        if (verboseLogs)
            Debug.Log($"[DeckManager] 새 카드 생성: {baseCardId} (시리얼: {instanceId}, 강화: {upgraded})");

        return card;
    }

    /// <summary>
    /// 절대 겹치지 않는 고유 시리얼 번호를 생성합니다. (예: "<runId>-00000001")
    /// </summary>
    private string AllocateInstanceId()
    {
        // Interlocked.Increment는 여러 작업이 동시에 실행되어도 안전하게 숫자를 1 올립니다.
        var n = Interlocked.Increment(ref _nextCardInstanceCounter);
        // "현재판ID-16진수8자리" 형태로 최종 ID를 만듭니다. (예: ...-0000000A)
        return $"{_runId}-{n:X8}";
    }

    /// <summary>
    /// 게임을 저장할 때, DB가 이해할 수 있는 '저장용 데이터(CardInDeck)' 형태로 MasterDeck의 스냅샷을 만듭니다.
    /// </summary>
    public List<CardInDeck> ToCardRowsForSave()
    {
        var rows = new List<CardInDeck>(MasterDeck.Count);
        foreach (var c in MasterDeck)
        {
            rows.Add(new CardInDeck
            {
                InstanceId = c.InstanceId,
                RunId      = _runId,
                CardId     = c.BaseCardId,
                IsUpgraded = c.IsUpgraded
            });
        }
        return rows;
    }

    // =========================
    // 전투용 보조 메서드 (선택사항이지만 매우 유용)
    // =========================

    /// <summary>
    /// 전투 시작을 준비합니다: MasterDeck의 모든 카드를 DrawPile로 복사한 뒤 섞습니다.
    /// </summary>
    public void PrepareForCombat(int shuffleSeed = 0)
    {
        // 이전 전투의 카드들을 모두 정리합니다.
        DrawPile.Clear();
        DiscardPile.Clear();
        ExhaustPile.Clear();

        // MasterDeck의 모든 카드를 DrawPile로 가져옵니다.
        DrawPile.AddRange(MasterDeck);

        // DrawPile을 무작위로 섞습니다.
        ShuffleDrawPile(shuffleSeed);

        // 모든 카드의 '전투 중 임시 효과'를 초기화합니다.
        foreach (var c in DrawPile)
        {
            c.TempCostDelta = 0;
            c.CombatModsJson = null;
        }

        if (verboseLogs)
            Debug.Log($"[DeckManager] 전투 준비 완료: 뽑을 덱={DrawPile.Count}장");
    }

    /// <summary>
    /// 드로우 더미를 섞습니다 (Fisher-Yates 알고리즘). seed가 0이면 무작위로 섞습니다.
    /// </summary>
    public void ShuffleDrawPile(int seed = 0)
    {
        var rng = seed == 0 ? new System.Random() : new System.Random(seed);
        for (int i = DrawPile.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            // 리스트의 i번째와 j번째 카드의 위치를 서로 바꿉니다.
            (DrawPile[i], DrawPile[j]) = (DrawPile[j], DrawPile[i]);
        }
        if (verboseLogs) Debug.Log("[DeckManager] 뽑을 牌 더미를 섞었습니다.");
    }

    /// <summary>
    /// 카드를 1장 뽑습니다. 뽑을 牌가 없으면, 버린 牌를 섞어서 보충합니다.
    /// </summary>
    public CardInstance DrawOne()
    {
        // 1. 뽑을 牌가 없는지 확인합니다.
        if (DrawPile.Count == 0)
        {
            // 2. 버린 牌도 없으면, 더 이상 뽑을 카드가 없으므로 null을 반환합니다.
            if (DiscardPile.Count == 0) return null;

            // 3. 버린 牌를 모두 뽑을 牌 더미로 옮기고, 버린 牌 더미는 비웁니다.
            DrawPile.AddRange(DiscardPile);
            DiscardPile.Clear();
            // 4. 새로 채워진 뽑을 牌 더미를 섞습니다.
            ShuffleDrawPile();
        }

        // 5. 뽑을 牌 더미의 맨 위(리스트의 맨 끝) 카드를 가져옵니다.
        var topCard = DrawPile[DrawPile.Count - 1];
        DrawPile.RemoveAt(DrawPile.Count - 1); // 뽑았으니 더미에서 제거합니다.
        return topCard;
    }

    /// <summary>
    /// 지정된 수만큼 카드를 뽑습니다.
    /// </summary>
    public List<CardInstance> Draw(int count)
    {
        var drawnCards = new List<CardInstance>(count);
        for (int k = 0; k < count; k++)
        {
            var card = DrawOne();
            if (card == null) break; // 더 이상 뽑을 카드가 없으면 중단합니다.
            drawnCards.Add(card);
        }
        return drawnCards;
    }

    /// <summary>
    /// 카드를 버림 더미로 이동시킵니다.
    /// </summary>
    public void Discard(CardInstance card)
    {
        if (card == null) return;
        // 카드가 다른 더미에 있을 수 있으므로, 일단 모두 제거하여 중복을 방지합니다.
        RemoveFromAllCombatPiles(card);
        DiscardPile.Add(card);
    }

    /// <summary>
    /// 카드를 소멸 더미로 이동시킵니다.
    /// </summary>
    public void Exhaust(CardInstance card)
    {
        if (card == null) return;
        RemoveFromAllCombatPiles(card);
        ExhaustPile.Add(card);
    }

    /// <summary>
    /// 특정 카드를 모든 전투용 더미에서 제거하여, 카드가 중복으로 존재하는 것을 방지합니다.
    /// </summary>
    private void RemoveFromAllCombatPiles(CardInstance card)
    {
        DrawPile.Remove(card);
        DiscardPile.Remove(card);
        ExhaustPile.Remove(card);
    }
}