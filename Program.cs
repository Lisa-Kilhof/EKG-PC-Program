using System;                      // Giver adgang til grundlæggende funktioner som Console.WriteLine
using System.Collections.Generic;  // Giver adgang til lister (List<T>)
using System.Globalization;        // Bruges til at læse decimaltal med punktum
using System.IO;                   // Bruges til at læse filer
using System.Linq;                 // Giver adgang til funktioner som Average()

class Program
{
    static void Main()
    {
        // Navnet på CSV-filen der indeholder EKG-data
        string filePath = "EKG-Data.csv";

        // Liste til tidsværdierne fra CSV-filen
        List<double> time = new List<double>();

        // Liste til spændingsværdierne (EKG-signalet)
        List<double> signal = new List<double>();

        // Gennemløber alle linjer i CSV-filen én ad gangen
        foreach (string line in File.ReadLines(filePath))
        {
            // Springer tomme linjer over
            if (string.IsNullOrWhiteSpace(line))
                continue;

            // Springer header-linjer over
            // WaveForms starter disse linjer med # eller tekst
            if (!char.IsDigit(line[0]) && line[0] != '-')
                continue;

            // Opdeler linjen ved komma
            string[] parts = line.Split(',');

            // Sikrer at der findes mindst 2 kolonner
            if (parts.Length < 2)
                continue;

            // Gemmer tidspunktet i time-listen
            time.Add(
                double.Parse(parts[0], CultureInfo.InvariantCulture)
            );

            // Gemmer spændingen i signal-listen
            signal.Add(
                double.Parse(parts[1], CultureInfo.InvariantCulture)
            );
        }

        // Liste til indeks for fundne R-takker
        List<int> rPeaks = new List<int>();

        // Tærskelværdi
        // R-takkerne ligger omkring 0,69 V i vores måling
        // Derfor vælges 0,4 V som minimum
        double threshold = 0.4;

        // Gennemløber hele signalet
        for (int i = 1; i < signal.Count - 1; i++)
        {
            // Tjekker om punktet:
            // 1. Er over tærsklen
            // 2. Er større end punktet før
            // 3. Er større eller lig punktet efter
            if (signal[i] > threshold &&
                signal[i] > signal[i - 1] &&
                signal[i] >= signal[i + 1])
            {
                // Sørger for at samme hjerteslag
                // ikke registreres flere gange

                if (rPeaks.Count == 0 ||
                    time[i] - time[rPeaks.Last()] > 0.8)
                {
                    // Gemmer placeringen af R-takken
                    rPeaks.Add(i);
                }
            }
        }

        // Udskriver alle fundne R-takker
        Console.WriteLine("R-takker fundet ved:");

        foreach (int peak in rPeaks)
        {
            Console.WriteLine($"{time[peak]:F6} s");
        }

        // Liste til RR-intervaller
        List<double> rrIntervals = new List<double>();

        // Beregner tidsforskellen mellem hver R-tak
        for (int i = 1; i < rPeaks.Count; i++)
        {
            rrIntervals.Add(
                time[rPeaks[i]] - time[rPeaks[i - 1]]
            );
        }

        // Udskriver RR-intervallerne
        Console.WriteLine("\nRR-intervaller:");

        foreach (double rr in rrIntervals)
        {
            Console.WriteLine($"{rr:F6} s");
        }

        // Beregner gennemsnittet af RR-intervallerne
        double averageRR = rrIntervals.Average();

        // Beregner pulsen
        // Puls = 60 / RR-interval
        double pulse = 60.0 / averageRR;

        // Udskriver resultatet
        Console.WriteLine("\nResultat:");
        Console.WriteLine($"Antal R-takker: {rPeaks.Count}");
        Console.WriteLine($"Gennemsnitligt RR-interval: {averageRR:F6} s");
        Console.WriteLine($"Puls: {pulse:F1} BPM");
    }
}