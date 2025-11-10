using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DeckController를 자동 생성하던 레거시 유틸리티로 현재는 경고만 출력합니다.
/// </summary>
public class DeckLoader : MonoBehaviour
{
    /// <summary>
    /// 컴포넌트가 남아 있을 경우 경고를 출력하고 비활성화합니다.
    /// </summary>
    private void Awake()
    {
        // 레거시: DeckController 인스턴스를 생성하던 스크립트입니다.
        // 현재는 GameInitializer + IDeckService가 덱을 관리하므로 더 이상 필요하지 않습니다.
        GameLog.Warn("[DeckLoader] 레거시 스크립트입니다. GameInitializer/IDeckService가 덱을 관리합니다. 컴포넌트를 제거해도 됩니다.", this);
        enabled = false;
    }
}
