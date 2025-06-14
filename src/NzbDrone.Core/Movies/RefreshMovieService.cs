using System;
using System.Collections.Generic;
using System.Linq;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Instrumentation.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Exceptions;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.Events;
using NzbDrone.Core.Messaging.Commands;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.MetadataSource;
using NzbDrone.Core.Movies.AlternativeTitles;
using NzbDrone.Core.Movies.Collections;
using NzbDrone.Core.Movies.Commands;
using NzbDrone.Core.Movies.Credits;
using NzbDrone.Core.Movies.Events;
using NzbDrone.Core.Movies.Translations;
using NzbDrone.Core.RootFolders;

namespace NzbDrone.Core.Movies
{
    public class RefreshMovieService : IExecute<RefreshMovieCommand>
    {
        private readonly IProvideMovieInfo _movieInfo;
        private readonly IMovieService _movieService;
        private readonly IAddMovieCollectionService _movieCollectionService;
        private readonly IMovieMetadataService _movieMetadataService;
        private readonly IRootFolderService _folderService;
        private readonly IMovieTranslationService _movieTranslationService;
        private readonly IAlternativeTitleService _alternativeTitleService;
        private readonly ICreditService _creditService;
        private readonly IEventAggregator _eventAggregator;
        private readonly IDiskScanService _diskScanService;
        private readonly ICheckIfMovieShouldBeRefreshed _checkIfMovieShouldBeRefreshed;
        private readonly IConfigService _configService;
        private readonly Logger _logger;

        public RefreshMovieService(IProvideMovieInfo movieInfo,
                                    IMovieService movieService,
                                    IAddMovieCollectionService movieCollectionService,
                                    IMovieMetadataService movieMetadataService,
                                    IRootFolderService folderService,
                                    IMovieTranslationService movieTranslationService,
                                    IAlternativeTitleService alternativeTitleService,
                                    ICreditService creditService,
                                    IEventAggregator eventAggregator,
                                    IDiskScanService diskScanService,
                                    ICheckIfMovieShouldBeRefreshed checkIfMovieShouldBeRefreshed,
                                    IConfigService configService,
                                    Logger logger)
        {
            _movieInfo = movieInfo;
            _movieService = movieService;
            _movieCollectionService = movieCollectionService;
            _movieMetadataService = movieMetadataService;
            _folderService = folderService;
            _movieTranslationService = movieTranslationService;
            _alternativeTitleService = alternativeTitleService;
            _creditService = creditService;
            _eventAggregator = eventAggregator;
            _diskScanService = diskScanService;
            _checkIfMovieShouldBeRefreshed = checkIfMovieShouldBeRefreshed;
            _configService = configService;
            _logger = logger;
        }

