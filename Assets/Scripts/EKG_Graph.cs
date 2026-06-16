using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class EKGGraph : MonoBehaviour
{
    public bool showWorldLine = false;
    public int maxPoints = 900;
    public float graphWidth = 14f;
    public float graphHeight = 2.8f;
    public float baselineFollow = 0.012f;
    public float signalSmoothing = 0.35f;
    public float qrsBoost = 1.4f;
    public bool autoCenter = true;
    public Color lineColor = new Color(0.2f, 1f, 0.35f);
    public float minVisibleRange = 22f;
    public float adaptiveScaleSpeed = 0.05f;

    private readonly List<float> values = new List<float>();
    private LineRenderer line;
    private float baseline;
    private float filteredValue;
    private float previousHighPassed;
    private float displayRange = 100f;
    private long lastSampleCount = -1;

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        if (line == null)
        {
            line = gameObject.AddComponent<LineRenderer>();
        }

        line.useWorldSpace = false;
        line.enabled = showWorldLine;
        line.positionCount = 0;
        line.startWidth = 0.025f;
        line.endWidth = 0.025f;
        line.numCapVertices = 2;
        line.numCornerVertices = 2;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = lineColor;
        line.endColor = lineColor;

        transform.localPosition = new Vector3(0f, 1.55f, 0f);
    }

    void Start()
    {
        baseline = ArduinoReader.currentEKGFloat;
    }

    void Update()
    {
        line.enabled = showWorldLine;

        if (!showWorldLine)
        {
            return;
        }

        if (ArduinoReader.SamplesReceived == lastSampleCount)
        {
            return;
        }

        lastSampleCount = ArduinoReader.SamplesReceived;
        AddValue(ArduinoReader.currentEKGFloat);
        DrawGraph();
    }

    private void AddValue(float raw)
    {
        baseline = values.Count == 0 ? raw : Mathf.Lerp(baseline, raw, baselineFollow);

        float highPassed = raw - baseline;
        float fastChange = highPassed - previousHighPassed;
        previousHighPassed = highPassed;

        float shapedValue = highPassed + fastChange * qrsBoost;
        filteredValue = values.Count == 0
            ? shapedValue
            : Mathf.Lerp(filteredValue, shapedValue, signalSmoothing);

        values.Add(filteredValue);

        while (values.Count > maxPoints)
        {
            values.RemoveAt(0);
        }
    }

    private void DrawGraph()
    {
        if (values.Count == 0)
        {
            line.positionCount = 0;
            return;
        }

        float center = autoCenter ? Average(values) : 0f;
        float targetRange = Mathf.Max(MaxAbsDistance(values, center), minVisibleRange);
        displayRange = Mathf.Lerp(displayRange, targetRange, adaptiveScaleSpeed);

        float xStep = values.Count > 1 ? graphWidth / (values.Count - 1) : 0f;

        line.positionCount = values.Count;

        for (int i = 0; i < values.Count; i++)
        {
            float x = (i * xStep) - (graphWidth * 0.5f);
            float normalized = (values[i] - center) / displayRange;
            float y = Mathf.Clamp(normalized, -1f, 1f) * graphHeight * 0.5f;
            line.SetPosition(i, new Vector3(x, y, 0f));
        }
    }

    private float Average(List<float> source)
    {
        float sum = 0f;

        for (int i = 0; i < source.Count; i++)
        {
            sum += source[i];
        }

        return sum / source.Count;
    }

    private float MaxAbsDistance(List<float> source, float center)
    {
        float maxDistance = 0f;

        for (int i = 0; i < source.Count; i++)
        {
            float distance = Mathf.Abs(source[i] - center);
            if (distance > maxDistance)
            {
                maxDistance = distance;
            }
        }

        return maxDistance;
    }
}
