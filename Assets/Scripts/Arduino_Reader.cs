using System;
using System.IO.Ports;
using UnityEngine;

public class ArduinoReader : MonoBehaviour
{
    public static int currentEKG = 0;

    private SerialPort serial;

    void Start()
    {
        serial = new SerialPort("COM5", 19200);
        serial.ReadTimeout = 50;

        try
        {
            serial.Open();

            serial.DiscardInBuffer();
            serial.DiscardOutBuffer();

            Debug.Log("COM5 åbnet");
        }
        catch (Exception e)
        {
            Debug.LogError(e.Message);
        }
    }

    void Update()
    {
        if (serial == null || !serial.IsOpen)
            return;

        try
        {
            string data = serial.ReadLine();

            string[] values = data.Split(',');

            if (values.Length >= 2)
            {
                currentEKG = int.Parse(values[1]);
                Debug.Log(currentEKG);
                if (Time.frameCount % 60 == 0)
                {
                    Debug.Log("EKG = " + currentEKG);
                }
            }
        }
        catch
        {
        }
    }

    void OnApplicationQuit()
    {
        if (serial != null && serial.IsOpen)
        {
            serial.Close();
        }
    }
}