//Hovedforfatter(e) Lisa, Amarinder, Matti og Benin
//Sekundærforfatter(e) 
using System;
using System.Globalization;
using System.IO.Ports;
using UnityEngine;

// Læser live EKG-tal fra Arduinoens serielle USB-port.
public class ArduinoReader : MonoBehaviour
{
    // Seneste EKG-værdi som heltal til UI og som float til beregning.
    public static int currentEKG = 0;
    public static float currentEKGFloat = 0f;

    // Statusfelter bruges af EKGDisplay til at vise forbindelse og måling.
    public static bool IsConnected { get; private set; }
    public static bool IsMeasuring { get; private set; } = true;
    public static string LastStatus { get; private set; } = "Ikke forbundet";
    public static string MeasurementStatus { get; private set; } = "Måler";
    public static long SamplesReceived { get; private set; }

    // Portindstillinger kan ændres i Unity Inspector.
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
        // Prøv at åbne USB/serial-porten, når scenen starter.
        OpenPort();
    }

    void Update()
    {
        // Hvis porten ikke er åben, kan programmet ikke modtage live-data.
        if (serial == null || !serial.IsOpen)
        {
            IsConnected = false;
            return;
        }

        // Læs nogle få linjer pr. frame, så Unity ikke fryser ved mange Arduino-data.
        for (int i = 0; i < maxLinesPerFrame; i++)
        {
            try
            {
                // Læs én tekstlinje fra Arduinoen.
                string line = serial.ReadLine();

                // Hvis linjen kan tolkes som tal, opdateres live-signalet.
                if (TryParseEkg(line, out float value))
                {
                    currentEKGFloat = value;
                    currentEKG = Mathf.RoundToInt(value);
                    SamplesReceived++;
                    LastStatus = "Forbundet: " + portName;
                }
            }
            catch (TimeoutException)
            {
                // Timeout betyder bare, at der ikke var flere linjer lige nu.
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
        // Opret forbindelse til Arduinoens USB-port.
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
        // Standardværdi hvis parsing fejler.
        value = 0f;

        // Tomme linjer ignoreres.
        if (string.IsNullOrWhiteSpace(line))
        {
            return false;
        }

        // Split linjen, fx "tid,ekg", og vælg den kolonne som indeholder EKG-værdien.
        char splitChar = string.IsNullOrEmpty(separator) ? ',' : separator[0];
        string[] values = line.Trim().Split(splitChar);
        int column = Mathf.Clamp(ekgColumn, 0, values.Length - 1);

        // Hvis Arduino kun sender ét tal pr. linje, bruges det direkte.
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
        // Luk porten pænt, så Arduinoen kan bruges igen bagefter.
        if (serial != null && serial.IsOpen)
        {
            serial.Close();
        }

        IsConnected = false;
    }

    public static void StartMeasuring()
    {
        // Bruges af Start-knappen i UI.
        IsMeasuring = true;
        MeasurementStatus = "Måling aktiv";
    }

    public static void StopMeasuring()
    {
        // Bruges af Stop-knappen i UI.
        IsMeasuring = false;
        MeasurementStatus = "Måling stoppet";
    }
}
