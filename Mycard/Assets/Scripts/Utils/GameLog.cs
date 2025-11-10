using System.Diagnostics;
using UnityEngine;

/// <summary>
/// Central logging helper. Info/Warn calls are stripped from non-development builds via Conditional attributes.
/// </summary>
public static class GameLog
{
    [Conditional("UNITY_EDITOR")]
    [Conditional("DEVELOPMENT_BUILD")]
    public static void Info(object message, Object context = null)
    {
        if (context != null) UnityEngine.Debug.Log(message, context);
        else UnityEngine.Debug.Log(message);
    }

    [Conditional("UNITY_EDITOR")]
    [Conditional("DEVELOPMENT_BUILD")]
    public static void Warn(object message, Object context = null)
    {
        if (context != null) UnityEngine.Debug.LogWarning(message, context);
        else UnityEngine.Debug.LogWarning(message);
    }

    public static void Error(object message, Object context = null)
    {
        if (context != null) UnityEngine.Debug.LogError(message, context);
        else UnityEngine.Debug.LogError(message);
    }
}
