using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace backend.Services
{
    public class GuardrailsService : IGuardrailsService
    {
        private static readonly List<(Regex Pattern, string Label)> SensitivePatterns = new()
        {
            (new Regex(@"\b\d+%\s*(off|discount)", RegexOptions.IgnoreCase | RegexOptions.Compiled), "discount percentage"),
            (new Regex(@"\$\d+", RegexOptions.IgnoreCase | RegexOptions.Compiled), "price/amount"),
            (new Regex(@"\b(refund|money.?back)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "refund policy"),
            (new Regex(@"\b(guarantee|guaranteed|warranty)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "guarantee/warranty"),
            (new Regex(@"\b(free\s+(?:trial|plan|tier))\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "free offering"),
            (new Regex(@"\b(SLA|uptime)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "SLA/uptime commitment"),
            (new Regex(@"\b(\d+[\s-]?day|\d+[\s-]?hour).*(?:response|resolution|turnaround)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "response time commitment"),
            (new Regex(@"\b(policy|policies)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "policy reference"),
            (new Regex(@"\b(cancel(?:lation)?.*(?:fee|charge|penalty))\b", RegexOptions.IgnoreCase | RegexOptions.Compiled), "cancellation terms")
        };

        public GuardrailResult CheckResponse(string response, List<string> knowledgeChunks)
        {
            if (string.IsNullOrWhiteSpace(response))
            {
                return new GuardrailResult
                {
                    IsSafe = true,
                    FlaggedTerms = new List<string>(),
                    Recommendation = "Empty response — nothing to check."
                };
            }

            var kbText = string.Join(" ", knowledgeChunks).ToLower();
            var responseLower = response.ToLower();
            var flaggedTerms = new HashSet<string>();

            foreach (var (regex, label) in SensitivePatterns)
            {
                var matches = regex.Matches(responseLower);
                if (matches.Count == 0)
                {
                    continue;
                }

                foreach (Match match in matches)
                {
                    var matchStr = match.Value;
                    if (!string.IsNullOrEmpty(matchStr) && !kbText.Contains(matchStr))
                    {
                        flaggedTerms.Add(label);
                        break;
                    }
                }
            }

            var flaggedList = new List<string>(flaggedTerms);

            if (flaggedList.Count == 0)
            {
                return new GuardrailResult
                {
                    IsSafe = true,
                    FlaggedTerms = flaggedList,
                    Recommendation = "Response appears grounded in knowledge base."
                };
            }

            string recommendation;
            if (flaggedList.Count >= 3)
            {
                recommendation = "High risk of hallucination. Multiple ungrounded claims detected. Recommend escalating to a human agent.";
            }
            else
            {
                recommendation = $"Potential ungrounded claims detected: {string.Join(", ", flaggedList)}. Review before sending or add a disclaimer.";
            }

            return new GuardrailResult
            {
                IsSafe = false,
                FlaggedTerms = flaggedList,
                Recommendation = recommendation
            };
        }
    }
}
