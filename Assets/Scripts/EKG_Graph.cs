using System.Collections.Generic;
using UnityEngine;

public class EKGGraph : MonoBehaviour
{
    public int maxPoints = 300;
    public float xSpacing = 0.02f;

    private LineRenderer line;
    private List<float> values = new List<float>();

    private float smoothValue;

    void Start()
    {
        line = gameObject.AddComponent<LineRenderer>();

        line.startWidth = 0.02f;
        line.endWidth = 0.02f;

        line.material = new Material(Shader.Find("Sprites/Default"));
        line.startColor = Color.green;
        line.endColor = Color.green;

        line.useWorldSpace = false;

        smoothValue = ArduinoReader.currentEKG;
    }

    void Update()
    {
        float raw = ArduinoReader.currentEKG;

        smoothValue = Mathf.Lerp(
            smoothValue,
            raw,
            0.2f
        );

        values.Add(smoothValue);

        if (values.Count > maxPoints)
        {
            values.RemoveAt(0);
        }

        // Find gennemsnit af signalet
        float average = 0f;

        foreach (float v in values)
        {
            average += v;
        }

        average /= values.Count;

        line.positionCount = values.Count;

        for (int i = 0; i < values.Count; i++)
        {
            float x = i * xSpacing;

            // Fjern DC-offset og forstør signalet
            float y = (values[i] - average) * 0.1f;

            line.SetPosition(i, new Vector3(x, y, 0));
        }
    }
}