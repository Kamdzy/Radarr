using System;
using System.Collections.Generic;
using System.Linq;
using FluentValidation.Results;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.ImportLists.ImportListMovies;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.ImportLists.Radarr
{
    public class RadarrImport : ImportListBase<RadarrSettings>
    {
        private readonly IRadarrV3Proxy _radarrV3Proxy;
        public override string Name => "Radarr";
        public override bool Enabled => true;
        public override bool EnableAuto => false;

        public override ImportListType ListType => ImportListType.Program;
        public override TimeSpan MinRefreshInterval => TimeSpan.FromMinutes(15);

        public RadarrImport(IRadarrV3Proxy radarrV3Proxy,
                            IImportListStatusService importListStatusService,
                            IConfigService configService,
                            IParsingService parsingService,
                            Logger logger)
            : base(importListStatusService, configService, parsingService, logger)
        {
            _radarrV3Proxy = radarrV3Proxy;
        }

        public override ImportListFetchResult Fetch()
        {
            var movies = new List<ImportListMovie>();
            var anyFailure = false;

            try
            {
                var remoteMovies = _radarrV3Proxy.GetMovies(Settings);

                foreach (var remoteMovie in remoteMovies)
                {
                    if (Settings.ProfileIds.Any() && !Settings.ProfileIds.Contains(remoteMovie.QualityProfileId))
                    {
                        continue;
                    }

                    if (Settings.TagIds.Any() && !Settings.TagIds.Any(tagId => remoteMovie.Tags.Any(itemTagId => itemTagId == tagId)))
                    {
                        continue;
                    }

                    if (Settings.RootFolderPaths.Any() && !Settings.RootFolderPaths.Any(rootFolderPath => remoteMovie.Path.ContainsIgnoreCase(rootFolderPath)))
                    {
                        continue;
                    }

                    movies.Add(new ImportListMovie
                    {
                        TmdbId = remoteMovie.TmdbId,
                        Title = remoteMovie.Title,
                        Year = remoteMovie.Year
                    });
                }

                _importListStatusService.RecordSuccess(Definition.Id);
            }
            catch (Exception ex)
            {
                anyFailure = true;

                _logger.Debug(ex, "Failed to fetch data for list {0} ({1})", Definition.Name, Name);

                _importListStatusService.RecordFailure(Definition.Id);
            }

            return new ImportListFetchResult { Movies = CleanupListItems(movies), AnyFailure = anyFailure };
        }

        public override object RequestAction(string action, IDictionary<string, string> query)
        {
            // Return early if there is not an API key
            if (Settings.ApiKey.IsNullOrWhiteSpace())
            {
                return new
                {
                    devices = new List<object>()
                };
            }

            Settings.Validate().Filter("ApiKey").ThrowOnError();

            if (action == "getProfiles")
            {
                var devices = _radarrV3Proxy.GetProfiles(Settings);

                return new
                {
                    options = devices.OrderBy(d => d.Name, StringComparer.InvariantCultureIgnoreCase)
                        .Select(d => new
                        {
                            Value = d.Id,
                            Name = d.Name
                        })
                };
            }

            if (action == "getTags")
            {
                var devices = _radarrV3Proxy.GetTags(Settings);

                return new
                {
                    options = devices.OrderBy(d => d.Label, StringComparer.InvariantCultureIgnoreCase)
                        .Select(d => new
                        {
                            Value = d.Id,
                            Name = d.Label
                        })
                };
            }

            if (action == "getRootFolders")
            {
                var remoteRootFolders = _radarrV3Proxy.GetRootFolders(Settings);

                return new
                {
                    options = remoteRootFolders.OrderBy(d => d.Path, StringComparer.InvariantCultureIgnoreCase)
                        .Select(d => new
                        {
                            Value = d.Path,
                            Name = d.Path
                        })
                };
            }

            return new { };
        }

        protected override void Test(List<ValidationFailure> failures)
        {
            failures.AddIfNotNull(_radarrV3Proxy.Test(Settings));
        }
    }
}
