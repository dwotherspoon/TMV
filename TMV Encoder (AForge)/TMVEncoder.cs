using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Threading;
using System.IO;

namespace TMV_Encoder__AForge_
{
    public sealed class TMVEncoder
    {
        /* The new encoder class, that's hopefully faster
            Begin the vars section:
         */
        
        private static Color[] colours = {   Color.FromArgb(000, 000, 000), Color.FromArgb(000, 000, 170), Color.FromArgb(000, 170, 000), Color.FromArgb(000, 170, 170), Color.FromArgb(170, 000, 000), 
                                     Color.FromArgb(170, 000, 170), Color.FromArgb(170, 085, 000), Color.FromArgb(170, 170, 170), Color.FromArgb(085, 085, 085), Color.FromArgb(085, 085, 255), 
                                     Color.FromArgb(085, 255, 085), Color.FromArgb(085, 255, 255), Color.FromArgb(255, 085, 085), Color.FromArgb(255, 085, 255), Color.FromArgb(255, 255, 085), 
                                     Color.FromArgb(255, 255, 255) }; //the cga 16 colour palette
        private Thread[] workers;

        private Queue<Cell> unprocessed;

        private int threshold { get; set; }

        private ulong current_frame; //The count of the current frame.

        private static string apath = AppDomain.CurrentDomain.BaseDirectory; //exe directory for reading supporting binary files.

        /* Methods: */

        public TMVEncoder()
        {
            //load_font();
            threshold = 60; //default threshold value
            workers = new Thread[Environment.ProcessorCount]; //Initialise the workers.
        }

        public void encoder(Bitmap input) //the main function, call this to encode a frame -> starts thread workers after breaking image down.
        {
            for (int row = 0; row < 25; row++) //for each row
            {
                for (int col = 0; col < 40; col++) // for each cell
                {
                    unprocessed.Enqueue(new Cell(getCell(input),(uint)((row*25)+(col*40)))); //add the new cell to our task pool
                }

            }
            for (int i = 0; i < workers.Length; i++ ) //start the workers on our cell pool
            {
                workers[i] = new Thread(worker);
                workers[i].Start();
            }
        }

        public Bitmap render_last() //renders the last frame
        {
            return null;
        }

        private Bitmap getCell(Bitmap input)
        {
            return null;
        }

        private void worker() //The worker function, runs until the queue is depleted.
        {
            while (unprocessed.Count > 0) //when queue is not empty, dequeue and encode
            {
                Cell current = unprocessed.Dequeue(); //grab a new cell
                if (StdDev(current.src) > threshold) //Auto algorithim selection, it's nice.
                {
                    MatchSlow(); //Important cell, brute match.
                }
                else
                {
                    MatchFast(); //Meh cell, colour only.
                }
            }
        }

        private uint StdDev(Bitmap input)
        {
            return 0;
        }

        private void MatchFast() //Colour matching algorithim
        {
        }

        private void MatchSlow() //Brute force matching algorithim.
        {
        }

    }
}
