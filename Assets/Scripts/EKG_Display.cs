using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class EKGDisplay : MonoBehaviour
{
    public TextMeshProUGUI ekgText;
    public bool showText = false;

    private readonly List<string> patients = new List<string>();
    private struct GraphSample
    {
        public float time;
        public float value;
    }

    private readonly List<GraphSample> graphValues = new List<GraphSample>();

    private string selectedPatient = "#0001";
    private string patientName = "Patient #0001";
    private string saveStatus = "Ikke gemt";
    private bool showCreatePatient;
    private bool showPatientPicker;
    private bool showResult;
    private int patientCounter = 1;

    private GUIStyle titleStyle;
    private GUIStyle monitorTitleStyle;
    private GUIStyle labelStyle;
    private GUIStyle smallLabelStyle;
    private GUIStyle panelStyle;
    private GUIStyle darkPanelStyle;
    private GUIStyle buttonStyle;
    private GUIStyle activeButtonStyle;
    private GUIStyle dangerButtonStyle;
    private GUIStyle statusBadgeStyle;
    private GUIStyle textFieldStyle;
    private Texture2D panelTexture;
    private Texture2D darkPanelTexture;
    private Texture2D headerTexture;
    private Texture2D lineTexture;

    private float baseline;
    private float filteredValue;
    private float previousHighPassed;
    private float displayRange = 100f;
    private long lastSampleCount = -1;

    private const float GraphWindowSeconds = 8f;
    private const float SecondsPerSmallSquare = 0.2f;
    private const float SecondsPerLargeSquare = 1f;

    void Start()
    {
        patients.Add(selectedPatient);

        if (ekgText != null)
        {
            ekgText.gameObject.SetActive(showText);

            Canvas canvas = ekgText.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                canvas.transform.localScale = Vector3.one;
            }
        }

        baseline = ArduinoReader.currentEKGFloat;
    }

    void Update()
    {
        CaptureGraphSample();

        if (ekgText == null || !showText)
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
        BuildStyles();

        DrawBackground();

        float scale = Mathf.Clamp(Mathf.Min(Screen.width / 1280f, Screen.height / 720f), 0.75f, 1.25f);
        float margin = 24f * scale;
        Rect app = new Rect(margin, margin, Screen.width - margin * 2f, Screen.height - margin * 2f);

        GUI.Box(app, GUIContent.none, darkPanelStyle);

        Rect header = new Rect(app.x, app.y, app.width, 56f * scale);
        GUI.DrawTexture(header, headerTexture);
        GUI.Label(new Rect(header.x + 22f * scale, header.y, 320f * scale, header.height), "EKG MONITOR", monitorTitleStyle);
        GUI.Label(new Rect(header.xMax - 260f * scale, header.y, 230f * scale, header.height), "LIVE USB  |  EDUCATION", smallLabelStyle);

        float leftPanelWidth = 230f * scale;
        float rightPanelWidth = 330f * scale;
        Rect graphPanel = new Rect(app.x + 22f * scale, app.y + 78f * scale, app.width - leftPanelWidth - rightPanelWidth - 66f * scale, app.height - 104f * scale);
        Rect controlPanel = new Rect(graphPanel.xMax + 22f * scale, graphPanel.y, leftPanelWidth, graphPanel.height);
        Rect statusPanel = new Rect(controlPanel.xMax + 22f * scale, graphPanel.y, rightPanelWidth, graphPanel.height);

        GUI.Box(graphPanel, GUIContent.none, panelStyle);
        GUI.Box(controlPanel, GUIContent.none, panelStyle);
        GUI.Box(statusPanel, GUIContent.none, panelStyle);

        Rect pulseRect = new Rect(graphPanel.x + 18f * scale, graphPanel.y + 12f * scale, graphPanel.width - 36f * scale, 44f * scale);
        GUI.Label(pulseRect, "HR  " + EKGAnalyzer.LatestBpm + " BPM", titleStyle);
        GUI.Label(new Rect(pulseRect.xMax - 154f * scale, pulseRect.y + 4f * scale, 140f * scale, 28f * scale), ArduinoReader.IsMeasuring ? "RECORDING" : "STOPPED", statusBadgeStyle);

        Rect graphFrame = new Rect(graphPanel.x + 18f * scale, graphPanel.y + 70f * scale, graphPanel.width - 36f * scale, graphPanel.height - 94f * scale);
        DrawGrid(graphFrame, scale);
        DrawGraph(graphFrame);
        DrawTimeScale(graphFrame, scale);

        GUI.Label(new Rect(controlPanel.x + 16f * scale, controlPanel.y + 14f * scale, controlPanel.width - 32f * scale, 30f * scale), "Kontrol", titleStyle);

        float buttonX = controlPanel.x + 22f * scale;
        float buttonY = controlPanel.y + 62f * scale;
        float buttonW = controlPanel.width - 44f * scale;
        float buttonH = 38f * scale;
        float gap = 8f * scale;

        if (GUI.Button(new Rect(buttonX, buttonY, buttonW, buttonH), "Start måling", ArduinoReader.IsMeasuring ? activeButtonStyle : buttonStyle))
        {
            ArduinoReader.StartMeasuring();
            saveStatus = "Måler";
        }

        if (GUI.Button(new Rect(buttonX, buttonY + (buttonH + gap), buttonW, buttonH), "Stop måling", !ArduinoReader.IsMeasuring ? dangerButtonStyle : buttonStyle))
        {
            ArduinoReader.StopMeasuring();
            saveStatus = "Klar til gem";
        }

        if (GUI.Button(new Rect(buttonX, buttonY + (buttonH + gap) * 2f, buttonW, buttonH), "Opret patient", showCreatePatient ? activeButtonStyle : buttonStyle))
        {
            showCreatePatient = !showCreatePatient;
            showPatientPicker = false;
            showResult = false;
        }

        if (GUI.Button(new Rect(buttonX, buttonY + (buttonH + gap) * 3f, buttonW, buttonH), "Vælg patient", showPatientPicker ? activeButtonStyle : buttonStyle))
        {
            showPatientPicker = !showPatientPicker;
            showCreatePatient = false;
            showResult = false;
        }

        if (GUI.Button(new Rect(buttonX, buttonY + (buttonH + gap) * 4f, buttonW, buttonH), "Vis resultat", showResult ? activeButtonStyle : buttonStyle))
        {
            showResult = !showResult;
            showCreatePatient = false;
            showPatientPicker = false;
            saveStatus = "Resultat vist";
        }

        Rect detailRect = new Rect(controlPanel.x + 16f * scale, buttonY + (buttonH + gap) * 5f + 16f * scale, controlPanel.width - 32f * scale, controlPanel.yMax - (buttonY + (buttonH + gap) * 5f) - 32f * scale);
        DrawDetailPanel(detailRect, scale);

        DrawStatusPanel(statusPanel, scale);
    }

    private void CaptureGraphSample()
    {
        if (ArduinoReader.SamplesReceived == lastSampleCount)
        {
            return;
        }

        lastSampleCount = ArduinoReader.SamplesReceived;

        float raw = ArduinoReader.currentEKGFloat;
        baseline = graphValues.Count == 0 ? raw : Mathf.Lerp(baseline, raw, 0.012f);

        float highPassed = raw - baseline;
        float fastChange = highPassed - previousHighPassed;
        previousHighPassed = highPassed;

        float shapedValue = highPassed + fastChange * 1.4f;
        filteredValue = graphValues.Count == 0
            ? shapedValue
            : Mathf.Lerp(filteredValue, shapedValue, 0.35f);

        graphValues.Add(new GraphSample
        {
            time = Time.time,
            value = filteredValue
        });

        float minTime = Time.time - GraphWindowSeconds;
        while (graphValues.Count > 0 && graphValues[0].time < minTime)
        {
            graphValues.RemoveAt(0);
        }
    }

    private void DrawStatusPanel(Rect statusRect, float scale)
    {
        GUI.Label(new Rect(statusRect.x + 16f * scale, statusRect.y + 14f * scale, statusRect.width - 32f * scale, 30f * scale), "System status", titleStyle);

        string status =
            "Patient: " + selectedPatient + "\n" +
            "Navn: " + patientName + "\n" +
            "Måling: " + ArduinoReader.MeasurementStatus + "\n" +
            "Signal: " + ArduinoReader.currentEKG + "\n" +
            "Puls: " + EKGAnalyzer.LatestBpm + " BPM\n" +
            "Datakvalitet: " + DataQuality() + "\n" +
            "Forbindelse: " + (ArduinoReader.IsConnected ? "Tilsluttet" : "Ikke tilsluttet") + "\n" +
            "Gemmestatus: " + saveStatus + "\n" +
            "Måle-samples: " + ArduinoReader.MeasurementSamples;

        GUI.Label(
            new Rect(statusRect.x + 22f * scale, statusRect.y + 58f * scale, statusRect.width - 44f * scale, statusRect.height - 74f * scale),
            status,
            labelStyle
        );
    }

    private void DrawDetailPanel(Rect rect, float scale)
    {
        GUI.Box(rect, GUIContent.none, darkPanelStyle);

        if (showCreatePatient)
        {
            GUI.Label(new Rect(rect.x + 10f * scale, rect.y + 12f * scale, rect.width - 20f * scale, 24f * scale), "Ny patient", smallLabelStyle);
            GUI.Label(new Rect(rect.x + 10f * scale, rect.y + 46f * scale, rect.width - 20f * scale, 20f * scale), "Navn", smallLabelStyle);
            patientName = GUI.TextField(new Rect(rect.x + 10f * scale, rect.y + 70f * scale, rect.width - 20f * scale, 28f * scale), patientName, textFieldStyle);

            if (GUI.Button(new Rect(rect.x + 10f * scale, rect.y + 120f * scale, rect.width - 20f * scale, 32f * scale), "Gem", buttonStyle))
            {
                patientCounter++;
                selectedPatient = "#" + patientCounter.ToString("0000");
                patients.Add(selectedPatient);
                patientName = "Patient " + selectedPatient;
                saveStatus = "Patient oprettet";
            }
        }
        else if (showPatientPicker)
        {
            GUI.Label(new Rect(rect.x + 10f * scale, rect.y + 12f * scale, rect.width - 20f * scale, 24f * scale), "Patienter", smallLabelStyle);

            for (int i = 0; i < patients.Count; i++)
            {
                Rect buttonRect = new Rect(rect.x + 10f * scale, rect.y + (46f + i * 34f) * scale, rect.width - 20f * scale, 28f * scale);
                if (GUI.Button(buttonRect, patients[i], patients[i] == selectedPatient ? activeButtonStyle : buttonStyle))
                {
                    selectedPatient = patients[i];
                    patientName = "Patient " + selectedPatient;
                    saveStatus = "Patient valgt";
                }
            }
        }
        else if (showResult)
        {
            GUI.Label(new Rect(rect.x + 10f * scale, rect.y + 12f * scale, rect.width - 20f * scale, 24f * scale), "Resultat", smallLabelStyle);
            GUI.Label(
                new Rect(rect.x + 10f * scale, rect.y + 46f * scale, rect.width - 20f * scale, rect.height - 56f * scale),
                "Puls: " + EKGAnalyzer.LatestBpm + "\nLive samples: " + ArduinoReader.SamplesReceived + "\nMåle-samples: " + ArduinoReader.MeasurementSamples + "\nStatus: " + DataQuality(),
                smallLabelStyle
            );
        }
        else
        {
            GUI.Label(
                new Rect(rect.x + 10f * scale, rect.y + 12f * scale, rect.width - 20f * scale, rect.height - 24f * scale),
                "Brug knapperne til at oprette patient, vælge patient eller se resultat.",
                smallLabelStyle
            );
        }
    }

    private void DrawGraph(Rect rect)
    {
        if (graphValues.Count < 2)
        {
            DrawLine(new Vector2(rect.x, rect.center.y), new Vector2(rect.xMax, rect.center.y), new Color(0.2f, 1f, 0.35f), 2f);
            return;
        }

        float now = Time.time;
        float startTime = now - GraphWindowSeconds;
        float center = Average(graphValues);
        float targetRange = Mathf.Max(MaxAbsDistance(graphValues, center), 22f);
        displayRange = Mathf.Lerp(displayRange, targetRange, 0.05f);

        Vector2 previous = Vector2.zero;
        bool hasPrevious = false;

        for (int i = 0; i < graphValues.Count; i++)
        {
            float normalizedTime = Mathf.InverseLerp(startTime, now, graphValues[i].time);
            float x = Mathf.Lerp(rect.x + 8f, rect.xMax - 8f, normalizedTime);
            float normalized = Mathf.Clamp((graphValues[i].value - center) / displayRange, -1f, 1f);
            float y = rect.center.y - normalized * rect.height * 0.42f;
            Vector2 current = new Vector2(x, y);

            if (hasPrevious)
            {
                DrawLine(previous, current, new Color(0.16f, 1f, 0.36f), 2.2f);
            }

            previous = current;
            hasPrevious = true;
        }
    }

    private void BuildStyles()
    {
        if (panelStyle != null)
        {
            return;
        }

        panelTexture = MakeTexture(new Color(0.055f, 0.065f, 0.078f, 0.98f));
        darkPanelTexture = MakeTexture(new Color(0.018f, 0.022f, 0.028f, 0.99f));
        headerTexture = MakeTexture(new Color(0.2f, 0.035f, 0.045f, 1f));
        lineTexture = MakeTexture(Color.white);

        panelStyle = new GUIStyle(GUI.skin.box);
        panelStyle.normal.background = panelTexture;
        panelStyle.border = new RectOffset(8, 8, 8, 8);

        darkPanelStyle = new GUIStyle(GUI.skin.box);
        darkPanelStyle.normal.background = darkPanelTexture;
        darkPanelStyle.border = new RectOffset(8, 8, 8, 8);

        monitorTitleStyle = new GUIStyle(GUI.skin.label);
        monitorTitleStyle.alignment = TextAnchor.MiddleLeft;
        monitorTitleStyle.fontSize = 24;
        monitorTitleStyle.fontStyle = FontStyle.Bold;
        monitorTitleStyle.normal.textColor = new Color(1f, 0.92f, 0.9f);

        titleStyle = new GUIStyle(GUI.skin.label);
        titleStyle.alignment = TextAnchor.MiddleLeft;
        titleStyle.fontSize = 22;
        titleStyle.fontStyle = FontStyle.Bold;
        titleStyle.normal.textColor = new Color(0.35f, 1f, 0.5f);

        labelStyle = new GUIStyle(GUI.skin.label);
        labelStyle.fontSize = 15;
        labelStyle.normal.textColor = new Color(0.88f, 0.91f, 0.92f);
        labelStyle.wordWrap = true;

        smallLabelStyle = new GUIStyle(labelStyle);
        smallLabelStyle.fontSize = 13;
        smallLabelStyle.alignment = TextAnchor.MiddleLeft;

        buttonStyle = new GUIStyle(GUI.skin.button);
        buttonStyle.fontSize = 16;
        buttonStyle.normal.textColor = new Color(0.9f, 0.92f, 0.93f);
        buttonStyle.hover.textColor = new Color(1f, 0.62f, 0.62f);

        activeButtonStyle = new GUIStyle(buttonStyle);
        activeButtonStyle.normal.textColor = new Color(0.35f, 1f, 0.5f);
        activeButtonStyle.fontStyle = FontStyle.Bold;

        dangerButtonStyle = new GUIStyle(buttonStyle);
        dangerButtonStyle.normal.textColor = new Color(1f, 0.34f, 0.34f);
        dangerButtonStyle.fontStyle = FontStyle.Bold;

        statusBadgeStyle = new GUIStyle(GUI.skin.box);
        statusBadgeStyle.alignment = TextAnchor.MiddleCenter;
        statusBadgeStyle.fontSize = 12;
        statusBadgeStyle.fontStyle = FontStyle.Bold;
        statusBadgeStyle.normal.textColor = new Color(1f, 0.82f, 0.82f);

        textFieldStyle = new GUIStyle(GUI.skin.textField);
        textFieldStyle.fontSize = 13;
        textFieldStyle.normal.textColor = Color.white;
    }

    private string DataQuality()
    {
        if (!ArduinoReader.IsConnected)
        {
            return "Ingen";
        }

        if (ArduinoReader.SamplesReceived < 30)
        {
            return "Starter";
        }

        return "God";
    }

    private float Average(List<GraphSample> source)
    {
        float sum = 0f;

        for (int i = 0; i < source.Count; i++)
        {
            sum += source[i].value;
        }

        return sum / source.Count;
    }

    private float MaxAbsDistance(List<GraphSample> source, float center)
    {
        float maxDistance = 0f;

        for (int i = 0; i < source.Count; i++)
        {
            float distance = Mathf.Abs(source[i].value - center);
            if (distance > maxDistance)
            {
                maxDistance = distance;
            }
        }

        return maxDistance;
    }

    private void DrawDashedFrame(Rect rect, Color color)
    {
        Color oldColor = GUI.color;
        GUI.color = color;

        float dash = 10f;
        float gap = 7f;

        for (float x = rect.x; x < rect.xMax; x += dash + gap)
        {
            GUI.DrawTexture(new Rect(x, rect.y, Mathf.Min(dash, rect.xMax - x), 1f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x, rect.yMax, Mathf.Min(dash, rect.xMax - x), 1f), Texture2D.whiteTexture);
        }

        for (float y = rect.y; y < rect.yMax; y += dash + gap)
        {
            GUI.DrawTexture(new Rect(rect.x, y, 1f, Mathf.Min(dash, rect.yMax - y)), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(rect.xMax, y, 1f, Mathf.Min(dash, rect.yMax - y)), Texture2D.whiteTexture);
        }

        GUI.color = oldColor;
    }

    private void DrawBackground()
    {
        Color oldColor = GUI.color;
        GUI.color = new Color(0.008f, 0.01f, 0.014f, 1f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = oldColor;
    }

    private void DrawGrid(Rect rect, float scale)
    {
        Color oldColor = GUI.color;
        GUI.color = new Color(0.08f, 0.2f, 0.13f, 0.34f);

        float smallWidth = rect.width * (SecondsPerSmallSquare / GraphWindowSeconds);
        float largeWidth = rect.width * (SecondsPerLargeSquare / GraphWindowSeconds);

        for (float x = rect.x; x <= rect.xMax; x += smallWidth)
        {
            GUI.DrawTexture(new Rect(x, rect.y, 1f, rect.height), Texture2D.whiteTexture);
        }

        float smallHeight = 24f * scale;
        for (float y = rect.y; y <= rect.yMax; y += smallHeight)
        {
            GUI.DrawTexture(new Rect(rect.x, y, rect.width, 1f), Texture2D.whiteTexture);
        }

        GUI.color = new Color(0.18f, 0.42f, 0.25f, 0.48f);
        for (float x = rect.x; x <= rect.xMax; x += largeWidth)
        {
            GUI.DrawTexture(new Rect(x, rect.y, 2f, rect.height), Texture2D.whiteTexture);
        }

        float largeHeight = smallHeight * 5f;
        for (float y = rect.y; y <= rect.yMax; y += largeHeight)
        {
            GUI.DrawTexture(new Rect(rect.x, y, rect.width, 2f), Texture2D.whiteTexture);
        }

        GUI.color = oldColor;
    }

    private void DrawTimeScale(Rect rect, float scale)
    {
        string label = SecondsPerSmallSquare.ToString("0.0") + " s/small  |  " +
            SecondsPerLargeSquare.ToString("0") + " s/large  |  " +
            GraphWindowSeconds.ToString("0") + " s window";

        GUI.Label(
            new Rect(rect.x + 8f, rect.yMax - 24f * scale, rect.width - 16f, 20f * scale),
            label,
            smallLabelStyle
        );
    }

    private void DrawLine(Vector2 start, Vector2 end, Color color, float width)
    {
        Matrix4x4 oldMatrix = GUI.matrix;
        Color oldColor = GUI.color;

        Vector2 direction = end - start;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        GUI.color = color;
        GUIUtility.RotateAroundPivot(angle, start);
        GUI.DrawTexture(new Rect(start.x, start.y - width * 0.5f, direction.magnitude, width), lineTexture);

        GUI.matrix = oldMatrix;
        GUI.color = oldColor;
    }

    private Texture2D MakeTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }
}
