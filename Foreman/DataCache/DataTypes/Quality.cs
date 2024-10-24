using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;

namespace Foreman
{
    public interface Quality : DataObjectBase
    {
        Quality NextQuality { get; }
        Quality PrevQuality { get; }
        double NextProbability { get; }

        int Level { get; }
        double BeaconPowerMultiplier { get; }
        double MiningDrillResourceDrainMultiplier { get; }

    }

    public class QualityPrototype : DataObjectBasePrototype, Quality
    {
        public Quality NextQuality { get; internal set; }
        public Quality PrevQuality { get; internal set; }
        public double NextProbability { get; set; }

        public int Level { get; set; }
        public double BeaconPowerMultiplier { get; set; }
        public double MiningDrillResourceDrainMultiplier { get; set; }

        public QualityPrototype(DataCache dCache, string name, string friendlyName, string order) : base(dCache, name, friendlyName, order)
        {
            Enabled = true;

            Level = 0;
            BeaconPowerMultiplier = 1;
            MiningDrillResourceDrainMultiplier = 1;
        }

        public override string ToString() { return string.Format("Quality T{0}: {1}", Level, Name); }
    }
}
