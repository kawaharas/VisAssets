using UnityEngine;
using UnityEngine.UI;

public class MinMaxUISync : MonoBehaviour
{
    public Slider minSlider;
    public Slider maxSlider;

    void Start()
    {
        minSlider.onValueChanged.AddListener(OnMinChanged);
        maxSlider.onValueChanged.AddListener(OnMaxChanged);
    }

    void OnMinChanged(float value)
    {
        if (value > maxSlider.value)
        {
            minSlider.value = maxSlider.value;
        }
        else
        {
            // module.SetOverrideMin(minSlider.value);
        }
    }

    void OnMaxChanged(float value)
    {
        if (value < minSlider.value)
        {
            maxSlider.value = minSlider.value;
        }
        else
        {
            // module.SetOverrideMax(maxSlider.value);
        }
    }
}