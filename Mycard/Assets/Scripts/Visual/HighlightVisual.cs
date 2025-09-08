using System;
using System.Collections.Generic;
using UnityEngine;

// 현장 담당자(로직) 역할: 프로파일에 따라 렌더러/오브젝트에 상태 시각을 반영
public class HighlightVisual : MonoBehaviour
{
    [Header("Profile")]
    [SerializeField] private HighlightProfile profile;

    [Header("Targets")]
    [SerializeField] private Renderer[] targetRenderers;
    [SerializeField] private bool autoCollectRenderersFromChildren = true;
    [SerializeField] private bool includeInactiveChildren = true;

    [Header("Lifecycle")]
    [SerializeField] private bool setOffOnAwake = true;

    private HighlightProfile.HighlightStateType _currentState = HighlightProfile.HighlightStateType.Off;

    // 원본 머티리얼 보관(MaterialSwap 모드에서 복원용)
    private Material[][] _originalSharedMaterials;

    // 성능을 위한 캐시
    private readonly Dictionary<Renderer, MaterialPropertyBlock> _mpbCache = new Dictionary<Renderer, MaterialPropertyBlock>();

    private void Awake()
    {
        EnsureTargets();

        CacheOriginalMaterials();

        if (setOffOnAwake)
        {
            TryApplyState(HighlightProfile.HighlightStateType.Off);
        }
    }

    private void EnsureTargets()
    {
        bool needsCollect = targetRenderers == null || targetRenderers.Length == 0;
        if (!needsCollect)
        {
            // 배열이 있으나 모두 null인 경우도 자동 수집 대상으로 처리
            bool allNull = true;
            for (int i = 0; i < targetRenderers.Length; i++)
            {
                if (targetRenderers[i] != null) { allNull = false; break; }
            }
            needsCollect = allNull;
        }
        if (needsCollect && autoCollectRenderersFromChildren)
        {
            targetRenderers = GetComponentsInChildren<Renderer>(includeInactiveChildren);
        }
    }

    private void CacheOriginalMaterials()
    {
        if (targetRenderers == null || targetRenderers.Length == 0)
        {
            _originalSharedMaterials = Array.Empty<Material[]>();
            return;
        }

        _originalSharedMaterials = new Material[targetRenderers.Length][];
        for (int i = 0; i < targetRenderers.Length; i++)
        {
            var r = targetRenderers[i];
            _originalSharedMaterials[i] = r != null ? r.sharedMaterials : Array.Empty<Material>();
        }
    }

    public void SetProfile(HighlightProfile newProfile)
    {
        profile = newProfile;
        TryApplyState(_currentState);
    }

    public void SetTargets(Renderer[] renderers)
    {
        targetRenderers = renderers;
        CacheOriginalMaterials();
        TryApplyState(_currentState);
    }

    public void SetState(HighlightProfile.HighlightStateType state)
    {
        if (_currentState == state) return;
        TryApplyState(state);
    }

    private void TryApplyState(HighlightProfile.HighlightStateType state)
    {
        _currentState = state;

        if (profile == null)
        {
            // 프로파일이 없으면 비활성화 또는 무시
            return;
        }

        // 타깃이 비었거나 잘못된 경우 다시 수집 시도
        EnsureTargets();

        var settings = profile.GetSettings(state);

        switch (profile.applyMode)
        {
            case HighlightProfile.ApplyMode.ColorOnly:
                if (settings.applyColor)
                    ApplyColor(settings.color);
                break;
            case HighlightProfile.ApplyMode.MaterialSwap:
                if (settings.applyMaterial)
                    ApplyMaterial(settings.material);
                else
                    RevertMaterials();
                break;
            case HighlightProfile.ApplyMode.ObjectToggle:
                ApplyObjectToggle(settings.objectsToEnable, settings.objectsToDisable);
                break;
        }
    }

