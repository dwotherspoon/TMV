using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Threading;
using System.Collections.Specialized;

namespace TMV_Encoder__AForge_
{
    public sealed class TMVEncoder
    {
        /* The new encoder class, that's hopefully faster */
        private static Color[] colours = {   Color.FromArgb(000, 000, 000), Color.FromArgb(000, 000, 170), Color.FromArgb(000, 170, 000), Color.FromArgb(000, 170, 170), Color.FromArgb(170, 000, 000), 
                                     Color.FromArgb(170, 000, 170), Color.FromArgb(170, 085, 000), Color.FromArgb(170, 170, 170), Color.FromArgb(085, 085, 085), Color.FromArgb(085, 085, 255), 
                                     Color.FromArgb(085, 255, 085), Color.FromArgb(085, 255, 255), Color.FromArgb(255, 085, 085), Color.FromArgb(255, 085, 255), Color.FromArgb(255, 255, 085), 
                                     Color.FromArgb(255, 255, 255) };
        private Thread[] workers;

        private Queue<Cell> unprocessed;

        public TMVEncoder()
        {
            load_font();
        }

        public void encoder(Bitmap input)
        {
        }

        private void worker()
        {
            while (unprocessed.Count != 0) //when queue is not empty, dequeue and encode
            {
                Cell current = unprocessed.Dequeue();
            }
        }

        private void load_font()
        {
        }

    }
}
