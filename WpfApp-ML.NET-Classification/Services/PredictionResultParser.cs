namespace WpfApp_ML.NET_Classification.Services
{
    public sealed class PredictionResult
    {
        public string Label { get; set; }
        public string Score { get; set; }
    }

    public static class PredictionResultParser
    {
        public static PredictionResult Parse(string output)
        {
            string label = null;
            string score = null;
            var fields = (output ?? string.Empty).Split(new[] { ',', '\r', '\n' },
                System.StringSplitOptions.RemoveEmptyEntries);

            foreach (var field in fields)
            {
                var separator = field.IndexOfAny(new[] { ':', '=' });
                if (separator < 0)
                {
                    continue;
                }

                var name = field.Substring(0, separator).Trim();
                var value = field.Substring(separator + 1).Trim();
                var normalizedName = name.Replace(" ", string.Empty).ToLowerInvariant();
                if (normalizedName == "prediction" || normalizedName == "predicted" ||
                    normalizedName == "predictedlabel" || normalizedName == "predictedvalue" ||
                    normalizedName == "класс" || normalizedName == "предсказание")
                {
                    label = value.Trim('"', '\'');
                }
                else if (normalizedName == "probability" || normalizedName == "confidence" ||
                         normalizedName == "score" || normalizedName == "оценка" ||
                         normalizedName == "вероятность" || normalizedName == "уверенность")
                {
                    score = ReadNumber(value);
                }
            }

            return string.IsNullOrEmpty(label) ? null : new PredictionResult { Label = label, Score = score };
        }

        private static string ReadNumber(string value)
        {
            var end = 0;
            while (end < value.Length && (char.IsDigit(value[end]) || "+-.,eE".IndexOf(value[end]) >= 0))
            {
                end++;
            }

            var number = end == 0 ? null : value.Substring(0, end);
            return double.TryParse(number?.Replace(',', '.'), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out _) ? number : null;
        }
    }
}
