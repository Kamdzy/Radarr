using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Equ;
using FluentValidation;
using NzbDrone.Core.Annotations;
using NzbDrone.Core.Languages;
using NzbDrone.Core.Parser.Model;
using NzbDrone.Core.Validation;

namespace NzbDrone.Core.Indexers.Nyaa
{
    public class NyaaSettingsValidator : AbstractValidator<NyaaSettings>
    {
        public NyaaSettingsValidator()
        {
            RuleFor(c => c.BaseUrl).ValidRootUrl();
            RuleFor(c => c.AdditionalParameters).Matches("(&[a-z]+=[a-z0-9_]+)*", RegexOptions.IgnoreCase);

            RuleFor(c => c.SeedCriteria).SetValidator(_ => new SeedCriteriaSettingsValidator());
        }
    }

    public class NyaaSettings : PropertywiseEquatable<NyaaSettings>, ITorrentIndexerSettings
    {
        private static readonly NyaaSettingsValidator Validator = new ();

        public NyaaSettings()
        {
            BaseUrl = "";
            AdditionalParameters = "&cats=1_0&filter=1";
            MinimumSeeders = IndexerDefaults.MINIMUM_SEEDERS;
            MultiLanguages = Array.Empty<int>();
            FailDownloads = Array.Empty<int>();
            RequiredFlags = Array.Empty<int>();
        }

        [FieldDefinition(0, Label = "IndexerSettingsBaseUrl")]
        public string BaseUrl { get; set; }

        [FieldDefinition(1, Label = "IndexerSettingsAdditionalParameters", Advanced = true, HelpText = "IndexerNyaaSettingsAdditionalParametersHelpText")]
        public string AdditionalParameters { get; set; }

        [FieldDefinition(2, Type = FieldType.Number, Label = "IndexerSettingsMinimumSeeders", HelpText = "IndexerSettingsMinimumSeedersHelpText", Advanced = true)]
        public int MinimumSeeders { get; set; }

        [FieldDefinition(3)]
        public SeedCriteriaSettings SeedCriteria { get; set; } = new ();

        [FieldDefinition(4, Type = FieldType.Checkbox, Label = "IndexerSettingsRejectBlocklistedTorrentHashes", HelpText = "IndexerSettingsRejectBlocklistedTorrentHashesHelpText", Advanced = true)]
        public bool RejectBlocklistedTorrentHashesWhileGrabbing { get; set; }

        [FieldDefinition(5, Type = FieldType.Select, SelectOptions = typeof(RealLanguageFieldConverter), Label = "IndexerSettingsMultiLanguageRelease", HelpText = "IndexerSettingsMultiLanguageReleaseHelpText", Advanced = true)]
        public IEnumerable<int> MultiLanguages { get; set; }

        [FieldDefinition(6, Type = FieldType.Select, SelectOptions = typeof(FailDownloads), Label = "IndexerSettingsFailDownloads", HelpText = "IndexerSettingsFailDownloadsHelpText", Advanced = true)]
        public IEnumerable<int> FailDownloads { get; set; }

        [FieldDefinition(7, Type = FieldType.Select, SelectOptions = typeof(IndexerFlags), Label = "IndexerSettingsRequiredFlags", HelpText = "IndexerSettingsRequiredFlagsHelpText", HelpLink = "https://wiki.servarr.com/radarr/settings#indexer-flags", Advanced = true)]
        public IEnumerable<int> RequiredFlags { get; set; }

        public NzbDroneValidationResult Validate()
        {
            return new NzbDroneValidationResult(Validator.Validate(this));
        }
    }
}
