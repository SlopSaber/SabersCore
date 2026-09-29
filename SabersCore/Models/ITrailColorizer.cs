using System.Collections.Generic;

namespace SabersCore.Models;

public interface ITrailColorizer
{
    IEnumerable<TrailColorInfo> GetPropertiesWithColors(ColorScheme colorScheme);
    IEnumerable<TrailColorInfo> GetPropertiesWithBoostColors(ColorScheme colorScheme, bool boost);
    IEnumerable<TrailColorInfo> GetDefault();
}