        private Movie RefreshMovieInfo(int movieId)
        {
            // Get the movie before updating, that way any changes made to the movie after the refresh started,
            // but before this movie was refreshed won't be lost.
            var movie = _movieService.GetMovie(movieId);
            var movieMetadata = _movieMetadataService.Get(movie.MovieMetadataId);

            _logger.ProgressInfo("Updating info for {0}", movie.Title);

            MovieMetadata movieInfo;
            List<Credit> credits;

            try
            {
                var tuple = _movieInfo.GetMovieInfo(movie.TmdbId);
                movieInfo = tuple.Item1;
                credits = tuple.Item2;
            }
            catch (MovieNotFoundException)
            {
                if (movieMetadata.Status != MovieStatusType.Deleted)
                {
                    movieMetadata.Status = MovieStatusType.Deleted;
                    _movieMetadataService.Upsert(movieMetadata);
                    _logger.Debug("Movie marked as deleted on TMDb for {0}", movie.Title);
                    _eventAggregator.PublishEvent(new MovieUpdatedEvent(movie));
                }

                throw;
            }

            if (movieMetadata.TmdbId != movieInfo.TmdbId)
            {
                _logger.Warn("Movie '{0}' (TMDb: {1}) was replaced with '{2}' (TMDb: {3}), because the original was a duplicate.", movie.Title, movie.TmdbId, movieInfo.Title, movieInfo.TmdbId);
                movieMetadata.TmdbId = movieInfo.TmdbId;
            }

            movieMetadata.Title = movieInfo.Title;
            movieMetadata.ImdbId = movieInfo.ImdbId;
            movieMetadata.Overview = movieInfo.Overview;
            movieMetadata.Status = movieInfo.Status;
            movieMetadata.Images = movieInfo.Images;
            movieMetadata.CleanTitle = movieInfo.CleanTitle;
            movieMetadata.SortTitle = movieInfo.SortTitle;
            movieMetadata.LastInfoSync = DateTime.UtcNow;
            movieMetadata.Runtime = movieInfo.Runtime;
            movieMetadata.Ratings = movieInfo.Ratings;
            movieMetadata.Genres = movieInfo.Genres;
            movieMetadata.Keywords = movieInfo.Keywords;
            movieMetadata.Certification = movieInfo.Certification;
            movieMetadata.InCinemas = movieInfo.InCinemas;
            movieMetadata.Website = movieInfo.Website;

            movieMetadata.Year = movieInfo.Year;
            movieMetadata.SecondaryYear = movieInfo.SecondaryYear;
            movieMetadata.PhysicalRelease = movieInfo.PhysicalRelease;
            movieMetadata.DigitalRelease = movieInfo.DigitalRelease;
            movieMetadata.YouTubeTrailerId = movieInfo.YouTubeTrailerId;
            movieMetadata.Studio = movieInfo.Studio;
            movieMetadata.OriginalTitle = movieInfo.OriginalTitle;
            movieMetadata.CleanOriginalTitle = movieInfo.CleanOriginalTitle;
            movieMetadata.OriginalLanguage = movieInfo.OriginalLanguage;
            movieMetadata.Recommendations = movieInfo.Recommendations;
            movieMetadata.Popularity = movieInfo.Popularity;

            // add collection
            if (movieInfo.CollectionTmdbId > 0)
            {
                var newCollection = _movieCollectionService.AddMovieCollection(new MovieCollection
                {
                    TmdbId = movieInfo.CollectionTmdbId,
                    Title = movieInfo.CollectionTitle,
                    Monitored = movie.AddOptions?.Monitor == MonitorTypes.MovieAndCollection,
                    SearchOnAdd = movie.AddOptions?.SearchForMovie ?? false,
                    QualityProfileId = movie.QualityProfileId,
                    MinimumAvailability = movie.MinimumAvailability,
                    RootFolderPath = _folderService.GetBestRootFolderPath(movie.Path).GetCleanPath(),
                    Tags = movie.Tags
                });

                if (newCollection != null)
                {
                    movieMetadata.CollectionTmdbId = newCollection.TmdbId;
                    movieMetadata.CollectionTitle = newCollection.Title;
                }
            }
            else
            {
                movieMetadata.CollectionTmdbId = 0;
                movieMetadata.CollectionTitle = null;
            }

            movieMetadata.AlternativeTitles = _alternativeTitleService.UpdateTitles(movieInfo.AlternativeTitles, movieMetadata);

            _movieTranslationService.UpdateTranslations(movieInfo.Translations, movieMetadata);
            _creditService.UpdateCredits(credits, movieMetadata);

            _movieMetadataService.Upsert(movieMetadata);

            movie.MovieMetadata = movieMetadata;

            _logger.Debug("Finished movie metadata refresh for {0}", movieMetadata.Title);
            _eventAggregator.PublishEvent(new MovieUpdatedEvent(movie));

            return movie;
        }

