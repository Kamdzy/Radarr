using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Disk;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.CustomFormats;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.MediaFiles.MovieImport.Aggregation;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.MediaFiles.MovieImport
{
    public interface IMakeImportDecision
    {
        List<ImportDecision> GetImportDecisions(List<string> videoFiles, Movie movie);
        List<ImportDecision> GetImportDecisions(List<string> videoFiles, Movie movie, bool filterExistingFiles);
        List<ImportDecision> GetImportDecisions(List<string> videoFiles, Movie movie, DownloadClientItem downloadClientItem, ParsedMovieInfo folderInfo, bool sceneSource);
        List<ImportDecision> GetImportDecisions(List<string> videoFiles, Movie movie, DownloadClientItem downloadClientItem, ParsedMovieInfo folderInfo, bool sceneSource, bool filterExistingFiles);
        ImportDecision GetDecision(LocalMovie localMovie, DownloadClientItem downloadClientItem);
    }

    public class ImportDecisionMaker : IMakeImportDecision
    {
        private readonly IEnumerable<IImportDecisionEngineSpecification> _specifications;
        private readonly IMediaFileService _mediaFileService;
        private readonly IAggregationService _aggregationService;
        private readonly IDiskProvider _diskProvider;
        private readonly IDetectSample _detectSample;
        private readonly ITrackedDownloadService _trackedDownloadService;
        private readonly ICustomFormatCalculationService _formatCalculator;
        private readonly IParsingService _parsingService;
        private readonly Logger _logger;

        public ImportDecisionMaker(IEnumerable<IImportDecisionEngineSpecification> specifications,
                                   IMediaFileService mediaFileService,
                                   IAggregationService aggregationService,
                                   IDiskProvider diskProvider,
                                   IDetectSample detectSample,
                                   ITrackedDownloadService trackedDownloadService,
                                   ICustomFormatCalculationService formatCalculator,
                                   IParsingService parsingService,
                                   Logger logger)
        {
            _specifications = specifications;
            _mediaFileService = mediaFileService;
            _aggregationService = aggregationService;
            _diskProvider = diskProvider;
            _detectSample = detectSample;
            _trackedDownloadService = trackedDownloadService;
            _formatCalculator = formatCalculator;
            _parsingService = parsingService;
            _logger = logger;
        }

        public List<ImportDecision> GetImportDecisions(List<string> videoFiles, Movie movie)
        {
            return GetImportDecisions(videoFiles, movie, null, null, false);
        }

        public List<ImportDecision> GetImportDecisions(List<string> videoFiles, Movie movie, bool filterExistingFiles)
        {
            return GetImportDecisions(videoFiles, movie, null, null, false, filterExistingFiles);
        }

        public List<ImportDecision> GetImportDecisions(List<string> videoFiles, Movie movie, DownloadClientItem downloadClientItem, ParsedMovieInfo folderInfo, bool sceneSource)
        {
            return GetImportDecisions(videoFiles, movie, downloadClientItem, folderInfo, sceneSource, true);
        }

        public List<ImportDecision> GetImportDecisions(List<string> videoFiles, Movie movie, DownloadClientItem downloadClientItem, ParsedMovieInfo folderInfo, bool sceneSource, bool filterExistingFiles)
        {
            var newFiles = filterExistingFiles ? _mediaFileService.FilterExistingFiles(videoFiles.ToList(), movie) : videoFiles.ToList();

            _logger.Debug("Analyzing {0}/{1} files.", newFiles.Count, videoFiles.Count);

            ParsedMovieInfo downloadClientItemInfo = null;

            if (downloadClientItem != null)
            {
                downloadClientItemInfo = Parser.Parser.ParseMovieTitle(downloadClientItem.Title);
            }

            var nonSampleVideoFileCount = GetNonSampleVideoFileCount(newFiles, movie.MovieMetadata);

            var decisions = new List<ImportDecision>();

            foreach (var file in newFiles)
            {
                var localMovie = new LocalMovie
                {
                    Movie = movie,
                    DownloadClientMovieInfo = downloadClientItemInfo,
                    DownloadItem = downloadClientItem,
                    FolderMovieInfo = folderInfo,
                    Path = file,
                    SceneSource = sceneSource,
                    ExistingFile = movie.Path.IsParentPath(file),
                    OtherVideoFiles = nonSampleVideoFileCount > 1
                };

                decisions.AddIfNotNull(GetDecision(localMovie, downloadClientItem, nonSampleVideoFileCount > 1));
            }

            return decisions;
        }

        public ImportDecision GetDecision(LocalMovie localMovie, DownloadClientItem downloadClientItem)
        {
            var reasons = _specifications.Select(c => EvaluateSpec(c, localMovie, downloadClientItem))
                                         .Where(c => c != null);

            return new ImportDecision(localMovie, reasons.ToArray());
        }

        private ImportDecision GetDecision(LocalMovie localMovie, DownloadClientItem downloadClientItem, bool otherFiles)
        {
            ImportDecision decision = null;

            try
            {
                // Use filename alone to prevent folder name match on all files
                var fileName = System.IO.Path.GetFileName(localMovie.Path);
                var fileMovieInfo = Parser.Parser.ParseMoviePath(fileName);

                if (fileMovieInfo == null)
                {
                    var result = _parsingService.GetMovie(fileName);

                    if (result != null)
                    {
                        fileMovieInfo = new ParsedMovieInfo()
                        {
                            MovieTitles = new List<string>() { result.Title },
                            TmdbId = result.TmdbId
                        };
                    }
                }

                localMovie.FileMovieInfo = fileMovieInfo;
                localMovie.Size = _diskProvider.GetFileSize(localMovie.Path);

                _aggregationService.Augment(localMovie, downloadClientItem);

                if (localMovie.Movie == null)
                {
                    decision = new ImportDecision(localMovie, new ImportRejection(ImportRejectionReason.InvalidMovie, "Invalid movie"));
                }
                else
                {
                    if (downloadClientItem?.DownloadId.IsNotNullOrWhiteSpace() == true)
                    {
                        var trackedDownload = _trackedDownloadService.Find(downloadClientItem.DownloadId);

                        if (trackedDownload?.RemoteMovie?.Release?.IndexerFlags != null)
                        {
                            localMovie.IndexerFlags = trackedDownload.RemoteMovie.Release.IndexerFlags;
                        }
                    }

                    localMovie.CustomFormats = _formatCalculator.ParseCustomFormat(localMovie);
                    localMovie.CustomFormatScore = localMovie.Movie.QualityProfile?.CalculateCustomFormatScore(localMovie.CustomFormats) ?? 0;

                    decision = GetDecision(localMovie, downloadClientItem);
                }
            }
            catch (AugmentingFailedException)
            {
                decision = new ImportDecision(localMovie, new ImportRejection(ImportRejectionReason.UnableToParse, "Unable to parse file"));
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Couldn't import file. {0}", localMovie.Path);

                decision = new ImportDecision(localMovie, new ImportRejection(ImportRejectionReason.Error, "Unexpected error processing file"));
            }

            if (decision == null)
            {
                _logger.Error("Unable to make a decision on {0}", localMovie.Path);
            }
            else if (decision.Rejections.Any())
            {
                _logger.Debug("File rejected for the following reasons: {0}", string.Join(", ", decision.Rejections));
            }
            else
            {
                _logger.Debug("File accepted");
            }

            return decision;
        }

        private ImportRejection EvaluateSpec(IImportDecisionEngineSpecification spec, LocalMovie localMovie, DownloadClientItem downloadClientItem)
        {
            try
            {
                var result = spec.IsSatisfiedBy(localMovie, downloadClientItem);

                if (!result.Accepted)
                {
                    return new ImportRejection(result.Reason, result.Message);
                }
            }
            catch (NotImplementedException e)
            {
                _logger.Warn(e, "Spec " + spec.ToString() + " currently does not implement evaluation for movies.");
                return null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Couldn't evaluate decision on {0}", localMovie.Path);
                return new ImportRejection(ImportRejectionReason.DecisionError, $"{spec.GetType().Name}: {ex.Message}");
            }

            return null;
        }

        private int GetNonSampleVideoFileCount(List<string> videoFiles, MovieMetadata movie)
        {
            return videoFiles.Count(file =>
            {
                var sample = _detectSample.IsSample(movie, file);

                if (sample == DetectSampleResult.Sample)
                {
                    return false;
                }

                return true;
            });
        }
    }
}
