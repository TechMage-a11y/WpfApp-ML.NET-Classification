using System;
using System.Text.RegularExpressions;

namespace WpfApp_ML.NET_Classification.Services
{
    public sealed class PredictionResult
    {
        public string Label { get; set; }
        public string Score { get; set; }
    }

    public static class PredictionResultParser
    {
        private static readonly Regex LabelPattern = new Regex(
            @"(?:Prediction|Predicted\s*(?:Label|value)?|PredictedLabel)\s*[:=]\s*(?<label>[^,\r\n]+)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex ProbabilityPattern = new Regex(
            @"(?:Probability|Confidence|Score)\s*[:=]\s*(?<value>[+-]?[0-9]+(?:[.,][0-9]+)?(?:[eE][+-]?[0-9]+)?)",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static PredictionResult Parse(string output)
        {
            var text = output ?? string.Empty;
            var label = LabelPattern.Match(text);
            if (!label.Success)
            {
                label = Regex.Match(text, @"PredictedLabel\s*:\s*(?<label>[^\s,]+)",
                    RegexOptions.IgnoreCase);
            }

            var probability = ProbabilityPattern.Match(text);
            if (!label.Success)
            {
                return null;
            }

            return new PredictionResult
            {
                Label = label.Groups["label"].Value.Trim().Trim('"', '\''),
                Score = probability.Success ? probability.Groups["value"].Value : null
            };
        }
    }
}