        private void RescanMovie(Movie movie, bool isNew, CommandTrigger trigger)
        {
            var rescanAfterRefresh = _configService.RescanAfterRefresh;

            if (isNew)
            {
                _logger.Trace("Forcing rescan of {0}. Reason: New movie", movie);
            }
            else if (rescanAfterRefresh == RescanAfterRefreshType.Never)
            {
                _logger.Trace("Skipping rescan of {0}. Reason: Never rescan after refresh", movie);
                _eventAggregator.PublishEvent(new MovieScanSkippedEvent(movie, MovieScanSkippedReason.NeverRescanAfterRefresh));

                return;
            }
            else if (rescanAfterRefresh == RescanAfterRefreshType.AfterManual && trigger != CommandTrigger.Manual)
            {
                _logger.Trace("Skipping rescan of {0}. Reason: Not after automatic scans", movie);
                _eventAggregator.PublishEvent(new MovieScanSkippedEvent(movie, MovieScanSkippedReason.RescanAfterManualRefreshOnly));

                return;
            }

            try
            {
                _diskScanService.Scan(movie);
            }
            catch (Exception e)
            {
                _logger.Error(e, "Couldn't rescan movie {0}", movie);
            }
        }

        private void UpdateTags(Movie movie, bool isNew)
        {
            if (isNew)
            {
                _logger.Trace("Skipping tag update for {0}. Reason: New movie", movie);
                return;
            }

            var tagsUpdated = _movieService.UpdateTags(movie);

            if (tagsUpdated)
            {
                _movieService.UpdateMovie(movie);
            }
        }

        public void Execute(RefreshMovieCommand message)
        {
            var trigger = message.Trigger;
            var isNew = message.IsNewMovie;
            _eventAggregator.PublishEvent(new MovieRefreshStartingEvent(message.Trigger == CommandTrigger.Manual));

            if (message.MovieIds.Any())
            {
                foreach (var movieId in message.MovieIds)
                {
                    var movie = _movieService.GetMovie(movieId);

                    try
                    {
                        movie = RefreshMovieInfo(movieId);
                        UpdateTags(movie, isNew);
                        RescanMovie(movie, isNew, trigger);
                    }
                    catch (MovieNotFoundException)
                    {
                        _logger.Error("Movie '{0}' (TMDb {1}) was not found, it may have been removed from The Movie Database.", movie.Title, movie.TmdbId);
                    }
                    catch (Exception e)
                    {
                        _logger.Error(e, "Couldn't refresh info for {0}", movie);
                        UpdateTags(movie, isNew);
                        RescanMovie(movie, isNew, trigger);
                        throw;
                    }
                }
            }
            else
            {
                // TODO refresh all moviemetadata here, even if not used by a Movie
                var allMovies = _movieService.GetAllMovies();

                var updatedTmdbMovies = new HashSet<int>();

                if (message.LastStartTime.HasValue && message.LastStartTime.Value.AddDays(14) > DateTime.UtcNow)
                {
                    updatedTmdbMovies = _movieInfo.GetChangedMovies(message.LastStartTime.Value);
                }

                foreach (var movie in allMovies)
                {
                    var movieLocal = movie;
                    if ((updatedTmdbMovies.Count == 0 && _checkIfMovieShouldBeRefreshed.ShouldRefresh(movie.MovieMetadata)) || updatedTmdbMovies.Contains(movie.TmdbId) || message.Trigger == CommandTrigger.Manual)
                    {
                        try
                        {
                            movieLocal = RefreshMovieInfo(movieLocal.Id);
                        }
                        catch (MovieNotFoundException)
                        {
                            _logger.Error("Movie '{0}' (TMDb {1}) was not found, it may have been removed from The Movie Database.", movieLocal.Title, movieLocal.TmdbId);
                            continue;
                        }
                        catch (Exception e)
                        {
                            _logger.Error(e, "Couldn't refresh info for {0}", movieLocal);
                        }

                        UpdateTags(movie, false);
                        RescanMovie(movieLocal, false, trigger);
                    }
                    else
                    {
                        _logger.Debug("Skipping refresh of movie: {0}", movieLocal.Title);
                        UpdateTags(movie, false);
                        RescanMovie(movieLocal, false, trigger);
                    }
                }
            }

            _eventAggregator.PublishEvent(new MovieRefreshCompleteEvent());
        }
    }
}
