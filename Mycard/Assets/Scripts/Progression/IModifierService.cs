using UnityEngine;

/// <summary>
/// 수정자 적용 범위를 나타냅니다.
/// </summary>
public enum ModifierScope { Global, CurrentRun }

/// <summary>
/// 런에 바인딩된 수정자 값을 적용하는 서비스 계약입니다.
/// </summary>
public interface IModifierService
{
    void RebindRun(string runId);
    float Apply(string key, float baseValue, ModifierScope scope);
}
