using System;
using System.Globalization;
using System.IO.Ports;
using UnityEngine;

public class ArduinoReader : MonoBehaviour
{
    public static int currentEKG = 0;
    public static float currentEKGFloat = 0f;
    public static bool IsConnected { get; private set; }
    public static bool IsMeasuring { get; private set; } = true;
    public static string LastStatus { get; private set; } = "Ikke forbundet";
    public static string MeasurementStatus { get; private set; } = "Måler";
    public static long SamplesReceived { get; private set; }
    public static long MeasurementSamples { get; private set; }

    [Header("Arduino")]
    public string portName = "/dev/cu.usbmodem11301";
    public int baudRate = 19200;
    public int readTimeoutMs = 20;
    public int maxLinesPerFrame = 8;

    [Header("Data format")]
    public string separator = ",";
    public int ekgColumn = 1;

    private SerialPort serial;

    void Start()
    {
        OpenPort();
    }

    void Update()
    {
        if (serial == null || !serial.IsOpen)
        {
            IsConnected = false;
            return;
        }

        for (int i = 0; i < maxLinesPerFrame; i++)
        {
            try
            {
                string line = serial.ReadLine();

                if (TryParseEkg(line, out float value))
                {
                    currentEKGFloat = value;
                    currentEKG = Mathf.RoundToInt(value);
                    SamplesReceived++;

                    if (IsMeasuring)
                    {
                        MeasurementSamples++;
                    }
                    LastStatus = "Forbundet: " + portName;
                }
            }
            catch (TimeoutException)
            {
                break;
            }
            catch (Exception e)
            {
                LastStatus = "Serial fejl: " + e.Message;
                Debug.LogWarning(LastStatus);
                break;
            }
        }
    }

    private void OpenPort()
    {
        try
        {
            serial = new SerialPort(portName, baudRate);
            serial.ReadTimeout = readTimeoutMs;
            serial.NewLine = "\n";
            serial.Open();
            serial.DiscardInBuffer();
            serial.DiscardOutBuffer();

            IsConnected = true;
            LastStatus = "Forbundet: " + portName;
            Debug.Log(LastStatus);
        }
        catch (Exception e)
        {
            IsConnected = false;
            LastStatus = "Kunne ikke aabne " + portName + ": " + e.Message;
            Debug.LogError(LastStatus);
        }
    }

    private bool TryParseEkg(string line, out float value)
    {
        value = 0f;

        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        char splitChar = string.IsNullOrEmpty(separator) ? ',' : separator[0];
        string[] values = line.Trim().Split(splitChar);
        int column = Mathf.Clamp(ekgColumn, 0, values.Length - 1);

        if (values.Length == 1)
        {
            column = 0;
        }

        return float.TryParse(
            values[column],
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value
        );
    }

    void OnDisable()
    {
        ClosePort();
    }

    void OnApplicationQuit()
    {
        ClosePort();
    }

    private void ClosePort()
    {
        if (serial != null && serial.IsOpen)
        {
            serial.Close();
        }

        IsConnected = false;
    }

    public static void StartMeasuring()
    {
        IsMeasuring = true;
        MeasurementStatus = "Måling aktiv";
    }

    public static void StopMeasuring()
    {
        IsMeasuring = false;
        MeasurementStatus = "Måling stoppet";
    }
}
