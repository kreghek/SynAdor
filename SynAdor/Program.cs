using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
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

                var content = new StringContent("{\"yandexPassportOauthToken\":\"{" + _apiToken + "}\"}", System.Text.Encoding.UTF8, "application/json");
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
                var url = $"https://stt.api.cloud.yandex.net/speech/v1/stt:recognize?topic=general&folderId={_folderId}";

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                client.DefaultRequestHeaders.TransferEncodingChunked = true;

                ConvertAudioResult();

                var data = File.ReadAllBytes("res.ogg");

                var audioContent = new ByteArrayContent(data);

                var result = await client.PostAsync(url, audioContent);

                var resultStr = await result.Content.ReadAsStringAsync();

                Console.WriteLine(resultStr);
            }
        }

        private static void ConvertAudioResult()
        {
            var pcmBytes = File.ReadAllBytes("res.wav");
            var oggBytes = ConvertRawPCMFile(44100, 2, pcmBytes, PCMSample.SixteenBit, 44100, 2);
            File.WriteAllBytes("res.ogg", oggBytes);
        }

        private class AuthResult {
            public string iamToken { get; set; }
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
