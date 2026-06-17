using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SQLite;
using UnityEngine;

public class EKGResultDatabase : MonoBehaviour
{
    private SQLiteConnection connection;

    public string DatabasePath { get; private set; }

    public class EKGMeasurementRow
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string PatientId { get; set; }
        public string PatientName { get; set; }
        public string CreatedAt { get; set; }
        public float AverageBpm { get; set; }
        public int SampleCount { get; set; }
        public string SamplesCsv { get; set; }
    }

    public class EKGResult
    {
        public int Id;
        public string PatientId;
        public string PatientName;
        public string CreatedAt;
        public float AverageBpm;
        public List<float> Samples = new List<float>();
    }

    void Awake()
    {
        Open();
    }

    void OnDestroy()
    {
        if (connection != null)
        {
            connection.Close();
            connection.Dispose();
            connection = null;
        }
    }

    public void SaveMeasurement(string patientId, string patientName, float averageBpm, List<float> samples)
    {
        Open();

        if (samples == null || samples.Count < 2)
        {
            Debug.LogWarning("EKGResultDatabase: Målingen blev ikke gemt, fordi der er for få samples.");
            return;
        }

        EKGMeasurementRow row = new EKGMeasurementRow
        {
            PatientId = patientId,
            PatientName = patientName,
            CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            AverageBpm = averageBpm,
            SampleCount = samples.Count,
            SamplesCsv = SerializeSamples(samples)
        };

        connection.Insert(row);
        Debug.Log("EKGResultDatabase: Gemte måling #" + row.Id + " i " + DatabasePath);
    }

    public EKGResult GetLatestMeasurement(string patientId)
    {
        Open();

        EKGMeasurementRow row = connection.Table<EKGMeasurementRow>()
            .Where(measurement => measurement.PatientId == patientId)
            .OrderByDescending(measurement => measurement.Id)
            .FirstOrDefault();

        if (row == null)
        {
            row = connection.Table<EKGMeasurementRow>()
                .OrderByDescending(measurement => measurement.Id)
                .FirstOrDefault();
        }

        if (row == null)
        {
            return null;
        }

        return ToResult(row);
    }

    public List<EKGResult> GetMeasurements(string patientId)
    {
        Open();

        List<EKGMeasurementRow> rows = connection.Table<EKGMeasurementRow>()
            .Where(measurement => measurement.PatientId == patientId)
            .OrderBy(measurement => measurement.Id)
            .ToList();

        List<EKGResult> results = new List<EKGResult>();
        for (int i = 0; i < rows.Count; i++)
        {
            results.Add(ToResult(rows[i]));
        }

        return results;
    }

    public void DeleteMeasurement(int measurementId)
    {
        Open();
        connection.Execute("DELETE FROM EKGMeasurementRow WHERE Id = ?", measurementId);
    }

    public void DeletePatientMeasurements(string patientId)
    {
        Open();
        connection.Execute("DELETE FROM EKGMeasurementRow WHERE PatientId = ?", patientId);
    }

    private EKGResult ToResult(EKGMeasurementRow row)
    {
        return new EKGResult
        {
            Id = row.Id,
            PatientId = row.PatientId,
            PatientName = row.PatientName,
            CreatedAt = row.CreatedAt,
            AverageBpm = row.AverageBpm,
            Samples = DeserializeSamples(row.SamplesCsv)
        };
    }

    private void Open()
    {
        if (connection != null)
        {
            return;
        }

        DatabasePath = Path.Combine(Application.persistentDataPath, "ekg_results.sqlite");
        connection = new SQLiteConnection(DatabasePath);
        connection.CreateTable<EKGMeasurementRow>();
    }

    private string SerializeSamples(List<float> samples)
    {
        StringBuilder builder = new StringBuilder(samples.Count * 8);

        for (int i = 0; i < samples.Count; i++)
        {
            if (i > 0)
            {
                builder.Append(';');
            }

            builder.Append(samples[i].ToString("G9", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private List<float> DeserializeSamples(string samplesCsv)
    {
        List<float> samples = new List<float>();

        if (string.IsNullOrEmpty(samplesCsv))
        {
            return samples;
        }

        string[] parts = samplesCsv.Split(';');
        for (int i = 0; i < parts.Length; i++)
        {
            if (float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out float value))
            {
                samples.Add(value);
            }
        }

        return samples;
    }
}
