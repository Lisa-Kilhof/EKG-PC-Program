using UnityEngine;

public class EKGAnalyzer : MonoBehaviour
{
    void Update()
    {
        Debug.Log(ArduinoReader.currentEKG);
    }
}