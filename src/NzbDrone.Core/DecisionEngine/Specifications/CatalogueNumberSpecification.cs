using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine.Specifications
{
    public class CatalogueNumberSpecification : IDownloadDecisionEngineSpecification
    {
        // A catalogue number is a short alphabetic studio prefix followed by a
        // serial, optionally separated: ABC-123, ABC123, "ABC 123".
        private static readonly Regex CatalogueNumber = new Regex(@"\b(?<prefix>[a-z]{2,6})[-_ ]?(?<serial>\d{2,5})\b",
                                                                 RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // A movie is only identified by catalogue number when its whole title is
        // one. That keeps this specification inert for ordinary movies, whose
        // titles ("Apollo 13", "Blade Runner 2049") would otherwise look like a
        // catalogue number to the regex above.
        private static readonly Regex CatalogueTitle = new Regex(@"^[a-z]{2,6}-?\d{2,5}$",
                                                                 RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private readonly Logger _logger;

        public CatalogueNumberSpecification(Logger logger)
        {
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public DownloadSpecDecision IsSatisfiedBy(RemoteMovie subject, SearchCriteriaBase searchCriteria)
        {
            // An indexer that echoes the queried tmdbid back onto every result it
            // returns makes ParsingService match all of them to the searched movie
            // as MovieMatchType.Id, however unrelated the release actually is. The
            // id then carries no information, so the release title has to be the
            // one deciding what the release is: a release that names a catalogue
            // number is asserting its own identity, and if that identity is not
            // the movie's then it is a different film no matter what id came
            // attached to it.
            var movieTitle = subject?.Movie?.Title;

            if (movieTitle == null || !CatalogueTitle.IsMatch(movieTitle.Trim()))
            {
                return DownloadSpecDecision.Accept();
            }

            var movieNumbers = ParseCatalogueNumbers(movieTitle);
            var releaseNumbers = ParseCatalogueNumbers(subject.Release?.Title);

            // A release that names no catalogue number contradicts nothing, so it
            // is left to the other specifications to judge.
            if (releaseNumbers.Count == 0 || releaseNumbers.Overlaps(movieNumbers))
            {
                return DownloadSpecDecision.Accept();
            }

            _logger.Debug("Release '{0}' is catalogue number {1}, but '{2}' is {3}",
                          subject.Release.Title,
                          string.Join("/", releaseNumbers),
                          movieTitle,
                          string.Join("/", movieNumbers));

            return DownloadSpecDecision.Reject(DownloadRejectionReason.WrongMovie,
                                               "Release is catalogue number {0}, expected {1}",
                                               string.Join("/", releaseNumbers),
                                               string.Join("/", movieNumbers));
        }

        private static HashSet<string> ParseCatalogueNumbers(string title)
        {
            var numbers = new HashSet<string>();

            if (title.IsNullOrWhiteSpace())
            {
                return numbers;
            }

            foreach (Match match in CatalogueNumber.Matches(title))
            {
                // ABC-123, ABC123 and "ABC 123" are the same number, and so are
                // ABC-12 and ABC-012, so compare on a single normalised form.
                var serial = int.Parse(match.Groups["serial"].Value, CultureInfo.InvariantCulture);

                numbers.Add($"{match.Groups["prefix"].Value.ToUpperInvariant()}-{serial:D3}");
            }

            return numbers;
        }
    }
}
