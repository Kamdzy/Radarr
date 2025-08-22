using System;
using NzbDrone.Common.Http;

namespace NzbDrone.Common.Cloud
{
    public interface IRadarrCloudRequestBuilder
    {
        IHttpRequestBuilderFactory Services { get; }
        IHttpRequestBuilderFactory TMDB { get; }
        IHttpRequestBuilderFactory RadarrMetadata { get; }
    }

    public class RadarrCloudRequestBuilder : IRadarrCloudRequestBuilder
    {
        public RadarrCloudRequestBuilder()
        {
            Services = new HttpRequestBuilder("https://radarr.servarr.com/v1/")
                .CreateFactory();

            TMDB = new HttpRequestBuilder("http://192.168.178.102:2070/v1/tmdb/{api}/{route}/{id}{secondaryRoute}")
                .SetHeader("Authorization", $"Bearer {AuthToken}")
                .SetTimeout(TimeSpan.FromMinutes(10))
                .CreateFactory();

            RadarrMetadata = new HttpRequestBuilder("http://192.168.178.102:2070/v1/metadata/{route}")
                .SetTimeout(TimeSpan.FromMinutes(10))
                .CreateFactory();
        }

        public IHttpRequestBuilderFactory Services { get; private set; }
        public IHttpRequestBuilderFactory TMDB { get; private set; }
        public IHttpRequestBuilderFactory RadarrMetadata { get; private set; }

        public string AuthToken => "eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJhdWQiOiIxYTczNzMzMDE5NjFkMDNmOTdmODUzYTg3NmRkMTIxMiIsInN1YiI6IjU4NjRmNTkyYzNhMzY4MGFiNjAxNzUzNCIsInNjb3BlcyI6WyJhcGlfcmVhZCJdLCJ2ZXJzaW9uIjoxfQ.gh1BwogCCKOda6xj9FRMgAAj_RYKMMPC3oNlcBtlmwk";
    }
}
