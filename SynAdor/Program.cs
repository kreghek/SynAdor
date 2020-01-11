using System;
using System.IO;

namespace SynAdor
{
    class Program
    {
        private const int MAX_TITLE_LENGTH = 30;

        static void Main(string[] args)
        {
            var adrRepositoryPath = "decisions";

            var templateFile = "template.md";

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
                }
            }
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
