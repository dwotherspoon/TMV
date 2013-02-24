using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Drawing;

namespace TMV_Encoder__AForge_
{
    /* A video */
    public sealed class TMVVideo
    {
        private Queue<TMVFrame> frames; //Fifo 

        private byte[] audio_data;

        private decimal frameRate;

        private static string apath = AppDomain.CurrentDomain.BaseDirectory;

        public TMVVideo() {
            frames = new Queue<TMVFrame>();
        }

        public void addFrame(TMVFrame frame) {
            frames.Enqueue(frame);
        }

        public void loadAudio(byte[] audio) {
            audio_data = audio;
        }

        public void setFPS(decimal FPS) {
            frameRate = FPS;
        }

        public void save() {
            UInt16 achunksize = (UInt16)(audio_data.LongLength / frames.Count);
            UInt16 samplerate = (UInt16)(frameRate * achunksize);
            Console.WriteLine("Sample Rate: " + samplerate);
            Console.WriteLine("Chunk size: " + achunksize);
            FileStream fs = new FileStream(apath + "output.tmv", FileMode.Create);
            BinaryWriter bw = new BinaryWriter(fs);
            bw.Write('T');
            bw.Write('M');
            bw.Write('A');
            bw.Write('V');
            bw.Write(samplerate); //sample rate;
            bw.Write(achunksize); //audio chunk size
            bw.Write((byte)0); //comp method
            bw.Write((byte)40); //cols
            bw.Write((byte)25); //rows
            bw.Write((byte)0); //type
            TMVFrame  cframe;
            int frame = 0;
            while (frames.Count > 0) {
                cframe = frames.Dequeue();
                for (int cell = 0; cell < 1000; cell++) {
                    bw.Write((byte)cframe.getCellChar(cell));
                    bw.Write((byte)cframe.getCellCol(cell));
                }
                for (int sample = 0; sample < achunksize; sample++) {
                    bw.Write(audio_data[(frame * achunksize) + sample]);
                }
                frame++;
            }
        }
    }
}
