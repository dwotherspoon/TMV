using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;

namespace TMV_Encoder__AForge_
{
    public sealed class Cell
    {
        /* Cell class, used for unprocessed cells */
        public uint cellNum { get; set; }
        public Color[] src { get; set; }

        public Cell(Color[] source, uint number)
        {
            cellNum = number;
            src = source;
        }
    }
}
