using NLog;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.DecisionEngine.Specifications
{
    public class CatalogueNumberSpecification : IDownloadDecisionEngineSpecification
    {
        private readonly Logger _logger;

        public CatalogueNumberSpecification(Logger logger)
        {
            _logger = logger;
        }

        public SpecificationPriority Priority => SpecificationPriority.Default;
        public RejectionType Type => RejectionType.Permanent;

        public DownloadSpecDecision IsSatisfiedBy(RemoteMovie subject, ReleaseDecisionInformation information)
        {
            // An indexer that echoes the queried tmdbid back onto every result it
            // returns makes ParsingService match all of them to the searched entry
            // as MovieMatchType.Id, however unrelated the release actually is. The
            // id then carries no information, so the release title has to be the
            // one deciding what the release is: a release that names a catalogue
            // number is asserting its own identity, and if that identity is not
            // the entry's then it is a different film no matter what id came
            // attached to it.
            var entryTitle = subject?.Movie?.Title;

            if (!CatalogueNumber.Contradicts(entryTitle, subject?.Release?.Title))
            {
                return DownloadSpecDecision.Accept();
            }

            var releaseNumbers = string.Join("/", CatalogueNumber.Parse(subject.Release.Title));
            var entryNumbers = string.Join("/", CatalogueNumber.Parse(entryTitle));

            _logger.Debug("Release '{0}' is catalogue number {1}, but '{2}' is {3}",
                          subject.Release.Title,
                          releaseNumbers,
                          entryTitle,
                          entryNumbers);

            return DownloadSpecDecision.Reject(DownloadRejectionReason.WrongMovie,
                                               "Release is catalogue number {0}, expected {1}",
                                               releaseNumbers,
                                               entryNumbers);
        }
    }
}
