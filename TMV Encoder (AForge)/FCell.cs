using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TMV_Encoder__AForge_
{
    /*FCell class, used for processed cells only */

    public class FCell
    {
        public byte character;
        public byte colour1;
        public byte colour2;

        public FCell() {
            character = 177;
            colour1 = 0;
            colour2 = 0;
        }
        
        public string ToString()
        {
            return "cha: " + character;
        }
    }

 
}
