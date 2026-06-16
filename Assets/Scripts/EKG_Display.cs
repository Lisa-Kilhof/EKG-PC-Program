using TMPro;
using UnityEngine;

public class EKGDisplay : MonoBehaviour
{
    public TextMeshProUGUI ekgText;

    void Start()
    {
        if (ekgText != null)
        {
            ekgText.gameObject.SetActive(true);
            ekgText.rectTransform.localScale = Vector3.one;
            ekgText.fontSize = 28;
            ekgText.alignment = TextAlignmentOptions.TopLeft;
            ekgText.rectTransform.anchorMin = new Vector2(0f, 1f);
            ekgText.rectTransform.anchorMax = new Vector2(0f, 1f);
            ekgText.rectTransform.pivot = new Vector2(0f, 1f);
            ekgText.rectTransform.anchoredPosition = new Vector2(24f, -24f);
            ekgText.rectTransform.sizeDelta = new Vector2(760f, 160f);

            Canvas canvas = ekgText.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                canvas.transform.localScale = Vector3.one;
            }
        }
    }

    void Update()
    {
        if (ekgText == null)
        {
            return;
        }

        ekgText.text =
            "EKG: " + ArduinoReader.currentEKG +
            "\nSamples: " + ArduinoReader.SamplesReceived +
            "\nStatus: " + ArduinoReader.LastStatus;
    }

    void OnGUI()
    {
        GUIStyle style = new GUIStyle(GUI.skin.label);
        style.fontSize = 22;
        style.normal.textColor = Color.white;
        style.wordWrap = true;

        string message =
            "EKG LIVE\n" +
            "EKG: " + ArduinoReader.currentEKG + "\n" +
            "Samples: " + ArduinoReader.SamplesReceived + "\n" +
            "Status: " + ArduinoReader.LastStatus + "\n" +
            "Hvis skærmen er sort: åbn Assets/Scenes/SampleScene og tryk Play.";

        GUI.Box(new Rect(16, 16, 560, 170), GUIContent.none);
        GUI.Label(new Rect(32, 28, 530, 150), message, style);
    }
}
