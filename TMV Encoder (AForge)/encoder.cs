using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

/* CGA Encoder/rederer, depends on Code Page 437 loaded as font.bin (8x8 - laid out little endian with each byte being a row top to bottom with bits left to right) 
and cols.dat which is a precalculated file based on font.bin and the "average" CGA palette for #177 -136 combos. Also includes TMV export functionality. All code
 is speed and not memory optimised. */

namespace TMV_Encoder__AForge_
{
    public sealed class encoder
    {

        //the CGA 16 colour palette
        public static Color[] colours = {   Color.FromArgb(000, 000, 000), Color.FromArgb(000, 000, 170), Color.FromArgb(000, 170, 000), Color.FromArgb(000, 170, 170), Color.FromArgb(170, 000, 000), 
                                     Color.FromArgb(170, 000, 170), Color.FromArgb(170, 085, 000), Color.FromArgb(170, 170, 170), Color.FromArgb(085, 085, 085), Color.FromArgb(085, 085, 255), 
                                     Color.FromArgb(085, 255, 085), Color.FromArgb(085, 255, 255), Color.FromArgb(255, 085, 085), Color.FromArgb(255, 085, 255), Color.FromArgb(255, 255, 085), 
                                     Color.FromArgb(255, 255, 255) };

        public struct colc
        {
            public Color avg;
            public byte col1;
            public byte col2;
        }

        public struct cell
        {
            public byte character;
            public byte colour;
            public byte colourB;
            public bool done;
        }

        public struct TMVFrame
        {
            public cell[] cells;
        }


        public static Bitmap[] chars1 = new Bitmap[256];
        public static Bitmap[] chars2 = new Bitmap[256];
        public Bitmap output = new Bitmap(320, 200);

        public static colc[] avgs = new colc[136];
        public TMV_Encoder__AForge_.Main source;

        public FileStream fs;
        public BinaryReader br;
        public TMVFrame[] ovideo;

        public UInt16 SampleRate;

        public cell[] result;

        private int _threshold = 60;

        public int Threshold
        {
            set { this._threshold = value; }
            get { return this._threshold; }
        }

        public long curr_frame;
        public static string apath = AppDomain.CurrentDomain.BaseDirectory;

        public encoder(string output, long num) //constructor loads font and average colour table into memory
        {
            curr_frame = 0;
            ovideo = new TMVFrame[num];
            FileStream fs = new FileStream(apath+"font.bin", FileMode.Open); //read our font from the binary
            BinaryReader br = new BinaryReader(fs);
            byte temp = 0; //needs shortening ----
            byte var = 0;
            for (int i = 0; i < 256; i++)
            {
                chars1[i] = new Bitmap(8, 8);
                chars2[i] = new Bitmap(8, 8);
                for (int row = 0; row < 8; row++)
                {
                    temp = br.ReadByte(); //get row byte

                    for (int c = 7; c >= 0; c--)
                    {
                        var = (byte)(1 << (byte)c);
                        var = (byte)(var & temp); //bitmask 
                        if (var != 0)
                        {
                            chars1[i].SetPixel((7 - c), row, Color.White); //7-c reverses endian.
                            chars2[i].SetPixel((7 - c), row, Color.White); //7-c reverses endian.
                        }
                        else
                        {
                            chars1[i].SetPixel((7 - c), row, Color.Black);
                            chars2[i].SetPixel((7 - c), row, Color.Black);
                        }
                    }
                }
            }
            fs.Dispose();
            fs = new FileStream(apath+"Fcols.dat", FileMode.Open); //load fast colour averages
            br = new BinaryReader(fs);
            for (int i = 0; i < 136; i++)
            {
                avgs[i].col1 = br.ReadByte();
                avgs[i].col2 = br.ReadByte();
                avgs[i].avg = Color.FromArgb((int)br.ReadByte(), (int)br.ReadByte(), (int)br.ReadByte());
            }

            fs.Dispose();
        }

        /*
        public Bitmap render(cell[] input) //input length must be 1000 or else. Renders into a bitmap based on the cell
        {
            Bitmap result = new Bitmap(320, 200);
            int ccount = 0;
            Graphics g = Graphics.FromImage(result);
            g.Clear(Color.Black);
            for (int row = 0; row < 25; row++) //scan top to bottom, left to right each time.
            {
                for (int col = 0; col < 40; col++)
                {
                    g.DrawImage(getFBitmap(input[ccount]), col * 8, row * 8, 8, 8); //draw from font bitmap
                    ccount++;
                }
            }
            return result;
        } */

        public unsafe Bitmap render(cell[] input) //renders the last frame
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

