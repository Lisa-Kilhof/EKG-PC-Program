using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class EKGDisplay : MonoBehaviour
{
    public TextMeshProUGUI ekgText;
    public bool showText = false;

    private class PatientInfo
    {
        public string Id;
        public string Name;
    }

    private readonly List<PatientInfo> patients = new List<PatientInfo>();
    private struct GraphSample
    {
        public float time;
        public float value;
    }

    private readonly List<GraphSample> graphValues = new List<GraphSample>();
    private readonly List<float> activeMeasurementSamples = new List<float>();
    private readonly List<int> activeBpmSamples = new List<int>();
    private readonly List<float> resultSamples = new List<float>();
    private readonly List<EKGResultDatabase.EKGResult> visibleResults = new List<EKGResultDatabase.EKGResult>();

    private string selectedPatient = "#0001";
    private string patientName = "Matti";
    private string newPatientName = "Matti";
    private string saveStatus = "Ikke gemt";
    private string resultSummary = "Ingen gemt måling endnu.";
    private bool showCreatePatient;
    private bool showPatientPicker;
    private bool showResult;
    private int patientCounter = 1;
    private int selectedResultId = -1;
    private int resultPage;
    private float selectedResultAverageBpm;
    private EKGResultDatabase resultDatabase;

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
    private const int ResultsPerPage = 4;

    void Start()
    {
        patients.Add(new PatientInfo
        {
            Id = selectedPatient,
            Name = patientName
        });
        resultDatabase = GetComponent<EKGResultDatabase>();
        if (resultDatabase == null)
        {
            resultDatabase = gameObject.AddComponent<EKGResultDatabase>();
        }

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
        string hrLabel = showResult && selectedResultId >= 0
            ? "AVG HR  " + Mathf.RoundToInt(selectedResultAverageBpm) + " BPM"
            : "HR  " + EKGAnalyzer.LatestBpm + " BPM";
        string badgeLabel = showResult ? "RESULTAT" : (ArduinoReader.IsMeasuring ? "RECORDING" : "STOPPED");
        GUI.Label(pulseRect, hrLabel, titleStyle);
        GUI.Label(new Rect(pulseRect.xMax - 154f * scale, pulseRect.y + 4f * scale, 140f * scale, 28f * scale), badgeLabel, statusBadgeStyle);

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
            StartNewMeasurement();
            ArduinoReader.StartMeasuring();
            saveStatus = "Måler";
        }

        if (GUI.Button(new Rect(buttonX, buttonY + (buttonH + gap), buttonW, buttonH), "Stop måling", !ArduinoReader.IsMeasuring ? dangerButtonStyle : buttonStyle))
        {
            ArduinoReader.StopMeasuring();
            SaveCurrentMeasurement();
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
            LoadLatestResult();
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

        if (ArduinoReader.IsMeasuring)
        {
            activeMeasurementSamples.Add(filteredValue);

            if (EKGAnalyzer.LatestBpm > 0)
            {
                activeBpmSamples.Add(EKGAnalyzer.LatestBpm);
            }
        }

        float minTime = Time.time - GraphWindowSeconds;
        while (graphValues.Count > 0 && graphValues[0].time < minTime)
        {
            graphValues.RemoveAt(0);
        }
    }

    private void DrawStatusPanel(Rect statusRect, float scale)
    {
        GUI.Label(new Rect(statusRect.x + 16f * scale, statusRect.y + 14f * scale, statusRect.width - 32f * scale, 30f * scale), showResult ? "Resultat oversigt" : "System status", titleStyle);

        string status = showResult
            ? ResultStatusText()
            : "Patient: " + patientName + "\n" +
                "ID: " + selectedPatient + "\n" +
                "Måling: " + ArduinoReader.MeasurementStatus + "\n" +
                "Rå signalværdi: " + ArduinoReader.currentEKG + "\n" +
                "Live puls: " + EKGAnalyzer.LatestBpm + " BPM\n" +
                "Signalstatus: " + DataQuality() + "\n" +
                "Forbindelse: " + (ArduinoReader.IsConnected ? "Tilsluttet" : "Ikke tilsluttet") + "\n" +
                "Gemmestatus: " + saveStatus + "\n" +
                "Samples i måling: " + ArduinoReader.MeasurementSamples;

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
            newPatientName = GUI.TextField(new Rect(rect.x + 10f * scale, rect.y + 70f * scale, rect.width - 20f * scale, 28f * scale), newPatientName, textFieldStyle);

            if (GUI.Button(new Rect(rect.x + 10f * scale, rect.y + 120f * scale, rect.width - 20f * scale, 32f * scale), "Gem", buttonStyle))
            {
                patientCounter++;
                selectedPatient = "#" + patientCounter.ToString("0000");
                patientName = string.IsNullOrWhiteSpace(newPatientName) ? "Patient " + selectedPatient : newPatientName.Trim();
                patients.Add(new PatientInfo
                {
                    Id = selectedPatient,
                    Name = patientName
                });
                newPatientName = "";
                saveStatus = "Patient oprettet";
            }
        }
        else if (showPatientPicker)
        {
            GUI.Label(new Rect(rect.x + 10f * scale, rect.y + 12f * scale, rect.width - 20f * scale, 24f * scale), "Patienter", smallLabelStyle);

            int maxPatients = Mathf.Min(patients.Count, 3);
            for (int i = 0; i < maxPatients; i++)
            {
                Rect buttonRect = new Rect(rect.x + 10f * scale, rect.y + (46f + i * 34f) * scale, rect.width - 20f * scale, 28f * scale);
                if (GUI.Button(buttonRect, patients[i].Name, patients[i].Id == selectedPatient ? activeButtonStyle : buttonStyle))
                {
                    selectedPatient = patients[i].Id;
                    patientName = patients[i].Name;
                    saveStatus = "Patient valgt";
                }
            }

            if (GUI.Button(new Rect(rect.x + 10f * scale, rect.yMax - 42f * scale, rect.width - 20f * scale, 30f * scale), "Slet patient", dangerButtonStyle))
            {
                DeleteSelectedPatient();
            }
        }
        else if (showResult)
        {
            GUI.Label(new Rect(rect.x + 10f * scale, rect.y + 12f * scale, rect.width - 20f * scale, 24f * scale), "Resultat", smallLabelStyle);

            if (visibleResults.Count == 0)
            {
                GUI.Label(
                    new Rect(rect.x + 10f * scale, rect.y + 46f * scale, rect.width - 20f * scale, rect.height - 56f * scale),
                    resultSummary,
                    smallLabelStyle
                );
            }
            else
            {
                int startIndex = Mathf.Clamp(resultPage * ResultsPerPage, 0, Mathf.Max(visibleResults.Count - 1, 0));
                int maxResults = Mathf.Min(visibleResults.Count - startIndex, ResultsPerPage);
                for (int i = 0; i < maxResults; i++)
                {
                    int resultIndex = startIndex + i;
                    EKGResultDatabase.EKGResult result = visibleResults[resultIndex];
                    Rect resultRect = new Rect(rect.x + 10f * scale, rect.y + (42f + i * 34f) * scale, rect.width - 20f * scale, 28f * scale);
                    string label = "#" + (resultIndex + 1) + "  |  " + Mathf.RoundToInt(result.AverageBpm) + " BPM";
                    if (GUI.Button(resultRect, label, result.Id == selectedResultId ? activeButtonStyle : buttonStyle))
                    {
                        SelectResult(result);
                    }
                }

                if (visibleResults.Count > ResultsPerPage)
                {
                    float pageButtonWidth = (rect.width - 30f * scale) * 0.5f;
                    Rect previousRect = new Rect(rect.x + 10f * scale, rect.yMax - 78f * scale, pageButtonWidth, 28f * scale);
                    Rect nextRect = new Rect(previousRect.xMax + 10f * scale, previousRect.y, pageButtonWidth, previousRect.height);

                    if (GUI.Button(previousRect, "Forrige", buttonStyle))
                    {
                        resultPage = Mathf.Max(0, resultPage - 1);
                    }

                    if (GUI.Button(nextRect, "Næste", buttonStyle))
                    {
                        int maxPage = Mathf.Max(0, (visibleResults.Count - 1) / ResultsPerPage);
                        resultPage = Mathf.Min(maxPage, resultPage + 1);
                    }
                }

                if (GUI.Button(new Rect(rect.x + 10f * scale, rect.yMax - 42f * scale, rect.width - 20f * scale, 30f * scale), "Slet resultat", dangerButtonStyle))
                {
                    DeleteSelectedResult();
                }
            }
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
        if (showResult && resultSamples.Count > 1)
        {
            DrawResultGraph(rect);
            return;
        }

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

    private void DrawResultGraph(Rect rect)
    {
        float center = AverageValues(resultSamples);
        float range = Mathf.Max(MaxAbsDistanceValues(resultSamples, center), 22f);
        Vector2 previous = Vector2.zero;

        for (int i = 0; i < resultSamples.Count; i++)
        {
            float x = Mathf.Lerp(rect.x + 8f, rect.xMax - 8f, i / (float)(resultSamples.Count - 1));
            float normalized = Mathf.Clamp((resultSamples[i] - center) / range, -1f, 1f);
            float y = rect.center.y - normalized * rect.height * 0.42f;
            Vector2 current = new Vector2(x, y);

            if (i > 0)
            {
                DrawLine(previous, current, new Color(1f, 0.28f, 0.28f), 2.2f);
            }

            previous = current;
        }
    }

    private void StartNewMeasurement()
    {
        activeMeasurementSamples.Clear();
        activeBpmSamples.Clear();
        resultSamples.Clear();
        visibleResults.Clear();
        selectedResultId = -1;
        resultPage = 0;
        selectedResultAverageBpm = 0f;
        resultSummary = "Måling i gang.";
        showResult = false;
    }

    private void SaveCurrentMeasurement()
    {
        if (activeMeasurementSamples.Count < 2)
        {
            saveStatus = "Ingen måling gemt";
            resultSummary = "Der er ikke nok data til at gemme en måling.";
            return;
        }

        float averageBpm = AverageBpm();
        resultDatabase.SaveMeasurement(selectedPatient, patientName, averageBpm, new List<float>(activeMeasurementSamples));
        saveStatus = "Måling gemt";
        resultSummary =
            "Seneste måling gemt\n" +
            "Patient: " + patientName + "\n" +
            "ID: " + selectedPatient + "\n" +
            "Gennemsnitspuls: " + Mathf.RoundToInt(averageBpm) + " BPM\n" +
            "Samples: " + activeMeasurementSamples.Count;
    }

    private void LoadLatestResult()
    {
        visibleResults.Clear();
        resultSamples.Clear();
        selectedResultId = -1;
        resultPage = 0;
        selectedResultAverageBpm = 0f;

        visibleResults.AddRange(resultDatabase.GetMeasurements(selectedPatient));

        if (visibleResults.Count == 0)
        {
            resultSummary = "Der er ikke gemt en måling endnu.";
            saveStatus = "Intet resultat";
            return;
        }

        resultPage = Mathf.Max(0, (visibleResults.Count - 1) / ResultsPerPage);
        SelectResult(visibleResults[visibleResults.Count - 1]);
    }

    private void SelectResult(EKGResultDatabase.EKGResult result)
    {
        if (result == null)
        {
            return;
        }

        resultSamples.Clear();
        resultSamples.AddRange(result.Samples);
        selectedResultId = result.Id;
        selectedResultAverageBpm = result.AverageBpm;
        selectedPatient = result.PatientId;
        patientName = result.PatientName;
        RememberPatient(selectedPatient, patientName);
        saveStatus = "Resultat vist";
        resultSummary =
            "#" + ResultNumberFor(result) + "\n" +
            "Tid: " + result.CreatedAt + "\n" +
            "Patient: " + result.PatientName + "\n" +
            "ID: " + result.PatientId + "\n" +
            "Gennemsnitspuls: " + Mathf.RoundToInt(result.AverageBpm) + " BPM\n" +
            "Samples: " + result.Samples.Count;
    }

    private void DeleteSelectedResult()
    {
        if (selectedResultId < 0)
        {
            resultSummary = "Vælg et resultat først.";
            return;
        }

        resultDatabase.DeleteMeasurement(selectedResultId);
        saveStatus = "Resultat slettet";
        LoadLatestResult();
    }

    private void DeleteSelectedPatient()
    {
        if (patients.Count <= 1)
        {
            saveStatus = "Kan ikke slette sidste patient";
            return;
        }

        string deletedPatient = patientName;
        resultDatabase.DeletePatientMeasurements(selectedPatient);

        for (int i = patients.Count - 1; i >= 0; i--)
        {
            if (patients[i].Id == selectedPatient)
            {
                patients.RemoveAt(i);
            }
        }

        selectedPatient = patients[0].Id;
        patientName = patients[0].Name;
        resultSamples.Clear();
        visibleResults.Clear();
        selectedResultId = -1;
        resultPage = 0;
        selectedResultAverageBpm = 0f;
        saveStatus = "Patient slettet";
        resultSummary = deletedPatient + " og patientens resultater er slettet.";
    }

    private string ResultStatusText()
    {
        if (visibleResults.Count == 0 || selectedResultId < 0)
        {
            return "Ingen gemte resultater for\n" + patientName + ".";
        }

        return resultSummary + "\n" +
            "Antal resultater: " + visibleResults.Count + "\n" +
            "Status: Klar til gennemgang";
    }

    private int ResultNumberFor(EKGResultDatabase.EKGResult result)
    {
        for (int i = 0; i < visibleResults.Count; i++)
        {
            if (visibleResults[i].Id == result.Id)
            {
                return i + 1;
            }
        }

        return 1;
    }

    private void RememberPatient(string id, string name)
    {
        for (int i = 0; i < patients.Count; i++)
        {
            if (patients[i].Id == id)
            {
                patients[i].Name = name;
                return;
            }
        }

        patients.Add(new PatientInfo
        {
            Id = id,
            Name = name
        });
    }

    private float AverageBpm()
    {
        if (activeBpmSamples.Count == 0)
        {
            return EKGAnalyzer.LatestBpm;
        }

        float sum = 0f;
        for (int i = 0; i < activeBpmSamples.Count; i++)
        {
            sum += activeBpmSamples[i];
        }

        return sum / activeBpmSamples.Count;
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

    private float AverageValues(List<float> source)
    {
        float sum = 0f;

        for (int i = 0; i < source.Count; i++)
        {
            sum += source[i];
        }

        return sum / source.Count;
    }

    private float MaxAbsDistanceValues(List<float> source, float center)
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
