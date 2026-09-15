using FluentAssertions;
using NUnit.Framework;
using NzbDrone.Core.DecisionEngine;
using NzbDrone.Core.DecisionEngine.Specifications;
using NzbDrone.Core.Movies;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.DecisionEngineTests
{
    [TestFixture]
    public class CatalogueNumberSpecificationFixture : CoreTest<CatalogueNumberSpecification>
    {
        private RemoteMovie GivenRelease(string movieTitle, string releaseTitle)
        {
            return new RemoteMovie
            {
                Movie = new Movie { Title = movieTitle },
                Release = new ReleaseInfo { Title = releaseTitle }
            };
        }

        // An indexer that echoes the queried tmdbid back onto every result makes
        // all of these match the searched movie as MovieMatchType.Id, so the
        // catalogue number named by the release is the only thing left that can
        // tell them apart.
        [TestCase("ABCDEF-851", "Some Long Descriptive Release Name [GHI-090] (Studio Name)")]
        [TestCase("ABCDE-965", "Another Release Title Entirely [JKL-397] (720p)")]
        [TestCase("ABCD-084", "A Third Unrelated Release [MNO-241] (Studio) [decen]")]
        [TestCase("ABC-08", "Yet Another Release [PQRS-053] (Studio) [decen]")]
        public void should_reject_release_naming_a_different_catalogue_number(string movieTitle, string releaseTitle)
        {
            Subject.IsSatisfiedBy(GivenRelease(movieTitle, releaseTitle), new ReleaseDecisionInformation()).Accepted.Should().BeFalse();
        }

        [Test]
        public void should_reject_when_only_the_serial_matches()
        {
            // ABCD-424 vs EFGH-424: same serial, different prefix, different film.
            var remoteMovie = GivenRelease("ABCD-424", "A Release Title [EFGH-424] (Studio) [cen]");

            Subject.IsSatisfiedBy(remoteMovie, new ReleaseDecisionInformation()).Accepted.Should().BeFalse();
        }

        [TestCase("ABCD-410", "[HD/720p] ABCD-410 Some Release Title")]
        [TestCase("ABCD-770", "[HD/720p] ABCD-770 Another Release Title")]
        public void should_accept_release_naming_the_same_catalogue_number(string movieTitle, string releaseTitle)
        {
            Subject.IsSatisfiedBy(GivenRelease(movieTitle, releaseTitle), new ReleaseDecisionInformation()).Accepted.Should().BeTrue();
        }

        [TestCase("ABC-08", "ABC-008 Some Title 1080p")]
        [TestCase("ABCD030", "ABCD-030 Some Title 720p")]
        [TestCase("ABCD-014", "ABCD014")]
        [TestCase("ABC-209", "ABC 209 Another Title")]
        public void should_treat_padding_and_separators_as_the_same_number(string movieTitle, string releaseTitle)
        {
            Subject.IsSatisfiedBy(GivenRelease(movieTitle, releaseTitle), new ReleaseDecisionInformation()).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_accept_when_release_names_no_catalogue_number()
        {
            // Nothing is being asserted about identity, so leave the judgement to
            // the other specifications rather than guessing.
            var remoteMovie = GivenRelease("ABCD-410", "A Release Title With No Number 1080p");

            Subject.IsSatisfiedBy(remoteMovie, new ReleaseDecisionInformation()).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_accept_when_release_also_names_the_expected_number()
        {
            var remoteMovie = GivenRelease("ABCD-410", "ABCD-410 with ABCD-411 trailer attached");

            Subject.IsSatisfiedBy(remoteMovie, new ReleaseDecisionInformation()).Accepted.Should().BeTrue();
        }

        // Ordinary movie titles must not engage this specification at all, or a
        // quality/audio tag that merely looks like a catalogue number would start
        // rejecting perfectly good releases.
        [TestCase("Apollo 13", "Apollo.13.1995.1080p.BluRay.x264-SPARKS")]
        [TestCase("Apollo 13", "Apollo 13 1995 Remastered MP3-320 BluRay")]
        [TestCase("Blade Runner 2049", "Blade.Runner.2049.2017.2160p.UHD.BluRay.x265-TERMiNAL")]
        [TestCase("The Matrix", "The.Matrix.1999.1080p.BluRay.DTS-HD.MA.5.1.x264")]
        [TestCase("Se7en", "Se7en.1995.REMASTERED.1080p.BluRay.MP3-320")]
        public void should_not_apply_to_movies_not_titled_by_catalogue_number(string movieTitle, string releaseTitle)
        {
            Subject.IsSatisfiedBy(GivenRelease(movieTitle, releaseTitle), new ReleaseDecisionInformation()).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_accept_when_movie_has_no_title()
        {
            var remoteMovie = new RemoteMovie
            {
                Movie = new Movie(),
                Release = new ReleaseInfo { Title = "Some Release [ABC-090]" }
            };

            Subject.IsSatisfiedBy(remoteMovie, new ReleaseDecisionInformation()).Accepted.Should().BeTrue();
        }

        [Test]
        public void should_accept_when_release_has_no_title()
        {
            var remoteMovie = new RemoteMovie
            {
                Movie = new Movie { Title = "ABCD-410" },
                Release = new ReleaseInfo()
            };

            Subject.IsSatisfiedBy(remoteMovie, new ReleaseDecisionInformation()).Accepted.Should().BeTrue();
        }
    }
}
