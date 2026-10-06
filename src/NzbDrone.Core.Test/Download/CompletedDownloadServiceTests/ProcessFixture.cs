using System.Collections.Generic;
using FizzWare.NBuilder;
using FluentAssertions;
using Moq;
using NUnit.Framework;
using NzbDrone.Common.Disk;
using NzbDrone.Core.Download;
using NzbDrone.Core.Download.TrackedDownloads;
using NzbDrone.Core.History;
using NzbDrone.Core.MediaFiles;
using NzbDrone.Core.MediaFiles.MovieImport;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;
using NzbDrone.Test.Common;

namespace NzbDrone.Core.Test.Download.CompletedDownloadServiceTests
{
    [TestFixture]
    public class ProcessFixture : CoreTest<CompletedDownloadService>
    {
        private TrackedDownload _trackedDownload;

        [SetUp]
        public void Setup()
        {
            var completed = Builder<DownloadClientItem>.CreateNew()
                                                    .With(h => h.Status = DownloadItemStatus.Completed)
                                                    .With(h => h.OutputPath = new OsPath(@"C:\DropFolder\MyDownload".AsOsAgnostic()))
                                                    .With(h => h.Title = "Drone.S01E01.HDTV")
                                                    .Build();

            var remoteMovie = BuildRemoteMovie();

            _trackedDownload = Builder<TrackedDownload>.CreateNew()
                    .With(c => c.State = TrackedDownloadState.Downloading)
                    .With(c => c.DownloadItem = completed)
                    .With(c => c.RemoteMovie = remoteMovie)
                    .Build();

            Mocker.GetMock<IDownloadClient>()
              .SetupGet(c => c.Definition)
              .Returns(new DownloadClientDefinition { Id = 1, Name = "testClient" });

            Mocker.GetMock<IProvideDownloadClient>()
                  .Setup(c => c.Get(It.IsAny<int>()))
                  .Returns(Mocker.GetMock<IDownloadClient>().Object);

            Mocker.GetMock<IProvideImportItemService>()
                  .Setup(c => c.ProvideImportItem(It.IsAny<DownloadClientItem>(), It.IsAny<DownloadClientItem>()))
                  .Returns((DownloadClientItem item, DownloadClientItem previous) => item);

            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.FindByDownloadId(_trackedDownload.DownloadItem.DownloadId))
                  .Returns(new List<MovieHistory>());

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.GetMovie("Drone.S01E01.HDTV"))
                  .Returns(remoteMovie.Movie);
        }

        private RemoteMovie BuildRemoteMovie()
        {
            return new RemoteMovie
            {
                Movie = new Movie(),
            };
        }

        private void GivenNoGrabbedHistory()
        {
            Mocker.GetMock<IHistoryService>()
                .Setup(s => s.FindByDownloadId(_trackedDownload.DownloadItem.DownloadId))
                .Returns(new List<MovieHistory>());
        }

        private void GivenMovieMatch()
        {
            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.GetMovie(It.IsAny<string>()))
                  .Returns(_trackedDownload.RemoteMovie.Movie);
        }

        private void GivenABadlyNamedDownload()
        {
            _trackedDownload.DownloadItem.DownloadId = "1234";
            _trackedDownload.DownloadItem.Title = "Droned Pilot"; // Set a badly named download
            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.FindByDownloadId(It.Is<string>(i => i == "1234")))
                  .Returns(new List<MovieHistory>
                  {
                      new MovieHistory() { SourceTitle = "Droned S01E01", EventType = MovieHistoryEventType.Grabbed }
                  });

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.GetMovie(It.IsAny<string>()))
                  .Returns((Movie)null);

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.GetMovie("Droned S01E01"))
                  .Returns(BuildRemoteMovie().Movie);
        }

        [TestCase(DownloadItemStatus.Downloading)]
        [TestCase(DownloadItemStatus.Failed)]
        [TestCase(DownloadItemStatus.Queued)]
        [TestCase(DownloadItemStatus.Paused)]
        [TestCase(DownloadItemStatus.Warning)]
        public void should_not_process_if_download_status_isnt_completed(DownloadItemStatus status)
        {
            _trackedDownload.DownloadItem.Status = status;

            Subject.Check(_trackedDownload);

            AssertNotReadyToImport();
        }

        [Test]
        public void should_not_process_if_matching_history_is_not_found_and_no_category_specified()
        {
            _trackedDownload.DownloadItem.Category = null;
            GivenNoGrabbedHistory();

            Subject.Check(_trackedDownload);

            AssertNotReadyToImport();
        }

        [Test]
        public void should_process_if_matching_history_is_not_found_but_category_specified()
        {
            _trackedDownload.DownloadItem.Category = "tv";
            GivenNoGrabbedHistory();
            GivenMovieMatch();

            Subject.Check(_trackedDownload);

            AssertReadyToImport();
        }

        [Test]
        public void should_not_process_if_output_path_is_empty()
        {
            _trackedDownload.DownloadItem.OutputPath = default(OsPath);

            Subject.Check(_trackedDownload);

            AssertNotReadyToImport();
        }

        [Test]
        public void should_not_process_if_the_download_cannot_be_tracked_using_the_source_title_as_it_was_initiated_externally()
        {
            GivenABadlyNamedDownload();

            Mocker.GetMock<IDownloadedMovieImportService>()
                  .Setup(v => v.ProcessPath(It.IsAny<string>(), It.IsAny<ImportMode>(), It.IsAny<Movie>(), It.IsAny<DownloadClientItem>()))
                  .Returns(new List<ImportResult>
                           {
                               new ImportResult(new ImportDecision(new LocalMovie { Path = @"C:\TestPath\Droned.S01E01.mkv" }))
                           });

            Subject.Check(_trackedDownload);

            AssertNotReadyToImport();
        }

        [Test]
        public void should_not_process_when_there_is_a_title_mismatch()
        {
            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.GetMovie("Drone.S01E01.HDTV"))
                  .Returns((Movie)null);

            Subject.Check(_trackedDownload);

            AssertNotReadyToImport();
        }

        private void GivenGrabbedByIdFor(string movieTitle, string releaseTitle)
        {
            _trackedDownload.DownloadItem.DownloadId = "5678";
            _trackedDownload.DownloadItem.Title = releaseTitle;

            Mocker.GetMock<IHistoryService>()
                  .Setup(s => s.FindByDownloadId("5678"))
                  .Returns(new List<MovieHistory>
                  {
                      new MovieHistory
                      {
                          MovieId = 5,
                          SourceTitle = releaseTitle,
                          EventType = MovieHistoryEventType.Grabbed,
                          Data = new Dictionary<string, string> { { MovieHistory.MOVIE_MATCH_TYPE, MovieMatchType.Id.ToString() } }
                      }
                  });

            Mocker.GetMock<IMovieService>()
                  .Setup(s => s.GetMovie(5))
                  .Returns(new Movie { Id = 5, Title = movieTitle });
        }

        [Test]
        public void should_process_when_release_names_the_grabbed_movies_catalogue_number()
        {
            GivenGrabbedByIdFor("ABC-12", "ABC-012 Performer Name [FHD]");

            // The title search finds nothing: the release writes the serial with
            // a leading zero the movie title does not have.
            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.GetMovie(It.IsAny<string>()))
                  .Returns((Movie)null);

            Subject.Check(_trackedDownload);

            AssertReadyToImport();
        }

        [Test]
        public void should_not_process_an_id_match_when_release_names_a_different_catalogue_number()
        {
            GivenGrabbedByIdFor("ABC-123", "XYZ-999 Performer Name");

            Mocker.GetMock<IParsingService>()
                  .Setup(s => s.GetMovie(It.IsAny<string>()))
                  .Returns((Movie)null);

            Subject.Check(_trackedDownload);

            AssertNotReadyToImport();
        }

        private void AssertNotReadyToImport()
        {
            _trackedDownload.State.Should().NotBe(TrackedDownloadState.ImportPending);
        }

        private void AssertReadyToImport()
        {
            _trackedDownload.State.Should().Be(TrackedDownloadState.ImportPending);
        }
    }
}
