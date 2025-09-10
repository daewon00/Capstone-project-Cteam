using UnityEngine;

public enum ModifierScope { Global, CurrentRun }

public interface IModifierService
{
    void RebindRun(string runId);
    float Apply(string key, float baseValue, ModifierScope scope);
}

