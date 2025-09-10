using System;
using System.Collections.Generic;
using UnityEngine;

// 게임 전체에서 사용될 전문가(서비스)들을 등록하고 찾아 쓸 수 있게 해주는 '보관소' 클래스입니다.
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

    // 필수 서비스가 등록되지 않았을 때, 버그를 바로 찾을 수 있도록 돕는 기능
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

    // 새 게임 시작 시, 이전 게임의 전문가들을 모두 해고하는 기능
    public static void ClearAll()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        UnityEngine.Debug.Log("[BossFlow][ServiceRegistry] ClearAll called.");
#endif
        _services.Clear();
    }
    // ClearAll() 안됐을때 방지용
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        _services.Clear();
    }
}
