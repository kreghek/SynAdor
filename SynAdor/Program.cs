using System;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;
using OggVorbisEncoder;

namespace SynAdor
{
    class Program
    {
        private const int MAX_TITLE_LENGTH = 30;
        private static WaveFileWriter writer;

        /// <summary>
        /// Получаем по https://console.cloud.yandex.ru/folders
        /// </summary>
        private static string _folderId;

        /// <summary>
        /// Получаем через https://cloud.yandex.ru/docs/iam/operations/iam-token/create
        /// Там ссылка.
        /// </summary>
        private static string _apiToken;

        private static TaskCompletionSource<string> _voiceRecordTcs;

        static void Main(string[] args)
        {
            _folderId = ArgumentHelper.GetProgramArgument(args, "folderid");
            _apiToken = ArgumentHelper.GetProgramArgument(args, "yandexPassportOauthToken");

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
                        ProcessCreation(templateFile, adrRepositoryPath, "res.wav", waveIn);
                        break;

                    case "S":
                        StartRecord("res.wav", waveIn);
                        break;
                    case "F":
                        FinishRecord(waveIn);
                        break;

                    case "A":
                    case "ACTUAL":

                        ProcessReport(adrRepositoryPath);

                        break;

                    case "Q":
                    case "QUIT":
                        return;
                }
            }
        }

        private static void ProcessReport(string adrRepositoryPath, bool onlyActual)
        {
            var adrFiles = Directory.GetFiles(adrRepositoryPath, "*.md");

            var contentTableSb = new StringBuilder();
            var sb = new StringBuilder();

            contentTableSb.AppendLine("# Содержание");
            contentTableSb.AppendLine();
            foreach (var adrFile in adrFiles)
            {
                if (adrFile.ToUpperInvariant().StartsWith("REPORT"))
                {
                    // Игнорируем файлы отчёты, потому что мы и делаем отчёт.
                    continue;
                }

                var fileContent = File.ReadAllText(adrFile);
                var fileContentLines = fileContent.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

                var title = fileContentLines[0].TrimStart('#').Trim();
                var anchor = title.ToLowerInvariant().Replace(" ", "-");
                contentTableSb.AppendLine($"[{title}](#{anchor})");
                contentTableSb.AppendLine();

                sb.AppendLine(fileContent);
                sb.AppendLine();
                sb.AppendLine("-----");
                sb.AppendLine();
            }

            var totalSb = new StringBuilder();
            totalSb.AppendLine(contentTableSb.ToString());
            totalSb.AppendLine();
            totalSb.AppendLine(sb.ToString());

            File.WriteAllText(Path.Combine(adrRepositoryPath, "reports", "report-actual.md"), totalSb.ToString());
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

            waveIn.RecordingStopped += async (s, a) =>
            {
                writer?.Dispose();
                writer = null;

                await RecognizeAsync();
            };
        }

        private static async Task RecognizeAsync()
        {
            var token = string.Empty;
            using (var client = new HttpClient())
            {
                var url = "https://iam.api.cloud.yandex.net/iam/v1/tokens";

                var content = new StringContent("{\"yandexPassportOauthToken\":\"" + _apiToken + "\"}", System.Text.Encoding.UTF8, "application/json");
                var result = client.PostAsync(url, content).Result;

                var resStr = await result.Content.ReadAsStringAsync();

                var options = new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    WriteIndented = true
                };

                var a = JsonSerializer.Deserialize<AuthResult>(resStr, options);

                token = a.iamToken;
            }

            using (var client = new HttpClient())
            {
                var url = $"https://stt.api.cloud.yandex.net/speech/v1/stt:recognize?topic=general&format=lpcm&sampleRateHertz=48000&folderId={_folderId}";

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                client.DefaultRequestHeaders.TransferEncodingChunked = true;

                ConvertAudioResult();

                var data = File.ReadAllBytes("res1.wav");

                var audioContent = new ByteArrayContent(data);

                var result = await client.PostAsync(url, audioContent);

                var resultStr = await result.Content.ReadAsStringAsync();

                Console.WriteLine(resultStr);

                if (_voiceRecordTcs != null)
                {
                    _voiceRecordTcs.SetResult(resultStr);
                }
            }
        }

        private static void ConvertAudioResult()
        {
            using (var reader = new WaveFileReader("res.wav"))
            {
                var outFormat = new WaveFormat(48000, 16, 1);
                using (var conversionStream = new WaveFormatConversionStream(outFormat, reader))
                {
                    WaveFileWriter.CreateWaveFile("res1.wav", conversionStream);
                }
            }
        }

        private class AuthResult {
            public string iamToken { get; set; }
        }

        private static void FinishRecord(WaveInEvent waveIn)
        {
            waveIn.StopRecording();
        }

        private static Task<string> StartRecord(string outputFilePath, WaveInEvent waveIn)
        {
            var format = new WaveFormat(8000, 16, 1);
            writer = new WaveFileWriter(outputFilePath, format);
            waveIn.StartRecording();
            
            _voiceRecordTcs = new TaskCompletionSource<string>();
            var task = _voiceRecordTcs.Task;
            return task;
        }

        private static void ProcessCreation(string templateFile, string adrRepositoryPath, string outputFilePath, WaveInEvent waveIn)
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
            fileContent = fileContent.Replace("[$TITLE]", $"{decisionNum:D4}-{title}");

            fileContent = fileContent.Replace("[$STATUS]", "accepted");

            fileContent = fileContent.Replace("[$CREATEDATE]", DateTime.Now.ToString("d", CultureInfo.GetCultureInfo("ru-RU")));

            Console.WriteLine("Контекст:");
            var context = Console.ReadLine();
            fileContent = fileContent.Replace("[$CONTEXT]", context);

            Console.WriteLine("Решение:");
            var decisionText = Console.ReadLine();
            fileContent = fileContent.Replace("[$DECISION]", decisionText);

            File.WriteAllText(decisionFilePath, fileContent);
        }

        private static void WriteCommands()
        {
            Console.WriteLine("[C, create] - создать новое решение");
            Console.WriteLine("[A, actual] - отчёт по актуальным решениям");
            Console.WriteLine("[Q, quit] - выход");
            Console.WriteLine("Команда: ");
        }

        private static int CalcLastNumber(string adrRepositoryPath)
        {
            var adrFiles = Directory.GetFiles(adrRepositoryPath, "*.md");

            return AdrNameHelper.CalcNumber(adrFiles);
        }
    }
}
