using System.Text;

namespace Monexia.Helpers
{
    public static class CommonHelper
    {
        // PascalCase veya camelCase stringi insan okunur hale getirir
        public static string ToHumanReadable(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;
            var sb = new StringBuilder();
            sb.Append(input[0]);
            for (int i = 1; i < input.Length; i++)
            {
                if (char.IsUpper(input[i]) && !char.IsWhiteSpace(input[i - 1]))
                    sb.Append(' ');
                sb.Append(input[i]);
            }
            return sb.ToString();
        }
    }
}