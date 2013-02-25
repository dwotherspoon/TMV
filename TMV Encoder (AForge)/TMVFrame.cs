using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;

namespace TMV_Encoder__AForge_
{
    public sealed class TMVFrame : ICloneable
    {
        private static Color[] colours = {   Color.FromArgb(000, 000, 000), Color.FromArgb(000, 000, 170), Color.FromArgb(000, 170, 000), Color.FromArgb(000, 170, 170), Color.FromArgb(170, 000, 000), 
                                     Color.FromArgb(170, 000, 170), Color.FromArgb(170, 085, 000), Color.FromArgb(170, 170, 170), Color.FromArgb(085, 085, 085), Color.FromArgb(085, 085, 255), 
                                     Color.FromArgb(085, 255, 085), Color.FromArgb(085, 255, 255), Color.FromArgb(255, 085, 085), Color.FromArgb(255, 085, 255), Color.FromArgb(255, 255, 085), 
                                     Color.FromArgb(255, 255, 255) }; //the cga 16 colour palette


        private FCell[] cells;


        public TMVFrame()
        {
            cells = new FCell[1000];
        }

        public void setCell(FCell cell, int index)
        {
            cells[index] = cell;
        }

        public byte getCellChar(int n)
        {
            return cells[n].character;
        }

        public byte getCellCol(int n)
        {
            byte col = (byte)(cells[n].colour2 << 4);
            return (byte)(col + cells[n].colour1);
        }

        public unsafe Bitmap renderFrame(TMVFont renderfont) {
            Bitmap result = new Bitmap(320, 200, PixelFormat.Format24bppRgb);
            BitmapData rData = result.LockBits(new Rectangle(0, 0, 320, 200), ImageLockMode.WriteOnly, result.PixelFormat);
            byte* pResult = (byte*)rData.Scan0.ToPointer();
            for (int c = 0; c < 1000; c++) {
                pResult = (byte*)rData.Scan0.ToPointer();
                pResult += ((c / 40) * (40 * 3 * 64)) + ((c % 40) * 8 * 3);
                for (int y = 0; y < 8; y++) {
                    for (int x = 0; x < 8; x++) {
                        if (renderfont.getPix(cells[c].character,(y*8) +x)) {
                            *pResult++ = colours[cells[c].colour1].B; //B
                            *pResult++ = colours[cells[c].colour1].G; //G
                            *pResult++ = colours[cells[c].colour1].R; //R
                        }
                        else {
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

        public override string ToString()
        {
            string result = "";
            for (int cell = 0; cell < 1000; cell++)
            {
                result += cells[cell].ToString();
            }
            return result;
        }

        public object Clone() {
            return this.MemberwiseClone();
        }
    }
}
