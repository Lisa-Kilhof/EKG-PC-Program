using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class EKGGraph : MonoBehaviour
{
    public int maxPoints = 500;
    public float graphWidth = 12f;
    public float verticalScale = 0.01f;
    public float smoothing = 0.15f;
    public bool autoCenter = true;
    public Color lineColor = new Color(0.2f, 1f, 0.35f);

    private readonly List<float> values = new List<float>();
    private LineRenderer line;
    private float smoothValue;
    private long lastSampleCount = -1;

    void Awake()
    {
        line = GetComponent<LineRenderer>();
        if (line == null)
        {
            line = gameObject.AddComponent<LineRenderer>();
        }

        line.useWorldSpace = false;
        line.positionCount = 0;
        line.startWidth = 0.035f;
        line.endWidth = 0.035f;
        line.numCapVertices = 2;
        line.numCornerVertices = 2;
        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = lineColor;
        line.endColor = lineColor;
    }

    void Start()
    {
        smoothValue = ArduinoReader.currentEKGFloat;
    }

    void Update()
    {
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
        smoothValue = values.Count == 0 ? raw : Mathf.Lerp(smoothValue, raw, smoothing);
        values.Add(smoothValue);

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
        float xStep = values.Count > 1 ? graphWidth / (values.Count - 1) : 0f;

        line.positionCount = values.Count;

        for (int i = 0; i < values.Count; i++)
        {
            float x = (i * xStep) - (graphWidth * 0.5f);
            float y = (values[i] - center) * verticalScale;
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
}
