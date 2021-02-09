using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using StateFlux.Model;

namespace StateFlux.Unity
{
    public class ColorSequence
    {
        static private List<Color> colors = new List<Color>
        {
            { new Color { Red=1, Green=0, Blue=0, Alpha=1 } },
            { new Color { Red=0, Green=1, Blue=0, Alpha=1 } },
            { new Color { Red=0, Green=0, Blue=1, Alpha=1 } },
            { new Color { Red=1, Green=1, Blue=0, Alpha=1 } },
            { new Color { Red=1, Green=0, Blue=1, Alpha=1 } },
            { new Color { Red=0, Green=1, Blue=1, Alpha=1 } },
            { new Color { Red=1, Green=1, Blue=1, Alpha=1 } }
        };

        static public Color Next()
        {
            int index = (int)Math.Round(UnityEngine.Random.value * 6.0f);
            return colors[index];
        }
    }
}
