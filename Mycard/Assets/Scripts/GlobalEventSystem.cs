using UnityEngine;
using UnityEngine.EventSystems;

public class GlobalEventSystem : MonoBehaviour
{
    private static GlobalEventSystem _instance;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
