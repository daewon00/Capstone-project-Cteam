using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Game.Save;

/// <summary>
/// 이벤트 세션 관리 서비스가 외부에 제공해야 할 기능 계약입니다.
/// </summary>
public interface IEventManager
{
    EventSessionDTO LoadActiveOrCreate(string eventIdFallback);
    /// <summary>
    /// 선택지를 적용한 뒤 맵으로 돌아가야 하는지 여부를 반환합니다.
    /// </summary>
    bool ApplyChoice(EventSessionDTO session, EventChoiceDTO choice);

    // DB에서 활성 세션을 '생성하지 않고' 불러오기만 시도하는 기능
    EventSessionDTO TryLoadActive();
}