        public Bitmap encode(Bitmap input) //MUST BE 320x200 OR STUFF WILL GO WRONG
        {
            result = new cell[1000];


            /*
            Bitmap bcell;
            int ccount = 0;
            for (int row = 0; row < 25; row++)
            {
                for (int col = 0; col < 40; col++)
                {
                    bcell = cropImage(input, new Rectangle((8 * col), (8 * row), 8, 8)); //Automatic Algorithim Selection
                    if (getStdDev(bcell) < _threshold) //compare to threshold 60
                    {
                        result[ccount] = matchFast(bcell);
                    }
                    else
                    {
                        result[ccount] = matchSlow(bcell);
                    }
                    ccount++;
                    Application.DoEvents();
                }
            }*/

            Bitmap input2 = (Bitmap)input.Clone();
            Thread alpha = new Thread(forward_pass);
            alpha.Start(input);

            Thread beta = new Thread(backward_pass);
            beta.Start(input2);

            while (alpha.IsAlive || beta.IsAlive)
            {
                System.Threading.Thread.Sleep(50);
                Application.DoEvents();
            }

            alpha.Join();
            beta.Join();

            ovideo[curr_frame].cells = result;
            curr_frame++;
            return render(result);
        }

        private void forward_pass(object src)
        {
            Bitmap input = (Bitmap)src;
            Bitmap bcell;
            int ccount = 0;
            for (int row = 0; row < 25; row++)
            {
                for (int col = 0; col < 40; col++)
                {
                    if (result[ccount].done)
                    {
                        return;
                    }
                    bcell = cropImage(input, new Rectangle((8 * col), (8 * row), 8, 8)); //Automatic Algorithim Selection
                    if (getStdDev(bcell) < _threshold) //compare to threshold 60
                    {
                        result[ccount] = matchFast(bcell);
                    }
                    else
                    {
                        result[ccount] = matchSlow(bcell, chars1);
                    }
                    ccount++;
                    Application.DoEvents();
                }
            }
        }

        private void backward_pass(object src)
        {
            Bitmap input = (Bitmap)src;
            Bitmap bcell;
            int ccount = 999;
            for (int row = 24; row >= 0; row--)
            {
                for (int col = 39; col >= 0; col--)
                {
                    if (result[ccount].done)
                    {
                        return;
                    }
                    bcell = cropImage(input, new Rectangle((8 * col), (8 * row), 8, 8)); //Automatic Algorithim Selection
                    if (getStdDev(bcell) < _threshold) //compare to threshold 60
                    {
                        result[ccount] = matchFast(bcell);
                    }
                    else
                    {
                        result[ccount] = matchSlow(bcell,chars2);
                    }
                    ccount--;
                    Application.DoEvents();
                }
            }
        }

        public void save(decimal fps, byte[] sound_data)
        {
            FileStream fs = new FileStream(apath+"output.tmv", FileMode.Create);
            BinaryWriter bw = new BinaryWriter(fs);
            UInt16 chunk_size = (UInt16)(sound_data.Length/ovideo.LongLength);
            bw.Write('T');
            bw.Write('M');
            bw.Write('A');
            bw.Write('V');
            bw.Write(SampleRate); //sample rate;
            bw.Write(chunk_size); //audio chunk size
            bw.Write((byte)0); //comp method
            bw.Write((byte)40); //cols
            bw.Write((byte)25); //rows
            bw.Write((byte)0); //type
            for (int c = 0; c < ovideo.LongLength; c++) //for each frame
            {
                byte col = 0;
                for (int i = 0; i < 1000; i++)
                {
                    bw.Write(ovideo[c].cells[i].character);
                    col = (byte)(ovideo[c].cells[i].colourB << 4);
                    col += ovideo[c].cells[i].colour;
                    bw.Write(col);
                }
                for (int i = 0; i < chunk_size; i++)
                {
                    bw.Write(sound_data[(c*chunk_size) + i]);
                }
            }
            fs.Dispose();
        }

        private Bitmap cropImage(Bitmap input, Rectangle cropArea)
        {
            Bitmap cell = input.Clone(cropArea, input.PixelFormat);
            Graphics g = Graphics.FromImage(cell);
            g.DrawImageUnscaled(cell, 0, 0, 8, 8);
            return cell;
        }

