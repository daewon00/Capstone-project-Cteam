using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 전역에서 서비스 인스턴스를 등록하고 검색하는 간단한 보관소입니다.
/// </summary>
public static class ServiceRegistry
{
    private static readonly Dictionary<Type, object> _services = new();

    /// <summary>
    /// 보관소에 전문가를 등록합니다.
    /// </summary>
    public static void Register<T>(T service) where T : class
    {
        _services[typeof(T)] = service;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        UnityEngine.Debug.Log($"[BossFlow][ServiceRegistry] Register: {typeof(T).Name} => {(service!=null ? "ok" : "null")}");
#endif
    }

    /// <summary>
    /// 보관소에서 필요한 전문가를 꺼내옵니다.
    /// </summary>
    public static T Get<T>() where T : class
    {
        _services.TryGetValue(typeof(T), out var service);
        return service as T;
    }

    /// <summary>
    /// 필수 서비스가 누락되었을 때 예외를 발생시켜 문제를 즉시 드러냅니다.
    /// </summary>
    public static T GetRequired<T>() where T : class
    {
        var svc = Get<T>();
        if (svc == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnityEngine.Debug.LogWarning($"[BossFlow][ServiceRegistry] GetRequired FAILED: {typeof(T).Name} not registered.");
#endif
            throw new InvalidOperationException($"[ServiceRegistry] 필수 서비스인 {typeof(T).Name}가 등록되지 않았습니다.");
        }
        return svc;
    }

    /// <summary>
    /// 등록된 모든 서비스를 해제합니다.
    /// </summary>
    public static void ClearAll()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        UnityEngine.Debug.Log("[BossFlow][ServiceRegistry] ClearAll called.");
#endif
        _services.Clear();
    }
    /// <summary>
    /// 도중 초기화 누락을 방지하기 위해 서브시스템 리셋 시 서비스를 비웁니다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _services.Clear();
    }
}
