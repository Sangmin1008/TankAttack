using System;
using UnityEngine;
using UnityEngine.UI;

public class HpBarView : MonoBehaviour
{
    private Slider _slider;
    public RectTransform RectTransform { get; set; }

    private void Awake()
    {
        _slider = GetComponent<Slider>();
        RectTransform = GetComponent<RectTransform>();
    }

    public void UpdateValue(int currentHp, int maxHp)
    {
        _slider.maxValue = maxHp;
        _slider.value = currentHp;
    }
}
