using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace TMV_Encoder__AForge_
{
    public class TMVFont
    {
        public byte[] font = new byte[16384];

        public TMVFont(string file) {
            FileStream fs = new FileStream(file, FileMode.Open); //read our font
            BinaryReader br = new BinaryReader(fs);
            byte c_row = 0;
            byte var = 0;
            for (int cha = 0; cha < 256; cha++) { //for each character
                for (int row = 0; row < 8; row++) {
                    c_row = br.ReadByte(); //get row byte

                    for (int x = 7; x >= 0; x--) { //loop through bitmasking each pixel
                        var = (byte)(128 >> (byte)x);
                        var = (byte)(var & c_row); //bitmask 
                        if (var > 0) {
                            font[(cha*64)+(row*8)+x] = 1;
                        }
                        else {
                            font[(cha * 64) + (row * 8) + x] = 0;
                        }
                    }
                }
            }
            br.Close();
            fs.Dispose();
        }

        public bool getPix(int cha, int n) {
            if (font[(cha * 64) + n] == 0)
            { return false; }
            else { return true; }
        }

        public bool getPixel(int cha, int x, int y) { //use with caution, seems to cause null refs.
            if (font[(cha * 64) + (y * 8) + x] == 0)
            { return false; }
            else { return true; }
        }

    }
}
