
using System;
using System.Threading;
using System.Diagnostics;
using System.IO.Ports;

namespace mcutils
{
    public class SPI
    {
        private int b_SCK;
        private int b_MOSI;
        private int b_MISO;
        private int b_RESET;

        private IPortIO port;
        private int state;

        private Stopwatch sw;
        public int[] data;

        protected bool invertedRESET;
        protected bool invertedMISO;
        protected bool invertedSCK;
        protected int bitDelay;

        private bool serialMode;
        private SerialPort serialPort;
        private int[] serialData;
        private byte[] tmp;
        private int serialDataSize, serialDataState;
        private bool serialReady;

        public SPI (IPortIO port, int SCK, int MOSI, int MISO, int RESET)
        {
            this.sw = new Stopwatch ();
            this.data = new int[1024];

            this.serialMode = false;
            this.port = port;
            this.state = 0;

            this.b_SCK = 1 << Math.Abs(SCK);
            this.b_MOSI = 1 << Math.Abs(MOSI);
            this.b_MISO = 1 << Math.Abs(MISO);
            this.b_RESET = 1 << Math.Abs(RESET);

            this.invertedRESET = RESET < 0 ? true : false;
            this.invertedMISO = MISO < 0 ? true : false;
            this.invertedSCK = SCK < 0 ? true : false;

            this.bitDelay = 1;

            this.SCK(0);
            this.MOSI(0);
            this.RESET(0);
        }
        
        public SPI (SerialPort port)
        {
            this.serialMode = true;
            this.serialPort = port;
            this.serialPort.DataReceived += new SerialDataReceivedEventHandler(serialDataRecv);

            serialData = new int[1024];
            tmp = new byte[128];
            serialDataSize = 0;
            serialDataState = 0;
            serialReady = false;

            this.sw = new Stopwatch ();
            this.data = new int[4];

            this.state = 0;

            this.invertedRESET = false;
            this.invertedMISO = false;
            this.invertedSCK = false;

            this.bitDelay = 1;
        }

        int HexToInt (int value)
        {
            if (value >= 0x30 && value <= 0x39)
                return value - 0x30;
            
            value &= 0xDF;

            if (value >= 0x41 && value <= 0x46)
                return value - 0x41 + 0x0A;

            return 0;
        }

        void serialDataRecv (object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort port = (SerialPort)sender;
            if (!port.IsOpen) return;

            try
            {
                if (serialDataState == 0)
                {
                    serialDataSize = 0;
                    serialDataState = 1;
                }

                while (port.BytesToRead > 0)
                {
                    int val = port.ReadByte();
                    if (val == 10)
                    {
                        serialDataState = 0;
                        serialReady = true;
                        break;
                    }

                    if (val <= 32) continue;

                    serialData[serialDataSize++] = val;

                    if (serialDataState == 1)
                    {
                        serialDataState = 2;
                        continue;
                    }

                    if (serialDataState == 2)
                    {
                        serialData[serialDataSize-2] = HexToInt(serialData[serialDataSize-2])*16 + HexToInt(serialData[serialDataSize-1]);
                        serialDataSize--;

                        serialDataState = 1;
                        continue;
                    }
                }
            }
            catch (Exception ex) {
            }
        }

        public SPI (IPortIO port, int SCK, int MOSI)
        {
            this.sw = new Stopwatch ();
            this.data = new int[1024];

            this.port = port;
            this.state = 0;

            this.b_SCK = 1 << Math.Abs(SCK);
            this.b_MOSI = 1 << Math.Abs(MOSI);

            this.invertedSCK = SCK < 0 ? true : false;
            this.bitDelay = 1;
        }

        public SPI Configure (bool invertedRESET, bool invertedMISO, bool invertedSCK, int reset, int sck)
        {
            this.invertedRESET = invertedRESET;
            this.invertedMISO = invertedMISO;
            this.invertedSCK = invertedSCK;

            if (serialMode)
            {
                serialPort.WriteLine("C5" + ((invertedRESET ? 0x80 : 0) | (invertedSCK ? 0x40 : 0)).ToString("X2"));
                if (!waitSerial(250)) return this;

                return this;
            }

            RESET(reset);
            SCK(sck);

            return this;
        }

        private IPortIO GetPort()
        {
            return this.port;
        }

        public bool testSerial()
        {
            int tries = 3;

            while (tries-- != 0)
            {
                serialPort.WriteLine("C0AA55");
                if (!waitSerial(1000)) return false;

                if (serialDataSize != 3)
                    continue;

                if (serialData[0] != 0x20)
                    continue;

                if (serialData[1] != 0xAA)
                    continue;

                if (serialData[2] != 0x55)
                    continue;

                return true;
            }

            return false;
        }

        private void serialDump()
        {
            Console.Write("<<< ");

            for (int i = 0; i < serialDataSize; i++)
                Console.Write(serialData[i].ToString("X2") + " ");

            Console.Write("\n");
        }

        public bool waitSerial (long millis)
        {
            long ticks = (long)(((float)millis / 1e3) * Stopwatch.Frequency);

            sw.Reset();
            sw.Start();

            while (sw.ElapsedTicks < ticks && !serialReady);

            sw.Stop();

            bool isReady = serialReady;
            serialReady = false;

            return isReady;
        }

