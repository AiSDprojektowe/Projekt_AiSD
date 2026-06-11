using System;
using System.Collections.Generic;

namespace Projekt_AiSD.Modules
{
    internal static class Visualization
    {
        public static IReadOnlyList<int> NormalizeConvergenceHistory(IReadOnlyList<int> values)
        {
            return values ?? Array.Empty<int>();
        }
    }
}
