using TMPro;
using UnityEngine;

public class EKGDisplay : MonoBehaviour
{
    public TextMeshProUGUI ekgText;

    void Update()
    {
        ekgText.text = "EKG: " + ArduinoReader.currentEKG;
    }
}