        // Affected by "invertedSCK".
        private void SCK (int value)
        {
            if (invertedSCK)
                value = value != 0 ? 0 : 1;

            if (value != 0) state |= b_SCK; else state &= ~b_SCK;
            port.WriteByte(state);
        }

        private void MOSI (int value)
        {
            if (value != 0) state |= b_MOSI; else state &= ~b_MOSI;
            port.WriteByte(state);
        }

        // Affected by "invertedRESET".
        private void RESET (int value)
        {
            if (invertedRESET)
                value = value != 0 ? 0 : 1;

            if (value != 0) state |= b_RESET; else state &= ~b_RESET;
            port.WriteByte(state);
        }

        // Affected by "invertedMISO".
        private int MISO ()
        {
            return (port.ReadByte() & b_MISO) != (invertedMISO ? b_MISO : 0) ? 1 : 0;
        }

        public void DelayMicroseconds (int micros)
        {
            // violet
            // Maybe needs a delay?
            return;

            if (micros == 0)
                return;

            long ticks = (long)(((float)micros / 1e6) * Stopwatch.Frequency);

            sw.Reset();
            sw.Start();

            while (sw.ElapsedTicks < ticks);

            sw.Stop();
        }

        public void DelayMilliseconds (int millis)
        {
            long ticks = (long)(((float)millis / 1e3) * Stopwatch.Frequency);

            sw.Reset();
            sw.Start();

            while (sw.ElapsedTicks < ticks);

            sw.Stop();
        }

        public int WriteByte (int value)
        {
            if (serialMode)
            {
                serialPort.WriteLine("C3" + value.ToString("X2"));
                if (!waitSerial(2500)) return 0;

                if (serialDataSize != 2)
                    return 0;

                if (serialData[0] != 0x20)
                    return 0;
    
                return serialData[1];
            }

            int r_value = 0;

            for (int i = 0; i < 8; i++)
            {
                MOSI(value & 0x80);
                SCK(1);
                DelayMicroseconds(bitDelay);

                value <<= 1;

                r_value <<= 1;
                r_value |= MISO();

                SCK(0);
                DelayMicroseconds(bitDelay);
            }

            return r_value;
        }

        private int WriteBit (int value)
        {
            int r_value;

            MOSI(value);
            SCK(1);
            DelayMicroseconds(bitDelay);

            r_value = MISO();
            SCK(0);

            return r_value;
        }

        public void Reset ()
        {
            if (serialMode)
            {
                serialPort.WriteLine("C5" + ((invertedRESET ? 0x80 : 0) | (invertedSCK ? 0x40 : 0)).ToString("X2"));
                waitSerial(2500);

                serialPort.WriteLine("C1");
                waitSerial(2500);

                serialPort.WriteLine("C2");
                waitSerial(2500);

                DelayMilliseconds(500);
                return;
            }

            SCK(0);
            RESET(1);
            RESET(0);
            DelayMilliseconds(500);
        }

        public void Normal ()
        {
            if (serialMode)
            {
                serialPort.WriteLine("C1");
                if (!waitSerial(250)) return;

                DelayMilliseconds(500);
                return;
            }

            SCK(0);
            RESET(1);
        }

        public bool IsSerial ()
        {
            return this.serialMode;
        }

        private int s_index;
        
        public void SpiStart()
        {
            s_index = 3;
        }

        public void SpiPush (int value)
        {
            tmp[s_index++] = (byte)value;
        }

        public bool SpiEnsure (int count)
        {
            if (s_index + count >= 128)
                return SpiFlush();

            return true;
        }

        public bool SpiFlush()
        {
            if (serialMode == false)
                return true;

            if (s_index == 3) return true;

            int n = s_index;
            SpiStart();

            tmp[0] = 0x21;
            tmp[1] = (byte)(n - 2);
            tmp[2] = 0xC3;

            serialPort.Write(tmp, 0, n);
            if (!waitSerial(2500)) return false;

            if (serialDataSize != n - 2)
                return false;

            if (serialData[0] != 0x20)
                return false;

            return true;
        }

        public int SpiDataSize()
        {
            return serialDataSize;
        }

        public int[] SpiData()
        {
            return serialData;
        }

        public void Command (int v0, int v1, int v2, int v3)
        {
            if (serialMode)
            {
                tmp[0] = 0x21;
                tmp[1] = 5;
                tmp[2] = 0xC3;
                tmp[3] = (byte)v0;
                tmp[4] = (byte)v1;
                tmp[5] = (byte)v2;
                tmp[6] = (byte)v3;

                serialPort.Write(tmp, 0, 7);
                //serialPort.WriteLine("C3" + v0.ToString("X2") + v1.ToString("X2") + v2.ToString("X2") + v3.ToString("X2"));
                if (!waitSerial(2500)) return;

                if (serialDataSize != 5)
                    return;

                if (serialData[0] != 0x20)
                    return;

                data[0] = serialData[1];
                data[1] = serialData[2];
                data[2] = serialData[3];
                data[3] = serialData[4];
            }
            else
            {
                data[0] = WriteByte(v0);
                data[1] = WriteByte(v1);
                data[2] = WriteByte(v2);
                data[3] = WriteByte(v3);
            }

//Console.WriteLine(">> " + data[0].ToString("X2") + " " + data[1].ToString("X2") + " " + data[2].ToString("X2") + " " + data[3].ToString("X2"));
        }
    }
}
