using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using NzbDrone.Common.Extensions;

namespace NzbDrone.Core.DecisionEngine
{
    public static class CatalogueNumber
    {
        // A catalogue number is a short alphabetic studio prefix followed by a
        // serial, optionally separated: ABC-123, ABC123, "ABC 123".
        private static readonly Regex Number = new Regex(@"\b(?<prefix>[a-z]{2,6})[-_ ]?(?<serial>\d{2,5})\b",
                                                         RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // An entry is only identified by catalogue number when its whole title is
        // one. That keeps every caller inert for ordinary titles, which would
        // otherwise look like a catalogue number to the expression above:
        // "Apollo 13" and "Blade Runner 2049" both contain one by that reading.
        private static readonly Regex WholeTitle = new Regex(@"^[a-z]{2,6}-?\d{2,5}$",
                                                             RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static bool IdentifiesTitle(string title)
        {
            return title != null && WholeTitle.IsMatch(title.Trim());
        }

        public static HashSet<string> Parse(string text)
        {
            var numbers = new HashSet<string>();

            if (text.IsNullOrWhiteSpace())
            {
                return numbers;
            }

            foreach (Match match in Number.Matches(text))
            {
                // ABC-123, ABC123 and "ABC 123" are the same number, and so are
                // ABC-12 and ABC-012, so compare on a single normalised form.
                var serial = int.Parse(match.Groups["serial"].Value, CultureInfo.InvariantCulture);

                numbers.Add($"{match.Groups["prefix"].Value.ToUpperInvariant()}-{serial:D3}");
            }

            return numbers;
        }

        /// <summary>
        /// True when the release title names a catalogue number and none of them
        /// is the one identifying the entry -- that is, the release positively
        /// claims to be something else. A release naming no catalogue number
        /// contradicts nothing, and neither does anything at all when the entry
        /// is not identified by a catalogue number in the first place.
        /// </summary>
        public static bool Contradicts(string entryTitle, string releaseTitle)
        {
            if (!IdentifiesTitle(entryTitle))
            {
                return false;
            }

            var releaseNumbers = Parse(releaseTitle);

            return releaseNumbers.Count > 0 && !releaseNumbers.Overlaps(Parse(entryTitle));
        }
    }
}
