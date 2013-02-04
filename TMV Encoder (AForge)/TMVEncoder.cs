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

        public  int threshold { get; set; }

        private ulong current_frame; //The count of the current frame.

        private static string apath = AppDomain.CurrentDomain.BaseDirectory; //exe directory for reading supporting binary files.
        private TMVFont[] fonts;

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
            current_frame = 0;
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
        }

        public Bitmap encode(Bitmap input) //the main function, call this to encode a frame -> starts thread workers after breaking image down.
        {
            frame = new fcell[1000];
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
            return render(frame);
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
          byte blue = 0;
          byte green = 0;
          byte red = 0;
          byte alpha = 0;
          for (int y = 0; y < 8; y++)
          {
              for (int x = 0; x < 8; x++)
              {
                  alpha = *pBdata++;
                  blue = *pBdata++;
                  green = *pBdata++;
                  red = *pBdata++;
                  result[(y*8)+x] = Color.FromArgb(red, green, blue, alpha); //load colour by pointers n.b. bgr
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
                if (stdDev(current.src) > threshold) //Auto algorithim selection, it's nice.
                {
                   frame[current.cellNum] = matchSlow(current, (int)i); //Important cell, brute match.
                }
                else
                {
                    frame[current.cellNum] = matchFast(current); //Meh cell, colour only.
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

        private double stdDev(Color[] input) //standard deviation of a cell
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

        private Color avgColour(Cell input) //average colour of a cell
        {
            uint sumRed = 0;
            uint sumGreen = 0;
            uint sumBlue = 0;

            for (int pix = 0; pix < 64; pix++)
            {
                sumRed += input.src[pix].R;
                sumGreen += input.src[pix].G;
                sumBlue += input.src[pix].B;
            }
            return Color.FromArgb((int)(sumRed / 64), (int)(sumGreen / 64), (int)(sumBlue / 64));
        }

        private fcell matchFast(Cell input) //Colour matching algorithim
        {
            fcell result;
            result.character = 177;
            result.colour = 0;
            result.colourB = 0;
            int diff = 0;
            int min = int.MaxValue;
            Color avg = avgColour(input);

            for (int i = 0; i < 136; i++)
            {
                diff = Math.Abs(avgs[i].avg.R - avg.R) + Math.Abs(avgs[i].avg.G - avg.G) + Math.Abs(avgs[i].avg.B - avg.B);
                if (diff < min)
                {
                    min = diff;
                    result.colour = avgs[i].colour1;
                    result.colourB = avgs[i].colour2;
                }
            }
            return result;
        }

        private fcell matchSlow(Cell input, int i)
        {
            fcell result;
            result.character = 0;
            result.colour = 0;
            result.colourB = 13;
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
                            diff1 += Math.Abs(input.src[pixel].R - colours[mcommon[0]].R) + Math.Abs(input.src[pixel].G - colours[mcommon[0]].G) + Math.Abs(input.src[pixel].B - colours[mcommon[0]].B); //0 and 1
                            diff2 += Math.Abs(input.src[pixel].R - colours[mcommon[1]].R) + Math.Abs(input.src[pixel].G - colours[mcommon[1]].G) + Math.Abs(input.src[pixel].B - colours[mcommon[1]].B); //1 and 0

                        }
                        else
                        {
                            diff1 += Math.Abs(input.src[pixel].R - colours[mcommon[1]].R) + Math.Abs(input.src[pixel].G - colours[mcommon[1]].G) + Math.Abs(input.src[pixel].B - colours[mcommon[1]].B); //0 and 1
                            diff2 += Math.Abs(input.src[pixel].R - colours[mcommon[0]].R) + Math.Abs(input.src[pixel].G - colours[mcommon[0]].G) + Math.Abs(input.src[pixel].B - colours[mcommon[0]].B); //1 and 0
                        }
                }

                if (diff1 < min)
                {
                    min = diff1;
                    result.character = (byte)cha;
                    result.colour = (byte)mcommon[0];
                    result.colourB = (byte)mcommon[1];
                }

                if (diff2 < min)
                {
                    min = diff2;
                    result.character = (byte)cha;
                    result.colour = (byte)mcommon[1];
                    result.colourB = (byte)mcommon[0];
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
                    diff = Math.Abs(colours[colour].R - input.src[pixel].R) + Math.Abs(colours[colour].G - input.src[pixel].G) + Math.Abs(colours[colour].B - input.src[pixel].B);
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

        private fcell matchVSlow(Cell input, int i) //Brute force matching algorithim.
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
                                    diff += (uint)(Math.Abs(input.src[pixel].R - colours[col1].R) + Math.Abs(input.src[pixel].G - colours[col1].G) + Math.Abs(input.src[pixel].B - colours[col1].B));
                                }
                                else
                                {
                                    diff += (uint)(Math.Abs(input.src[pixel].R - colours[col2].R) + Math.Abs(input.src[pixel].G - colours[col2].G) + Math.Abs(input.src[pixel].B - colours[col2].B));
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
