using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SQLite;
using UnityEngine;

// Gemmer EKG-målinger lokalt i en SQLite-database.
public class EKGResultDatabase : MonoBehaviour
{
    // SQLite-forbindelsen til den lokale databasefil.
    private SQLiteConnection connection;

    // Sikrer at migration kun køres én gang pr. runtime.
    private bool migrated;

    // Viser hvor databasen fysisk ligger på computeren.
    public string DatabasePath { get; private set; }

    // Denne klasse svarer til en række i SQLite-tabellen.
    public class EKGMeasurementRow
    {
        [PrimaryKey, AutoIncrement] // SQLite laver selv et nyt ID.
        public int Id { get; set; }
        public string PatientId { get; set; }
        public string PatientName { get; set; }
        public string PatientAge { get; set; }
        public string PatientGender { get; set; }
        public string CreatedAt { get; set; }
        public float AverageBpm { get; set; }
        public float DurationSeconds { get; set; }
        public int SampleCount { get; set; }
        public string SamplesCsv { get; set; }
    }

    // Denne klasse bruges af UI'et, når resultater læses ud af databasen.
    public class EKGResult
    {
        public int Id;
        public string PatientId;
        public string PatientName;
        public string PatientAge;
        public string PatientGender;
        public string CreatedAt;
        public float AverageBpm;
        public float DurationSeconds;
        public List<float> Samples = new List<float>();
    }

    void Awake()
    {
        // Åbn databasen så snart komponenten starter.
        Open();
    }

    void OnDestroy()
    {
        // Luk databaseforbindelsen pænt, når Unity stopper eller objektet fjernes.
        if (connection != null)
        {
            connection.Close();
            connection.Dispose();
            connection = null;
        }
    }

    public void SaveMeasurement(string patientId, string patientName, string patientAge, string patientGender, float averageBpm, float durationSeconds, List<float> samples)
    {
        // Sørg for at databasen er klar, før vi gemmer.
        Open();

        // En måling uden nok punkter giver ingen brugbar graf.
        if (samples == null || samples.Count < 2)
        {
            Debug.LogWarning("EKGResultDatabase: Målingen blev ikke gemt, fordi der er for få samples.");
            return;
        }

        // Pak patientdata, puls, varighed og EKG-kurve i én database-række.
        EKGMeasurementRow row = new EKGMeasurementRow
        {
            PatientId = patientId,
            PatientName = patientName,
            PatientAge = patientAge,
            PatientGender = patientGender,
            CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            AverageBpm = averageBpm,
            DurationSeconds = durationSeconds,
            SampleCount = samples.Count,
            SamplesCsv = SerializeSamples(samples)
        };

        connection.Insert(row);
    }

    public List<EKGResult> GetMeasurements(string patientId)
    {
        Open();

        // Resultater hentes i ældste-til-nyeste rækkefølge, så UI kan vise #1, #2, #3.
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
        // Slet én specifik måling.
        Open();
        connection.Execute("DELETE FROM EKGMeasurementRow WHERE Id = ?", measurementId);
    }

    public void DeletePatientMeasurements(string patientId)
    {
        // Slet alle målinger, der hører til en bestemt patient.
        Open();
        connection.Execute("DELETE FROM EKGMeasurementRow WHERE PatientId = ?", patientId);
    }

    private EKGResult ToResult(EKGMeasurementRow row)
    {
        // Konverter database-rækken til en nemmere UI-model.
        return new EKGResult
        {
            Id = row.Id,
            PatientId = row.PatientId,
            PatientName = row.PatientName,
            PatientAge = row.PatientAge,
            PatientGender = row.PatientGender,
            CreatedAt = row.CreatedAt,
            AverageBpm = row.AverageBpm,
            DurationSeconds = row.DurationSeconds,
            Samples = DeserializeSamples(row.SamplesCsv)
        };
    }

    private void Open()
    {
        if (connection != null)
        {
            return;
        }

        // Application.persistentDataPath er Unitys sikre mappe til lokale gemte data.
        DatabasePath = Path.Combine(Application.persistentDataPath, "ekg_results.sqlite");
        connection = new SQLiteConnection(DatabasePath);
        connection.CreateTable<EKGMeasurementRow>();
        MigrateColumns();
    }

    private void MigrateColumns()
    {
        // Tilføjer nye kolonner til gamle databaser uden at slette gamle data.
        if (migrated)
        {
            return;
        }

        migrated = true;
        TryAddColumn("PatientAge");
        TryAddColumn("PatientGender");
        TryAddColumn("DurationSeconds", "REAL");
    }

    private void TryAddColumn(string columnName, string columnType = "TEXT")
    {
        try
        {
            // SQLite fejler hvis kolonnen allerede findes; derfor er try/catch nok her.
            connection.Execute("ALTER TABLE EKGMeasurementRow ADD COLUMN " + columnName + " " + columnType);
        }
        catch
        {
            // Kolonnen findes allerede i databasen.
        }
    }

    private string SerializeSamples(List<float> samples)
    {
        // Listen gemmes som tekst, fordi SQLite-rækken kun har ét felt til kurvedata.
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
        // Gør den gemte tekst tilbage til en liste af EKG-værdier.
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
