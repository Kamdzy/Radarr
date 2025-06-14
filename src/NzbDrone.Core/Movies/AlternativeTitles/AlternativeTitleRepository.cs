using System.Collections.Generic;
using NzbDrone.Core.Datastore;
using NzbDrone.Core.Messaging.Events;

namespace NzbDrone.Core.Movies.AlternativeTitles
{
    public interface IAlternativeTitleRepository : IBasicRepository<AlternativeTitle>
    {
        List<AlternativeTitle> FindByMovieMetadataId(int movieId);
        List<AlternativeTitle> FindByCleanTitles(List<string> cleanTitles);
        void DeleteForMovies(List<int> movieIds);
    }

    public class AlternativeTitleRepository : BasicRepository<AlternativeTitle>, IAlternativeTitleRepository
    {
        public AlternativeTitleRepository(IMainDatabase database, IEventAggregator eventAggregator)
            : base(database, eventAggregator)
        {
        }

        public List<AlternativeTitle> FindByMovieMetadataId(int movieId)
        {
            return Query(x => x.MovieMetadataId == movieId);
        }

        public List<AlternativeTitle> FindByCleanTitles(List<string> cleanTitles)
        {
            return Query(x => cleanTitles.Contains(x.CleanTitle));
        }

        public void DeleteForMovies(List<int> movieIds)
        {
            Delete(x => movieIds.Contains(x.MovieMetadataId));
        }
    }
}
