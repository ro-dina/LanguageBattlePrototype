using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Reuse existing rows; long dynamic names wrap and grow vertically within their scroll view.
public sealed class MaterialRowLayout : MonoBehaviour
{
    private TMP_Text label;
    private LayoutElement row;
    private float lastWidth = -1;
    public static void Configure(GameObject owner,TMP_Text text)
    {
        var layout = owner.AddComponent<MaterialRowLayout>();layout.label = text;layout.row = owner.GetComponent<LayoutElement>();
        text.enableAutoSizing = false;text.fontSize = 26;text.textWrappingMode = TextWrappingModes.Normal;text.overflowMode = TextOverflowModes.Overflow;
    }
    private void LateUpdate()
    {
        if (label == null || row == null) return;
        float width = label.rectTransform.rect.width;
        if (width <= 0 || Mathf.Abs(lastWidth-width) < .5f) return;
        lastWidth = width;
        row.preferredHeight = Mathf.Max(80,Mathf.Ceil(label.GetPreferredValues(label.text,width,float.PositiveInfinity).y)+24);
        LayoutRebuilder.MarkLayoutForRebuild((RectTransform)transform);
    }
}
