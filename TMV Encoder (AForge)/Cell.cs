using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;

namespace TMV_Encoder__AForge_
{
    public sealed class Cell
    {
        /* Cell class, used for unprocessed cells only - pretty boring - maybe a struct would be better */
        public uint cellNum { get; set; }
        public Bitmap src { get; set; }

        public Cell(Bitmap source, uint number)
        {
            cellNum = number;
            src = source;
        }
    }
}
