﻿using System;
using NLog;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.Indexers.PassThePopcorn
{
    public class PassThePopcorn : HttpIndexerBase<PassThePopcornSettings>
    {
        public override string Name => "PassThePopcorn";
        public override DownloadProtocol Protocol => DownloadProtocol.Torrent;
        public override bool SupportsRss => true;
        public override bool SupportsSearch => true;
        public override int PageSize => 50;
        public override TimeSpan RateLimit => TimeSpan.FromSeconds(4);

        public PassThePopcorn(IHttpClient httpClient,
            IIndexerStatusService indexerStatusService,
            IConfigService configService,
            IParsingService parsingService,
            Logger logger)
            : base(httpClient, indexerStatusService, configService, parsingService, logger)
        {
        }

        public override IIndexerRequestGenerator GetRequestGenerator()
        {
            return new PassThePopcornRequestGenerator(Settings);
        }

        public override IParseIndexerResponse GetParser()
        {
            return new PassThePopcornParser(Settings, _logger);
        }

        public override HttpRequest GetDownloadRequest(string link)
        {
            var request = new HttpRequest(link);

            request.Headers.Set("ApiUser", Settings.APIUser);
            request.Headers.Set("ApiKey", Settings.APIKey);

            return request;
        }
    }
}
