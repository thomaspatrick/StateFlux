using System;
using System.Collections.Generic;
using System.Text;

namespace StateFlux.Model
{
    public class Mouse
    {
        public string PlayerId { get; set; }
        public string Icon { get; set; }
        public string Style { get; set; }
        public Vec2d Pos { get; set; }

    }
    public class Mice
    {
        public List<Mouse> Items { get; set; }
    }
}
