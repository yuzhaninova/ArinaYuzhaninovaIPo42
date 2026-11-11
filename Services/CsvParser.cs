namespace ECommerceCsvImport.Services
{
    public static class CsvParser
    {
        public static IReadOnlyList<string> Parse(string line)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(line)) return result;

            bool inQuotes = false;
            var currentField = new System.Text.StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        currentField.Append('"');
                        i++; 
                    }
                    else
                    {
                        inQuotes = !inQuotes; 
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(currentField.ToString().Trim());
                    currentField.Clear();
                }
                else
                {
                    currentField.Append(c);
                }
            }
            result.Add(currentField.ToString().Trim());
            return result;
        }
    }
}