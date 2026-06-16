using UnityEngine;

public class EKGAnalyzer : MonoBehaviour
{
    public static int LatestBpm { get; private set; }

    public int beatThreshold = 520;
    public float minSecondsBetweenBeats = 0.3f;

    public int CurrentBpm { get; private set; }

    private bool wasAboveThreshold;
    private float lastBeatTime = -10f;

    void Update()
    {
        float value = ArduinoReader.currentEKGFloat;
        bool isAboveThreshold = value >= beatThreshold;

        if (isAboveThreshold && !wasAboveThreshold)
        {
            float now = Time.time;
            float secondsSinceLastBeat = now - lastBeatTime;

            if (secondsSinceLastBeat >= minSecondsBetweenBeats && lastBeatTime > 0f)
            {
                CurrentBpm = Mathf.RoundToInt(60f / secondsSinceLastBeat);
                LatestBpm = CurrentBpm;
                Debug.Log("Puls: " + CurrentBpm + " BPM");
            }

            lastBeatTime = now;
        }

        wasAboveThreshold = isAboveThreshold;
    }
}
