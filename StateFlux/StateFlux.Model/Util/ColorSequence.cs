using System;
using System.Collections.Generic;
using System.Text;

namespace StateFlux.Model
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
        static private int index = 0;

        static public Color Next()
        {
            Color c = colors[index++];
            index = index > (colors.Count - 1) ? 0 : index;
            return c;
        }
    }
}
