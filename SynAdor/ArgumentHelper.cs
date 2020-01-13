using System;
using System.Linq;

namespace SynAdor
{
    /// <summary>
    /// Вспомогательный класс для работы с аргументами командной строки
    /// в консольном приложении.
    /// </summary>
    public static class ArgumentHelper
    {
        /// <summary>
        /// Проверяет, присутствует ли аргумент.
        /// </summary>
        /// <param name="args"> Все агрументы приложения. </param>
        /// <param name="testArg"> Проверяемый аргумент. Без учёта регистра. </param>
        /// <returns> Возвращает true, если аргумент присутствует. </returns>
        public static bool HasProgramArgument(string[] args, string testArg)
        {
            return args?.Select(x => x?.Trim().ToLowerInvariant()).Contains(testArg.ToLowerInvariant()) == true;
        }

        /// <summary>
        /// Возвращает значение аргумента.
        /// </summary>
        /// <param name="args"> Все агрументы приложения. </param>
        /// <param name="testArg"> Аргумент, значение которого требуется извлечь. Без учёта регистра. </param>
        /// <returns> Возвращает строковое значение аргумента. Или null, если аргумента нет. </returns>
        public static string GetProgramArgument(string[] args, string testArg)
        {
            return GetProgramArgument(args, testArg, defaultValue: default);
        }

        /// <summary>
        /// Возвращает значение аргумента.
        /// </summary>
        /// <param name="args"> Все агрументы приложения. </param>
        /// <param name="testArg"> Аргумент, значение которого требуется извлечь. Без учёта регистра. </param>
        /// <returns> Возвращает строковое значение аргумента. Или null, если аргумента нет. </returns>
        public static string GetProgramArgument(string[] args, string testArg, string defaultValue)
        {
            foreach (var arg in args)
            {
                var components = arg.Split(new[] { '=' }, StringSplitOptions.RemoveEmptyEntries);
                if (string.Equals(components[0], testArg, StringComparison.InvariantCultureIgnoreCase))
                {
                    if (components.Length >= 2)
                    {
                        if (string.IsNullOrEmpty(components[1]))
                        {
                            return defaultValue;
                        }
                        else
                        {
                            return components[1];
                        }
                    }
                }
            }

            return defaultValue;
        }
    }
}
