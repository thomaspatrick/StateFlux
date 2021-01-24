using System;
using System.Collections.Generic;
using System.Text;

namespace StateFlux.Model
{
    public class GameCommand
    {
        public string Name { get; set; }
        public string ObjectId { get; set; }
        public Dictionary<string,string> Params { get; set; }
    }
}
