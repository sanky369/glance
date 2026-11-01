using UnityEngine;
using UnityEngine.UI;

public class ShaderSliderController : MonoBehaviour
{
    public Slider slider;
    public Renderer targetRenderer;
    public string shaderPropertyName = "_SlideOffset";

    [Header("Slider Limits")]
    public float sliderMinValue = -1f;
    public float sliderMaxValue = 1f;
    public float sliderStartValue = 0f;

    [Header("Shader Offset")]
    public float maxOffset = 1f;

    MaterialPropertyBlock propertyBlock;

    void Awake()
    {
        propertyBlock = new MaterialPropertyBlock();

        if (slider != null)
        {
            slider.minValue = sliderMinValue;
            slider.maxValue = sliderMaxValue;
            slider.value = Mathf.Clamp(sliderStartValue, sliderMinValue, sliderMaxValue);
            slider.onValueChanged.AddListener(UpdateShader);
        }

        UpdateShader(slider != null ? slider.value : sliderStartValue);
    }

    void UpdateShader(float value)
    {
        if (targetRenderer == null)
            return;

        float finalOffset = value * maxOffset;

        targetRenderer.GetPropertyBlock(propertyBlock);
        propertyBlock.SetFloat(shaderPropertyName, finalOffset);
        targetRenderer.SetPropertyBlock(propertyBlock);
    }

    void OnDestroy()
    {
        if (slider != null)
            slider.onValueChanged.RemoveListener(UpdateShader);
    }

    void OnValidate()
    {
        if (sliderMaxValue < sliderMinValue)
            sliderMaxValue = sliderMinValue;

        sliderStartValue = Mathf.Clamp(sliderStartValue, sliderMinValue, sliderMaxValue);

        if (slider != null)
        {
            slider.minValue = sliderMinValue;
            slider.maxValue = sliderMaxValue;
            slider.value = sliderStartValue;
        }
    }
}