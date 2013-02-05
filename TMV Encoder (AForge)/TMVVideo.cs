using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TMV_Encoder__AForge_
{
    /* A video */
    public class TMVVideo
    {
        private Queue<TMVFrame> frames; //Fifo 

        private byte[] audio_data;

        private decimal frameRate;

        public TMVVideo()
        {
            frames = new Queue<TMVFrame>();
        }

        public void addFrame(TMVFrame frame)
        {
            frames.Enqueue(frame);
        }

        public void save()
        {
        }
    }
}