    private void ApplyColor(Color c)
    {
        EnsureTargets();
        if (targetRenderers == null || targetRenderers.Length == 0) return;

        var props = profile.colorPropertyNames;
        bool useMPB = profile.useMaterialPropertyBlock;
        var matchMode = profile.colorMatchMode;

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            var r = targetRenderers[i];
            if (r == null) continue;
            var shareds = r.sharedMaterials;
            if (shareds == null || shareds.Length == 0) continue;

            // 렌더러의 모든 서브 머티리얼을 검사하여 매칭되는 속성 수집
            var matchedProps = new List<string>(props.Length);
            for (int p = 0; p < props.Length; p++)
            {
                var prop = props[p];
                bool found = false;
                for (int s = 0; s < shareds.Length; s++)
                {
                    var sm = shareds[s];
                    if (sm != null && sm.HasProperty(prop))
                    {
                        found = true;
                        break;
                    }
                }
                if (found)
                {
                    matchedProps.Add(prop);
                    if (matchMode == HighlightProfile.ColorMatchMode.FirstMatch)
                        break; // 첫 매치만 적용
                }
            }
            if (matchedProps.Count == 0) continue;

            if (useMPB)
            {
                if (!_mpbCache.TryGetValue(r, out var block) || block == null)
                {
                    block = new MaterialPropertyBlock();
                    _mpbCache[r] = block;
                }
                r.GetPropertyBlock(block);
                for (int k = 0; k < matchedProps.Count; k++)
                {
                    block.SetColor(matchedProps[k], c);
                }
                r.SetPropertyBlock(block);
            }
            else
            {
                // 머티리얼 인스턴싱 발생 허용 시: 각 재료에 대해 모든 매칭 속성에 적용
                var mats = r.materials;
                for (int m = 0; m < mats.Length; m++)
                {
                    var mat = mats[m];
                    if (mat == null) continue;
                    for (int k = 0; k < matchedProps.Count; k++)
                    {
                        var prop = matchedProps[k];
                        if (mat.HasProperty(prop)) mat.SetColor(prop, c);
                    }
                }
            }
        }
    }

    private void ApplyMaterial(Material replacement)
    {
        if (targetRenderers == null) return;
        if (replacement == null)
        {
            RevertMaterials();
            return;
        }

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            var r = targetRenderers[i];
            if (r == null) continue;

            // 모든 서브 머티리얼을 동일 재료로 교체(간단 전략)
            var shareds = r.sharedMaterials;
            var repList = new Material[shareds != null ? shareds.Length : 1];
            for (int m = 0; m < repList.Length; m++) repList[m] = replacement;
            r.sharedMaterials = repList;
        }
    }

    private void RevertMaterials()
    {
        if (_originalSharedMaterials == null || targetRenderers == null) return;
        for (int i = 0; i < targetRenderers.Length && i < _originalSharedMaterials.Length; i++)
        {
            var r = targetRenderers[i];
            if (r == null) continue;
            r.sharedMaterials = _originalSharedMaterials[i];
        }
    }

    private static void ApplyObjectToggle(GameObject[] enable, GameObject[] disable)
    {
        if (enable != null)
        {
            for (int i = 0; i < enable.Length; i++)
            {
                if (enable[i] != null && !enable[i].activeSelf) enable[i].SetActive(true);
            }
        }

        if (disable != null)
        {
            for (int i = 0; i < disable.Length; i++)
            {
                if (disable[i] != null && disable[i].activeSelf) disable[i].SetActive(false);
            }
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Preview/Off")] private void PreviewOff() => TryApplyState(HighlightProfile.HighlightStateType.Off);
    [ContextMenu("Preview/Allowed")] private void PreviewAllowed() => TryApplyState(HighlightProfile.HighlightStateType.Allowed);
    [ContextMenu("Preview/Blocked")] private void PreviewBlocked() => TryApplyState(HighlightProfile.HighlightStateType.Blocked);
#endif
}
