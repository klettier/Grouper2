namespace Grouper2.Core.SddlParser
{
    internal static class Format
    {
        public static string Unknown(string input)
        {
            const string unknownString = "Unknown({0})";
            return string.Format(unknownString, input);
        }
    }
}
