using System.Collections.Generic;
using System.Linq;
using NzbDrone.Core.Qualities;
using Radarr.Http.REST;

namespace Radarr.Api.V3.Qualities
{
    public class QualityDefinitionResource : RestResource
    {
        public Quality Quality { get; set; }
        public string Title { get; set; }
        public int Weight { get; set; }
        public double? MinSize { get; set; }
        public double? MaxSize { get; set; }
        public double? PreferredSize { get; set; }
    }

    public static class QualityDefinitionResourceMapper
    {
        public static QualityDefinitionResource ToResource(this QualityDefinition model)
        {
            if (model == null)
            {
                return null;
            }

            return new QualityDefinitionResource
            {
                Id = model.Id,
                Quality = model.Quality,
                Title = model.Title,
                Weight = model.Weight,
                MinSize = model.MinSize,
                MaxSize = model.MaxSize,
                PreferredSize = model.PreferredSize
            };
        }

        public static QualityDefinition ToModel(this QualityDefinitionResource resource)
        {
            if (resource == null)
            {
                return null;
            }

            return new QualityDefinition
            {
                Id = resource.Id,
                Quality = resource.Quality,
                Title = resource.Title,
                Weight = resource.Weight,
                MinSize = resource.MinSize,
                MaxSize = resource.MaxSize,
                PreferredSize = resource.PreferredSize
            };
        }

        public static List<QualityDefinitionResource> ToResource(this IEnumerable<QualityDefinition> models)
        {
            return models.Select(ToResource).ToList();
        }

        public static List<QualityDefinition> ToModel(this IEnumerable<QualityDefinitionResource> resources)
        {
            return resources.Select(ToModel).ToList();
        }
    }
}
