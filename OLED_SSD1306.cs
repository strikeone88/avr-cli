
using System;

namespace mcutils
{
    public class OLED_SSD1306 : I2C
    {
        public int width, height;
        public byte[] buffer;

        public OLED_SSD1306(IPortIO port, int SCL, int SDO, int addr) : base(port, SCL, SDO, addr)
        {
        }

        public void Init (int width, int height)
        {
            this.width = width;
            this.height = height;

            buffer = new byte[width*height >> 3];

            CMD(0xAE); // Display Off

            CMD(0xD5); // Display Clock Divider (3:0) and Oscillator Frequency (7:4)
            CMD(0xF0);

            CMD(0xA8); // Set Multiplex Ratio
            CMD(height-1);

            CMD(0xD3); // Set Display Offset
            CMD(0);

            CMD(0x40 | 0); // Set Display Start Line (0..63)

            CMD(0x8D); // Charge Pump Setting A2: 0=Disable, 1=Enable)
            CMD(0x10 | 4);

            CMD(0x20); // Memory Addresing Mode (0=Horz, 1=Vert, 2=Page Addr)
            CMD(0);

            CMD(0xA0 | 1); // Set Segment Re-Map (0=SEG0, 1=SEG127)
            CMD(0xC8); // Set COM Output Scan Direction (C0=COM[0] to COM[N], C8=COM[N-1] to COM[0]);

            CMD(0xDA); // Set COM Pins Hw Config (D4: 0=Seq, 1=Alt | D5: 0=Disable L/R Remap, 1=Enable L/R Remap)
            CMD(0x02 | 16);

            CMD(0x81); // Set Contrast (0..255)
            CMD(127);

            CMD(0xD9); // Set Pre-Charge Period
            CMD(0xF1);

            CMD(0xDB); // Set Vcomh Deselect Level (6..4: 0=0.65Vcc, 2=0.77Vcc, 3=0.83Vcc)
            CMD(0);

            CMD(0xA4); // Entire Display ON (A5=Off)
            CMD(0xA6); // Set Normal (A6) or Inverse (A7) Display Mode.
            CMD(0x2E); // Deactivate Scroll.

            CMD(0xAF); // Display On
        }

        public void Clear (int value)
        {
            if (value > 0)
                value = 0xFF;

            for (int i = 0; i < buffer.Length; i++)
                buffer[i] = (byte)value;
        }

        public void PutPixel (int x, int y, int value)
        {
            if (x < 0 || y < 0 || x >= width || y >= height)
                return;

            byte bit = (byte)(1 << (y & 7));

            if (value > 0)
                buffer[(y >> 3)*width + x] |= bit;
            else
                buffer[(y >> 3)*width + x] &= (byte)(~bit & 255);
        }

        public void Display()
        {
            CMD(0x21);
            CMD(0);
            CMD(width-1);
        
            CMD(0x22);
            CMD(0);

            switch (height)
            {
                case 64: CMD(7); break;
                case 32: CMD(3); break;
                case 16: CMD(1); break;
            }

            int i = 0;

            while (i < buffer.Length)
            {
                BEGIN();
                PUSH(0x40);

                for (int k = 0; k < 16; k++)
                    PUSH(buffer[i++]);

                END();
            }
        }
    }
}
