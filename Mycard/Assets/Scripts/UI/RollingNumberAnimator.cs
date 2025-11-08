using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// TMP 숫자 텍스트를 목표 값까지 빠르게 굴려서 표시하는 유틸리티입니다.
/// </summary>
[System.Serializable]
public sealed class RollingNumberAnimator
{
    [SerializeField] private string numberFormat = "N0";
    [SerializeField, Min(1f)] private float minUnitsPerSecond = 90f;
    [SerializeField, Min(1f)] private float maxUnitsPerSecond = 2000f;
    [SerializeField, Range(0.1f, 1.5f)] private float maxRollDuration = 0.65f;
    [SerializeField, Range(0, 50)] private int slowTailThreshold = 20;

    private MonoBehaviour _owner;
    private TMP_Text _text;
    private Coroutine _rollRoutine;
    private int _visualValue;

    /// <summary>
    /// 코루틴을 운용할 주체와 텍스트 컴포넌트를 연결합니다.
    /// </summary>
    public void Bind(MonoBehaviour owner, TMP_Text targetText)
    {
        if (_owner == owner && _text == targetText) return;

        Stop();
        _owner = owner;
        _text = targetText;
        ApplyText(_visualValue);
    }

    /// <summary>
    /// 코루틴을 중단하고 연결을 해제합니다.
    /// </summary>
    public void Unbind()
    {
        Stop();
        _owner = null;
        _text = null;
    }

    /// <summary>
    /// 즉시 값을 강제 갱신합니다.
    /// </summary>
    public void SetInstant(int value)
    {
        value = Mathf.Max(0, value);
        _visualValue = value;
        ApplyText(_visualValue);
    }

    /// <summary>
    /// 목표 값까지 숫자를 굴려가며 표시합니다.
    /// </summary>
    public void AnimateTo(int value)
    {
        value = Mathf.Max(0, value);

        if (_owner == null || _text == null)
        {
            SetInstant(value);
            return;
        }

        if (_rollRoutine != null)
        {
            _owner.StopCoroutine(_rollRoutine);
        }

        _rollRoutine = _owner.StartCoroutine(RollRoutine(value));
    }

    /// <summary>
    /// 진행 중인 코루틴을 중단합니다.
    /// </summary>
    public void Stop()
    {
        if (_rollRoutine != null && _owner != null)
        {
            _owner.StopCoroutine(_rollRoutine);
        }
        _rollRoutine = null;
    }

    private IEnumerator RollRoutine(int target)
    {
        if (_visualValue == target)
        {
            ApplyText(_visualValue);
            _rollRoutine = null;
            yield break;
        }

        var direction = target > _visualValue ? 1 : -1;
        float carry = 0f;
        float maxSpeed = Mathf.Max(minUnitsPerSecond, maxUnitsPerSecond);

        while (_visualValue != target)
        {
            int remaining = Mathf.Abs(target - _visualValue);

            if (remaining <= slowTailThreshold)
            {
                _visualValue += direction;
                ApplyText(_visualValue);
                yield return null;
                continue;
            }

            float unitsPerSecond = Mathf.Max(minUnitsPerSecond, remaining / maxRollDuration);
            unitsPerSecond = Mathf.Min(maxSpeed, unitsPerSecond);

            carry += unitsPerSecond * Time.deltaTime;

            int step = Mathf.Max(1, Mathf.FloorToInt(carry));
            if (step == 0)
            {
                yield return null;
                continue;
            }

            carry -= step;
            _visualValue += direction * step;

            if ((direction > 0 && _visualValue > target) || (direction < 0 && _visualValue < target))
            {
                _visualValue = target;
            }

            ApplyText(_visualValue);
            yield return null;
        }

        _rollRoutine = null;
    }

    private void ApplyText(int value)
    {
        if (_text == null) return;

        if (string.IsNullOrEmpty(numberFormat))
        {
            _text.text = value.ToString();
        }
        else
        {
            _text.text = value.ToString(numberFormat);
        }
    }
}