        private cell matchFast(Bitmap input) //fast match
        {
            cell result;
            result.character = 177;
            result.colour = 0;
            result.colourB = 0;
            int diff = 0;
            int min = int.MaxValue;
            Color avg = getAvgColour(input);

            for (int i = 0; i < 136; i++)
            {
                diff = Math.Abs(avgs[i].avg.R - avg.R) + Math.Abs(avgs[i].avg.G - avg.G) + Math.Abs(avgs[i].avg.B - avg.B);
                if (diff < min)
                {
                    min = diff;
                    result.colour = avgs[i].col1;
                    result.colourB = avgs[i].col2;
                }
            }
            result.done = true;
            return result;
        }

        private cell matchSlow(Bitmap input, Bitmap[] chars) //brute force match
        {
            cell result;
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
                for (int x = 0; x < 8; x++)
                {
                    for (int y = 0; y < 8; y++)
                    {
                        if (chars[cha].GetPixel(x, y).R > 0)
                        {
                            diff1 += Math.Abs(input.GetPixel(x, y).R - colours[mcommon[0]].R) + Math.Abs(input.GetPixel(x, y).G - colours[mcommon[0]].G) + Math.Abs(input.GetPixel(x, y).B - colours[mcommon[0]].B); //0 and 1
                            diff2 += Math.Abs(input.GetPixel(x, y).R - colours[mcommon[1]].R) + Math.Abs(input.GetPixel(x, y).G - colours[mcommon[1]].G) + Math.Abs(input.GetPixel(x, y).B - colours[mcommon[1]].B); //1 and 0

                        }
                        else
                        {
                            diff1 += Math.Abs(input.GetPixel(x, y).R - colours[mcommon[1]].R) + Math.Abs(input.GetPixel(x, y).G - colours[mcommon[1]].G) + Math.Abs(input.GetPixel(x, y).B - colours[mcommon[1]].B); //0 and 1
                            diff2 += Math.Abs(input.GetPixel(x, y).R - colours[mcommon[0]].R) + Math.Abs(input.GetPixel(x, y).G - colours[mcommon[0]].G) + Math.Abs(input.GetPixel(x, y).B - colours[mcommon[0]].B); //1 and 0
                        }
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
            result.done = true;
            return result;
        }

        private byte[] getMCommon(Bitmap input)
        {
            byte[] results = new byte[16]; //stores occurences

            int min;
            int minval;
            int diff;

            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 8; y++)
                {
                    minval = int.MaxValue;
                    min = 0;
                    for (int colour = 0; colour < 16; colour++)
                    {
                        diff = Math.Abs(colours[colour].R - input.GetPixel(x, y).R) + Math.Abs(colours[colour].G - input.GetPixel(x, y).G) + Math.Abs(colours[colour].B - input.GetPixel(x, y).B);
                        if (diff < minval)
                        {
                            minval = diff;
                            min = colour;
                        }
                    }
                    results[min] += 1;
                }
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

        public static Bitmap getFBitmap(cell input)
        {
            Bitmap result = new Bitmap(8, 8);
            for (int row = 0; row <= 7; row++)
            {
                for (int col = 0; col < 8; col++)
                {
                    if (chars1[input.character].GetPixel(col, row).R > 0) //getFpixel(input.character, row, col))
                    {
                        result.SetPixel(col, row, colours[input.colour]);
                    }
                    else
                    {
                        result.SetPixel(col, row, colours[input.colourB]);
                    }
                }
            }
            return result;
        }


        public static double getStdDev(Bitmap input)
        {
            long totalR = 0;
            long totalG = 0;
            long totalB = 0;
            long sigmaR2 = 0;
            long sigmaG2 = 0;
            long sigmaB2 = 0;

            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 8; y++)
                {
                    totalR += input.GetPixel(x, y).R;
                    totalG += input.GetPixel(x, y).G;
                    totalB += input.GetPixel(x, y).B;

                    sigmaR2 += (long)Math.Pow((input.GetPixel(x, y).R), 2);
                    sigmaG2 += (long)Math.Pow((input.GetPixel(x, y).G), 2);
                    sigmaB2 += (long)Math.Pow((input.GetPixel(x, y).B), 2);
                }
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

        public static Color getAvgColour(Bitmap input)
        {
            int TRed = 0;
            int TBlue = 0;
            int TGreen = 0;
            for (int x = 0; x < input.Width; x++)
            {
                for (int y = 0; y < input.Height; y++)
                {
                    TRed += input.GetPixel(x, y).R;
                    TGreen += input.GetPixel(x, y).G;
                    TBlue += input.GetPixel(x, y).B;
                }
            }
            TRed = (int)(TRed / (input.Width * input.Height));
            TGreen = (int)(TGreen / (input.Width * input.Height));
            TBlue = (int)(TBlue / (input.Width * input.Height));
            Color result = Color.FromArgb(TRed, TGreen, TBlue);
            return result;
        }
    }
}
