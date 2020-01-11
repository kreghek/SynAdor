using System;
using System.IO;
using NAudio.Wave;

namespace SynAdor
{
    class Program
    {
        private const int MAX_TITLE_LENGTH = 30;
        private static WaveFileWriter writer;

        static void Main(string[] args)
        {
            var adrRepositoryPath = "decisions";

            var templateFile = "template.md";

            var waveIn = new WaveInEvent();

            HandleWaveIn(waveIn);

            while (true)
            {
                WriteCommands();
                var command = Console.ReadLine().Trim().ToUpper();

                switch (command)
                {
                    case "C":
                    case "CREATE":
                        ProcessCreation(templateFile, adrRepositoryPath);
                        break;

                    case "S":
                        StartRecord("res.wav", waveIn);
                        break;
                    case "F":
                        FinishRecord(waveIn);
                        break;

                    case "Q":
                    case "QUIT":
                        return;
                }
            }
        }

        private static void HandleWaveIn(WaveInEvent waveIn)
        {
            waveIn.DataAvailable += (s, a) =>
            {
                writer.Write(a.Buffer, 0, a.BytesRecorded);
                if (writer.Position > waveIn.WaveFormat.AverageBytesPerSecond * 30)
                {
                    waveIn.StopRecording();
                }
            };

            waveIn.RecordingStopped += (s, a) =>
            {
                writer?.Dispose();
                writer = null;
            };
        }

        private static void FinishRecord(WaveInEvent waveIn)
        {
            waveIn.StopRecording();
        }

        private static void StartRecord(string outputFilePath, WaveInEvent waveIn)
        {
            writer = new WaveFileWriter(outputFilePath, waveIn.WaveFormat);
            waveIn.StartRecording();
        }

        private static void ProcessCreation(string templateFile, string adrRepositoryPath)
        {
            Console.WriteLine("Title:");
            var title = Console.ReadLine();
            var sanitizedTitle = title.ToLower().Trim().Replace(" ", "_");
            if (sanitizedTitle.Length > MAX_TITLE_LENGTH)
            {
                sanitizedTitle = sanitizedTitle.Substring(0, MAX_TITLE_LENGTH);
            }

            var lastDecisionNum = CalcLastNumber(adrRepositoryPath);

            var decisionNum = lastDecisionNum + 1;

            var decisionFileName = $"{decisionNum:D4}-{sanitizedTitle}.md";
            var decisionFilePath = Path.Combine(adrRepositoryPath, decisionFileName);
            File.Copy(templateFile, decisionFilePath);

            var fileContent = File.ReadAllText(decisionFilePath);
            fileContent = fileContent.Replace("[$TITLE]", title);

            fileContent = fileContent.Replace("[$STATUS]", "accepted");

            Console.WriteLine("Context:");
            var context = Console.ReadLine();
            fileContent = fileContent.Replace("[$CONTEXT]", context);

            Console.WriteLine("Decision:");
            var decisionText = Console.ReadLine();
            fileContent = fileContent.Replace("[$DECISION]", decisionText);

            File.WriteAllText(decisionFilePath, fileContent);
        }

        private static void WriteCommands()
        {
            Console.WriteLine("[C, create] - create new decision");
            Console.WriteLine("[Q, quit] - close application");
            Console.WriteLine("Enter command: ");
        }

        private static int CalcLastNumber(string adrRepositoryPath)
        {
            var adrFiles = Directory.GetFiles(adrRepositoryPath, "*.md");

            return AdrNameHelper.CalcNumber(adrFiles);
        }
    }
}
