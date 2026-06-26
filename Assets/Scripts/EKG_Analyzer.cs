//Hovedforfatter(e) Matti og Benin
//Sekundærforfatter(e) Amarinder
using UnityEngine;

// Finder R-takker i live-signalet og beregner puls i BPM.
public class EKGAnalyzer : MonoBehaviour
{
    // Den nyeste beregnede puls, som resten af programmet kan læse.
    public static int LatestBpm { get; private set; }

    // Mindste/tilladte tid mellem to hjerteslag. Det beskytter mod dobbelt-peaks og støj.
    public float minSecondsBetweenBeats = 0.42f;
    public float maxSecondsBetweenBeats = 2.2f;

    // Hvor hurtigt baseline og pulsberegning må følge signalet.
    public float baselineFollow = 0.015f;
    public float peakThresholdMultiplier = 1.45f;
    public float bpmSmoothing = 0.18f;

    // Interne værdier til filtrering og peak-detektion.
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
        // Kør kun analysen, når Arduino faktisk har sendt en ny sample.
        if (ArduinoReader.SamplesReceived == lastSampleCount)
        {
            return;
        }

        lastSampleCount = ArduinoReader.SamplesReceived;

        // Råværdi fra Arduinoen.
        float raw = ArduinoReader.currentEKGFloat;

        // Første sample bruges som start-baseline.
        if (!hasBaseline)
        {
            baseline = raw;
            hasBaseline = true;
        }

        // Fjern langsomt skiftende baseline, så peaks bliver tydeligere.
        baseline = Mathf.Lerp(baseline, raw, baselineFollow);
        previousFiltered = currentFiltered;
        currentFiltered = raw - baseline;

        // Envelope følger signalets styrke og giver en adaptiv tærskel for R-takker.
        envelope = Mathf.Lerp(envelope, Mathf.Abs(currentFiltered), 0.03f);
        float threshold = Mathf.Max(18f, envelope * peakThresholdMultiplier);

        // En R-tak er lokalt peak: signalet steg før og falder nu.
        bool isRising = currentFiltered > previousFiltered;
        bool isLocalPeak = wasRising && !isRising;
        wasRising = isRising;

        // Ignorer små peaks under tærsklen.
        if (!isLocalPeak || previousFiltered < threshold)
        {
            return;
        }

        // Tid mellem to accepterede R-takker omregnes til BPM.
        float now = Time.time;
        float secondsSinceLastBeat = now - lastBeatTime;

        if (secondsSinceLastBeat < minSecondsBetweenBeats)
        {
            return;
        }

        // Når der er et tidligere slag, kan tiden mellem slagene omregnes til BPM.
        if (lastBeatTime > 0f && secondsSinceLastBeat <= maxSecondsBetweenBeats)
        {
            float instantBpm = 60f / secondsSinceLastBeat;

            // Udjævn pulsen, så tallet ikke hopper for voldsomt fra sample til sample.
            smoothedBpm = smoothedBpm <= 0f
                ? instantBpm
                : Mathf.Lerp(smoothedBpm, instantBpm, bpmSmoothing);

            LatestBpm = Mathf.RoundToInt(smoothedBpm);
        }

        // Gem tidspunktet for den accepterede R-tak.
        lastBeatTime = now;
    }
}
