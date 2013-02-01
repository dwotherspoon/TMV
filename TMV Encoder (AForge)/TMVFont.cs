using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace TMV_Encoder__AForge_
{
    public sealed class TMVFont
    {
        private Boolean[][][] font = new Boolean[256][][];
        public TMVFont(string file)
        {
            for (int i = 0; i < 256; i++) //initialise our jagged array.
            {
                font[i] = new Boolean[8][];
                for (int j = 0; j < 8; j++)
                {
                    font[i][j] = new Boolean[8];
                }
            }

            FileStream fs = new FileStream(file, FileMode.Open); //read our font
            BinaryReader br = new BinaryReader(fs);
            byte c_row = 0;
            byte var = 0;
            for (int cha = 0; cha < 256; cha++) //for each character
            {
                for (int row = 0; row < 8; row++)
                {
                    c_row = br.ReadByte(); //get row byte

                    for (int x = 7; x >= 0; x--) //loop through bitmasking each pixel
                    {
                        var = (byte)(128 >> (byte)x);
                        var = (byte)(var & c_row); //bitmask 
                        if (var > 0)
                        {
                            font[cha][x][row] = true;
                        }
                        else
                        {
                            font[cha][x][row] = false;
                        }
                    }
                }
            }
            fs.Dispose();
        }

        public bool getPixel(int cha, int x, int y)
        {
            return font[cha][x][y];
        }

    }
}
