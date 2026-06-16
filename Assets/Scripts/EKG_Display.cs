using TMPro;
using UnityEngine;

public class EKGDisplay : MonoBehaviour
{
    public TextMeshProUGUI ekgText;

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
}
