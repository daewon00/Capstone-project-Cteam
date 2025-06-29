using UnityEngine;
using DG.Tweening;

/// <summary>
/// 카드 선택 등 외부 호출로 카메라를 부드럽게 이동/회전시키고,
/// 필요 시 원위치로 복귀시키는 싱글톤 기반 컨트롤러입니다.
/// </summary>
public class CameraController : MonoBehaviour
{
    public static CameraController instance;

    [Tooltip("주 카메라(transform) (비워두면 Camera.main 자동 할당)")]
    public Camera mainCamera;
    [Tooltip("카메라 복귀 위치/회전용 Transform")]
    public Transform homeTransform;
    [Tooltip("카메라 위치/회전용 Transform")]
    public Transform battleTransform;
    public Transform handTransform;
    [Tooltip("이동 및 회전 시간 (초)")]
    public float moveDuration = 1f;
    [Tooltip("이징 곡선 설정(DOTween Ease)")]
    public Ease easeType = Ease.InOutSine;

    private void Awake()
    {
        // 싱글톤 인스턴스 할당
        if (instance == null)
            instance = this;
        else
            Destroy(gameObject);

        if (mainCamera == null)
            mainCamera = Camera.main;
    }

    /// <summary>
    /// 지정된 Transform 위치와 회전으로 카메라를 부드럽게 이동/회전시킵니다.
    /// </summary>
    /// <param name="target">이동/회전 목표 Transform</param>
    public void MoveTo(Transform target)
    {
        if (mainCamera == null) return;
        Transform cam = mainCamera.transform;

        cam.DOKill();
        // 위치
        cam.DOMove(target.position, moveDuration).SetEase(easeType);
        // 회전
        cam.DORotateQuaternion(target.rotation, moveDuration).SetEase(easeType);
    }

  
 
}
