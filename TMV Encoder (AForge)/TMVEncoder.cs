using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using System.IO;

namespace TMV_Encoder__AForge_
{
    public sealed class TMVEncoder
    {
        /* The new encoder class, that's hopefully faster
            Begin the vars section:
         */
        public struct fcell
        {
            public byte character;
            public byte colour;
            public byte colourB;
            public bool done;
        }

        private static Color[] colours = {   Color.FromArgb(000, 000, 000), Color.FromArgb(000, 000, 170), Color.FromArgb(000, 170, 000), Color.FromArgb(000, 170, 170), Color.FromArgb(170, 000, 000), 
                                     Color.FromArgb(170, 000, 170), Color.FromArgb(170, 085, 000), Color.FromArgb(170, 170, 170), Color.FromArgb(085, 085, 085), Color.FromArgb(085, 085, 255), 
                                     Color.FromArgb(085, 255, 085), Color.FromArgb(085, 255, 255), Color.FromArgb(255, 085, 085), Color.FromArgb(255, 085, 255), Color.FromArgb(255, 255, 085), 
                                     Color.FromArgb(255, 255, 255) }; //the cga 16 colour palette
        private Thread[] workers;

        private Queue<Cell> unprocessed;

        private int threshold { get; set; }

        private ulong current_frame; //The count of the current frame.

        private static string apath = AppDomain.CurrentDomain.BaseDirectory; //exe directory for reading supporting binary files.
        private TMVFont[] fonts;

        private struct averages
        {
            private Color avg;
            private byte colour1;
            private byte colour2;
        }


        /* Methods: */

        public TMVEncoder()
        {
            threshold = 60; //default threshold value
            current_frame = 0;
            workers = new Thread[Environment.ProcessorCount]; //Initialise the workers.
            fonts = new TMVFont[workers.Length];
            for (int i = 0; i < workers.Length; i++)
            {
                fonts[i] = new TMVFont(apath + "font.bin"); //fonts for each worker.
            }
        }

        public void encoder(Bitmap input) //the main function, call this to encode a frame -> starts thread workers after breaking image down.
        {
            for (int row = 0; row < 25; row++) //for each row
            {
                for (int col = 0; col < 40; col++) // for each cell
                {
                    unprocessed.Enqueue(new Cell(getCell(input, row, col),(uint)((row*25)+(col*40)))); //add the new cell to our task pool
                }

            }
            for (int i = 0; i < workers.Length; i++ ) //start the workers on our cell pool
            {
                workers[i] = new Thread(worker);
                workers[i].Start();
            }
        }

        public unsafe Bitmap render(fcell[] input) //renders the last frame, fast!
        {
            TMVFont renderfont = new TMVFont(apath + "font.bin");
            Bitmap result = new Bitmap(320, 200, PixelFormat.Format24bppRgb);
            BitmapData rData = result.LockBits(new Rectangle(0, 0, 320, 200), ImageLockMode.WriteOnly, result.PixelFormat);
            byte* pResult = (byte*)rData.Scan0.ToPointer();
            for (int c = 0; c < 1000; c++)
            {
                pResult = (byte*)rData.Scan0.ToPointer();
                pResult += ((c / 40) * (40 * 3 * 64)) + ((c % 40) * 8 * 3);
                for (int y = 0; y < 8; y++)
                {
                    for (int x = 0; x < 8; x++)
                    {
                        if (renderfont.getPixel(input[c].character, x, y))
                        {
                            *pResult++ = colours[input[c].colour].B; //B
                            *pResult++ = colours[input[c].colour].G; //G
                            *pResult++ = colours[input[c].colour].R; //R
                        }
                        else
                        {
                            *pResult++ = colours[input[c].colourB].B; //B
                            *pResult++ = colours[input[c].colourB].G; //G
                            *pResult++ = colours[input[c].colourB].R; //R
                        }
                    }
                    pResult += (3 * 312); //jump to next row for char (3 bytes per pixel for 320 pixels, back 8)
                }
            }
            result.UnlockBits(rData);
            return result;
        }

        private unsafe Color[] getCell(Bitmap input, int row, int col) //gets our single cell, stored as a byte array this is faster than a bitmap.
        {
          BitmapData bdata = input.LockBits(new Rectangle(col * 8, row * 8, 8, 8), System.Drawing.Imaging.ImageLockMode.ReadOnly, input.PixelFormat);
          byte* pBdata = (byte*)bdata.Scan0.ToPointer();
          Color[] result = new Color[64];
          for (int pixel = 0; pixel < 64; pixel++)
          {
              if (bdata.PixelFormat == PixelFormat.Format32bppArgb)
              {
                  result[pixel] = Color.FromArgb(*pBdata++, *pBdata++, *pBdata++, *pBdata++); //load colour by pointers
              }
              else //24RGB
              {
                  result[pixel] = Color.FromArgb(*pBdata++, *pBdata++, *pBdata++);
              }
          }
          input.UnlockBits(bdata);
          return result;
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

        private double StdDev(Color[] input) //standard deviation of a cell
        {
            long totalR = 0;
            long totalG = 0;
            long totalB = 0;
            long sigmaR2 = 0;
            long sigmaG2 = 0;
            long sigmaB2 = 0;

            for (int i = 0; i < input.Length; i++) //pixel scan
            {
                totalR += input[i].R;
                totalG += input[i].G;
                totalB += input[i].B;

                sigmaR2 += (long)Math.Pow((input[i].R), 2);
                sigmaG2 += (long)Math.Pow((input[i].G), 2);
                sigmaB2 += (long)Math.Pow((input[i].B), 2);
            }

            double mRed = totalR / input.Length;
            mRed = Math.Pow(mRed, 2);
            double mGreen = totalG / input.Length;
            mGreen = Math.Pow(mGreen, 2);
            double mBlue = totalB / input.Length;
            mBlue = Math.Pow(mBlue, 2);

            double devRed = (sigmaR2 - (input.Length * mRed)) / (input.Length - 1);
            devRed = Math.Sqrt(devRed);

            double devGreen = (sigmaG2 - (input.Length * mGreen)) / (input.Length - 1);
            devGreen = Math.Sqrt(devGreen);

            double devBlue = (sigmaB2 - (input.Length * mBlue)) / (input.Length - 1);
            devBlue = Math.Sqrt(devBlue);

            return (devRed + devGreen + devBlue);
        }

        private void MatchFast() //Colour matching algorithim
        {
        }

        private void MatchSlow() //Brute force matching algorithim.
        {
        }
    }
}
