using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;

namespace TMV_Encoder__AForge_
{
    public class TMVFrame
    {
        private static Color[] colours = {   Color.FromArgb(000, 000, 000), Color.FromArgb(000, 000, 170), Color.FromArgb(000, 170, 000), Color.FromArgb(000, 170, 170), Color.FromArgb(170, 000, 000), 
                                     Color.FromArgb(170, 000, 170), Color.FromArgb(170, 085, 000), Color.FromArgb(170, 170, 170), Color.FromArgb(085, 085, 085), Color.FromArgb(085, 085, 255), 
                                     Color.FromArgb(085, 255, 085), Color.FromArgb(085, 255, 255), Color.FromArgb(255, 085, 085), Color.FromArgb(255, 085, 255), Color.FromArgb(255, 255, 085), 
                                     Color.FromArgb(255, 255, 255) }; //the cga 16 colour palette

        private static string apath = AppDomain.CurrentDomain.BaseDirectory;

        private FCell[] cells;

        private TMVFont renderfont;

        public TMVFrame()
        {
            cells = new FCell[1000];
            renderfont = new TMVFont(apath + "font.bin");
        }

        public void setCell(FCell cell, int index)
        {
            cells[index] = cell;
        }

        public unsafe Bitmap renderFrame()
        {
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
                        if (renderfont.getPixel(cells[c].character, x, y))
                        {
                            *pResult++ = colours[cells[c].colour1].B; //B
                            *pResult++ = colours[cells[c].colour1].G; //G
                            *pResult++ = colours[cells[c].colour1].R; //R
                        }
                        else
                        {
                            *pResult++ = colours[cells[c].colour2].B; //B
                            *pResult++ = colours[cells[c].colour2].G; //G
                            *pResult++ = colours[cells[c].colour2].R; //R
                        }
                    }
                    pResult += (3 * 312); //jump to next row for char (3 bytes per pixel for 320 pixels, back 8)
                }

            }
            result.UnlockBits(rData);
            return result;
        }
    }
}
