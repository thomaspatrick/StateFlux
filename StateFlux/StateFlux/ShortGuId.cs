using System;
using System.Collections.Generic;
using System.Text;

namespace StateFlux.Service
{
    public class ShortGuid
    {
        public static string Generate()
        {
            string enc = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
            enc = enc.Replace("/", "_");
            enc = enc.Replace("+", "-");
            return enc.Substring(0, 22);
        }
    }
}
