using System;
using System.Collections.Generic;
using Ape.Data;

namespace Ape.Game
{
    // Runtime wheel instance: authored definition plus the resolved slices for this run.
    public sealed class RouletteResolvedWheel
    {
        public RouletteWheelData Definition { get; }
        public IReadOnlyList<RouletteResolvedSlice> Slices { get; }

        public RouletteResolvedWheel(RouletteWheelData definition, IReadOnlyList<RouletteResolvedSlice> slices)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Slices = slices ?? throw new ArgumentNullException(nameof(slices));
        }
    }
}
