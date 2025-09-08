using System;

public interface IRewardUI
{
    /// <summary>
    /// 보상 UI를 표시하고, 닫힐 때 onClosed 콜백을 호출합니다.
    /// </summary>
    void Show(RewardContainer rewards, Action onClosed);
}

