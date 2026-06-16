using UnityEngine;

public class EKGAnalyzer : MonoBehaviour
{
    public static int LatestBpm { get; private set; }

    public float minSecondsBetweenBeats = 0.42f;
    public float maxSecondsBetweenBeats = 2.2f;
    public float baselineFollow = 0.015f;
    public float peakThresholdMultiplier = 1.45f;
    public float bpmSmoothing = 0.18f;

    public int CurrentBpm { get; private set; }

    private float baseline;
    private float envelope = 20f;
    private float previousFiltered;
    private float currentFiltered;
    private float lastBeatTime = -10f;
    private float smoothedBpm;
    private bool hasBaseline;
    private bool wasRising;
    private long lastSampleCount = -1;

    void Update()
    {
        if (ArduinoReader.SamplesReceived == lastSampleCount)
        {
            return;
        }

        lastSampleCount = ArduinoReader.SamplesReceived;

        float raw = ArduinoReader.currentEKGFloat;
        if (!hasBaseline)
        {
            baseline = raw;
            hasBaseline = true;
        }

        baseline = Mathf.Lerp(baseline, raw, baselineFollow);
        previousFiltered = currentFiltered;
        currentFiltered = raw - baseline;

        envelope = Mathf.Lerp(envelope, Mathf.Abs(currentFiltered), 0.03f);
        float threshold = Mathf.Max(18f, envelope * peakThresholdMultiplier);

        bool isRising = currentFiltered > previousFiltered;
        bool isLocalPeak = wasRising && !isRising;
        wasRising = isRising;

        if (!isLocalPeak || previousFiltered < threshold)
        {
            return;
        }

        float now = Time.time;
        float secondsSinceLastBeat = now - lastBeatTime;

        if (secondsSinceLastBeat < minSecondsBetweenBeats)
        {
            return;
        }

        if (lastBeatTime > 0f && secondsSinceLastBeat <= maxSecondsBetweenBeats)
        {
            float instantBpm = 60f / secondsSinceLastBeat;
            smoothedBpm = smoothedBpm <= 0f
                ? instantBpm
                : Mathf.Lerp(smoothedBpm, instantBpm, bpmSmoothing);

            CurrentBpm = Mathf.RoundToInt(smoothedBpm);
            LatestBpm = CurrentBpm;
            Debug.Log("Puls: " + CurrentBpm + " BPM");
        }

        lastBeatTime = now;
    }
}
