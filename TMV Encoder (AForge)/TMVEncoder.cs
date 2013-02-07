using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.Threading;
using System.IO;
using System.Windows.Forms;

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
        }

        private static Color[] colours = {   Color.FromArgb(000, 000, 000), Color.FromArgb(000, 000, 170), Color.FromArgb(000, 170, 000), Color.FromArgb(000, 170, 170), Color.FromArgb(170, 000, 000), 
                                     Color.FromArgb(170, 000, 170), Color.FromArgb(170, 085, 000), Color.FromArgb(170, 170, 170), Color.FromArgb(085, 085, 085), Color.FromArgb(085, 085, 255), 
                                     Color.FromArgb(085, 255, 085), Color.FromArgb(085, 255, 255), Color.FromArgb(255, 085, 085), Color.FromArgb(255, 085, 255), Color.FromArgb(255, 255, 085), 
                                     Color.FromArgb(255, 255, 255) }; //the cga 16 colour palette
        private Thread[] workers;

        private Queue<Cell> unprocessed;

        public int threshold { get; set; }

        public decimal brightness { get; set; }

        private static string apath = AppDomain.CurrentDomain.BaseDirectory; //exe directory for reading supporting binary files.
        private TMVFont[] fonts;

        private TMVFrame cframe;

        private struct averages
        {
            public Color avg;
            public byte colour1;
            public byte colour2;
        }

        private fcell[] frame;

        private averages[] avgs = new averages[136];
        /* Methods: */

        public TMVEncoder()
        {
            threshold = 60; //default threshold value
            brightness = (decimal)1.1; //default multiplier.
            workers = new Thread[Environment.ProcessorCount-1]; //Initialise the workers.
            fonts = new TMVFont[workers.Length];
            for (int i = 0; i < workers.Length; i++)
            {
                fonts[i] = new TMVFont(apath + "font.bin"); //fonts for each worker.
            }
            FileStream fs = new FileStream(apath + "Fcols.dat", FileMode.Open); //load fast colour averages
            BinaryReader br = new BinaryReader(fs);
            for (int i = 0; i < 136; i++)
            {
                avgs[i].colour1 = br.ReadByte();
                avgs[i].colour2 = br.ReadByte();
                avgs[i].avg = Color.FromArgb((int)br.ReadByte(), (int)br.ReadByte(), (int)br.ReadByte());
            }
            fs.Dispose();
            cframe = new TMVFrame();
        }

        public TMVFrame encode(Bitmap input) //the main function, call this to encode a frame -> starts thread workers after breaking image down.
        {
            unprocessed = new Queue<Cell>();
            for (int row = 0; row < 25; row++) //for each row
            {
                for (int col = 0; col < 40; col++) // for each cell
                {
                    unprocessed.Enqueue(new Cell(getCell(input, row, col),(uint)((row*40)+(col)))); //add the new cell to our task pool
                }

            }
            for (int i = 0; i < workers.Length; i++ ) //start the workers on our cell pool
            {
                workers[i] = new Thread(worker);
                workers[i].Start(i);
            }
            while (unprocessed.Count > 0)
            {
                System.Threading.Thread.Sleep(50);
                Application.DoEvents();
            }
            return cframe;
        }

        private unsafe Color[] getCell(Bitmap input, int row, int col) //gets our single cell, stored as a byte array this is faster than a bitmap.
        {
          BitmapData bdata = input.LockBits(new Rectangle(col * 8, row * 8, 8, 8), System.Drawing.Imaging.ImageLockMode.ReadOnly, input.PixelFormat);
          byte* pBdata = (byte*)bdata.Scan0.ToPointer();
          Color[] result = new Color[64];
          byte blue = 0;
          byte green = 0;
          byte red = 0;
          byte alpha = 0;
          for (int y = 0; y < 8; y++)
          {
              for (int x = 0; x < 8; x++)
              {
                  blue = *pBdata++;
                  if ((blue * brightness) > 255)
                  { blue = 255; }
                  else
                  { blue = (byte)(blue * brightness); } 

                  green = *pBdata++;
                  if ((green * brightness) > 255)
                  { green = 255; }
                  else
                  { green = (byte)(green * brightness); }

                  red = *pBdata++;
                  if ((red * brightness) > 255)
                  { red = 255; }
                  else
                  { red = (byte)(red * brightness); } 

                  alpha = *pBdata++;

                  result[(y * 8) + x] = Color.FromArgb(alpha, red,green,blue); //load colour by pointers n.b. bgr
              }
              pBdata += bdata.Stride - (4*8);
          }
          input.UnlockBits(bdata);
          return result;
        }

        private void worker(object i) //The worker function, runs until the queue is depleted.
        {
            Cell current;
            lock (unprocessed) //grab initial cell
            {
                if (unprocessed.Count > 0)
                {
                    current = unprocessed.Dequeue();
                }
                else
                {
                    current = null;
                }
            }
            while (current != null) //when queue is not empty, dequeue and encode
            {
                if (stdDev(current) > threshold) //Auto algorithim selection, it's nice.
                {
                    cframe.setCell(matchSlow(current, (int)i), (int)current.cellNum); //Important cell, brute match.
                }
                else
                {
                    cframe.setCell(matchFast(current), (int)current.cellNum); //Meh cell, colour only.
                }
                lock (unprocessed)
                {
                    if (unprocessed.Count > 0)
                    {
                        current = unprocessed.Dequeue();
                    }
                    else
                    {
                        current = null;
                    }
                }
            }
        }

        private double stdDev(Cell input) //standard deviation of a cell
        {
            long totalR = 0;
            long totalG = 0;
            long totalB = 0;
            long sigmaR2 = 0;
            long sigmaG2 = 0;
            long sigmaB2 = 0;

            for (int i = 0; i < 64; i++) //pixel scan
            {
                totalR += input.getPix(i).R;
                totalG += input.getPix(i).G;
                totalB += input.getPix(i).B;

                sigmaR2 += (input.getPix(i).R * input.getPix(i).R);
                sigmaG2 += (input.getPix(i).G * input.getPix(i).G);
                sigmaB2 += (input.getPix(i).B * input.getPix(i).B);
            }

            double mRed = totalR / 64;
            mRed = Math.Pow(mRed, 2);
            double mGreen = totalG / 64;
            mGreen = Math.Pow(mGreen, 2);
            double mBlue = totalB / 64;
            mBlue = Math.Pow(mBlue, 2);

            double devRed = (sigmaR2 - (64 * mRed)) / 63;
            devRed = Math.Sqrt(devRed);

            double devGreen = (sigmaG2 - (64 * mGreen)) / 63;
            devGreen = Math.Sqrt(devGreen);

            double devBlue = (sigmaB2 - (64 * mBlue)) / 63;
            devBlue = Math.Sqrt(devBlue);

            return (devRed + devGreen + devBlue);
        }

        private Color avgColour(Cell input) //average colour of a cell
        {
            uint sumRed = 0;
            uint sumGreen = 0;
            uint sumBlue = 0;

            for (int pix = 0; pix < 64; pix++)
            {
                sumRed += input.getPix(pix).R;
                sumGreen += input.getPix(pix).G;
                sumBlue += input.getPix(pix).B;
            }
            return Color.FromArgb((int)(sumRed / 64), (int)(sumGreen / 64), (int)(sumBlue / 64));
        }

        private FCell matchFast(Cell input) //Colour matching algorithim
        {
            FCell result = new FCell();
            result.character = 177;
            result.colour1 = 0;
            result.colour2 = 0;
            int diff = 0;
            int min = int.MaxValue;
            Color avg = avgColour(input);

            for (int i = 0; i < 136; i++)
            {
                diff = Math.Abs(avgs[i].avg.R - avg.R) + Math.Abs(avgs[i].avg.G - avg.G) + Math.Abs(avgs[i].avg.B - avg.B);
                if (diff < min)
                {
                    min = diff;
                    result.colour1 = avgs[i].colour1;
                    result.colour2 = avgs[i].colour2;
                }
            }
            return result;
        }

        private FCell matchSlow(Cell input, int i)
        {
            FCell result = new FCell();
            result.character = 0;
            result.colour1 = 0;
            result.colour2 = 13;
            //find most popular colours

            int diff1;
            int diff2;
            int min = int.MaxValue;
            byte[] mcommon = getMCommon(input);
            for (int cha = 3; cha < 255; cha++)
            {
                diff1 = 0;
                diff2 = 0;
                //only two things to try here.
                for (int pixel = 0; pixel < 64; pixel++)
                {
                        if (fonts[i].getPix(cha, pixel))
                        {
                            diff1 += Math.Abs(input.getPix(pixel).R - colours[mcommon[0]].R) + Math.Abs(input.getPix(pixel).G - colours[mcommon[0]].G) + Math.Abs(input.getPix(pixel).B - colours[mcommon[0]].B); //0 and 1
                            diff2 += Math.Abs(input.getPix(pixel).R - colours[mcommon[1]].R) + Math.Abs(input.getPix(pixel).G - colours[mcommon[1]].G) + Math.Abs(input.getPix(pixel).B - colours[mcommon[1]].B); //1 and 0

                        }
                        else
                        {
                            diff1 += Math.Abs(input.getPix(pixel).R - colours[mcommon[1]].R) + Math.Abs(input.getPix(pixel).G - colours[mcommon[1]].G) + Math.Abs(input.getPix(pixel).B - colours[mcommon[1]].B); //0 and 1
                            diff2 += Math.Abs(input.getPix(pixel).R - colours[mcommon[0]].R) + Math.Abs(input.getPix(pixel).G - colours[mcommon[0]].G) + Math.Abs(input.getPix(pixel).B - colours[mcommon[0]].B); //1 and 0
                        }
                }
                if (diff1 < min)
                {
                    min = diff1;
                    result.character = (byte)cha;
                    result.colour1 = (byte)mcommon[0];
                    result.colour2 = (byte)mcommon[1];
                }

                if (diff2 < min)
                {
                    min = diff2;
                    result.character = (byte)cha;
                    result.colour1 = (byte)mcommon[1];
                    result.colour2 = (byte)mcommon[0];
                }

            }
            return result;
        }

        private byte[] getMCommon(Cell input)
        {
            byte[] results = new byte[16]; //stores occurences

            int min;
            int minval;
            int diff;
            for (int pixel = 0; pixel < 64; pixel++)
            {
                minval = int.MaxValue;
                min = 0;
                for (int colour = 0; colour < 16; colour++)
                {
                    diff = Math.Abs(colours[colour].R - input.getPix(pixel).R) + Math.Abs(colours[colour].G - input.getPix(pixel).G) + Math.Abs(colours[colour].B - input.getPix(pixel).B);
                    if (diff < minval)
                    {
                        minval = diff;
                        min = colour;
                    }
                }
                results[min] += 1;
            }

            byte[] output = new byte[2];
            int max = 0;

            for (int c = 0; c < 16; c++)
            {
                if (results[c] > max)
                {
                    max = results[c];
                    output[0] = (byte)c;
                }
            }

            if (output[0] == 0) //tweak results if we get black, as grey will often follow obbliterating detail
            {
                results[8] = (byte)(results[8] * 0.3);
                results[7] = (byte)(results[7] * 0.6);

            }
            else if (output[0] == 8)
            {
                results[0] = (byte)(results[8] * 0.6);
                results[7] = (byte)(results[7] * 0.6);
            }
            results[output[0]] = 0;
            max = 0;

            for (int c = 0; c < 16; c++)
            {
                if (results[c] > max)
                {
                    max = results[c];
                    output[1] = (byte)c;
                }
            }

            max = 0;
            return output;
        }

        private fcell matchVSlow(Cell input, int i) //Brute force matching algorithim, not used
        {
            fcell result;
            result.character = 0;
            result.colour = 13;
            result.colourB = 13;
            uint diff;
            uint min = uint.MaxValue;
            for (byte cha = 3; cha < 255; cha++)
            {
                for (byte col1 = 0; col1 < 16; col1++)
                {
                    for (byte col2 = 0; col2 < 16; col2++)
                    {
                        if (col1 != col2) //try and save some time 
                        {
                            diff = 0;
                            for (int pixel = 0; pixel < 64; pixel++)
                            {
                                if (fonts[i].getPix(cha, pixel))
                                {
                                    diff += (uint)(Math.Abs(input.getPix(pixel).R - colours[col1].R) + Math.Abs(input.getPix(pixel).G - colours[col1].G) + Math.Abs(input.getPix(pixel).B - colours[col1].B));
                                }
                                else
                                {
                                    diff += (uint)(Math.Abs(input.getPix(pixel).R - colours[col2].R) + Math.Abs(input.getPix(pixel).G - colours[col2].G) + Math.Abs(input.getPix(pixel).B - colours[col2].B));
                                }
                            }
                            if (diff < min)
                            {
                                min = diff;
                                result.character = cha;
                                result.colour = col1;
                                result.colourB = col2;
                            }
                        }
                    }
                }
            }
            return result;
        }
    }
}
