using System.Text.RegularExpressions;

namespace SynAdor
{
    public static class AdrNameHelper
    {
        public static int GetMaxNumber(string[] fileNames)
        {
            var regex = new Regex($"(\\d{{4}})-.+");
            var maxNumber = 0;
            foreach (var file in fileNames)
            {
                var match = regex.Match(file);

                var numberGroup = match.Groups[1];

                var number = int.Parse(numberGroup.Value);

                if (maxNumber < number)
                {
                    maxNumber = number;
                }
            }

            return maxNumber;
        }

        public static string FindByNumber(string[] fileNames, int num)
        {
            var regex = new Regex($"(\\d{{4}})-.+");
            
            foreach (var file in fileNames)
            {
                var match = regex.Match(file);

                var numberGroup = match.Groups[1];

                var number = int.Parse(numberGroup.Value);

                if (num == number)
                {
                    return file;
                }
            }

            return null;
        }
    }
}
