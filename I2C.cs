
using System;

namespace mcutils
{
    /// <summary>
    /// Description of OLED_SSD1306.
    /// </summary>
    public class I2C
    {
        public SPI spi;
        public int addr;

        private int[] buffer;
        private int top;

        public I2C(IPortIO port, int SCL, int SDO, int addr)
        {
            spi = new SPI (port, SCL, SDO);
            ADDR(addr);

            buffer = new int[4096];
            INIT();
        }

        public void ADDR (int value)
        {
            this.addr = value;
        }

        public void SDA (int value)
        {
            spi.MOSI (value);
        }
        
        public void SCL (int value)
        {
            spi.SCK (value);
        }

        public void BITDELAY()
        {
            //spi.DelayMicroseconds(1);
        }
        
        public void DELAY(int micros)
        {
            spi.DelayMicroseconds(micros);
        }

        public void INIT()
        {
            SDA(1); SCL(1); BITDELAY();
        }

        public void START ()
        {
            SDA(1); SCL(1); BITDELAY();
            SDA(0); BITDELAY();
            SCL(0); BITDELAY();
        }

        public void STOP ()
        {
            SCL(1); BITDELAY();
            SDA(1); BITDELAY();
        }

        public void BIT (int value)
        {
            SDA(value); SCL(1); BITDELAY();
            SCL(0); BITDELAY();

            if (value != 0) SDA(0);
            BITDELAY();
        }

        public void BYTE (int value)
        {
            for (int i = 0; i < 8; i++)
            {
                BIT(value & 0x80);
                value <<= 1;
            }

            BIT(0);
        }

        public void CMD (int value)
        {
            START();
            BYTE((addr << 1) | 0);
            BYTE(0);
            BYTE(value);
            STOP();
        }

        public void CMD (int control, int value)
        {
            START();
            BYTE((addr << 1) | 0);
            BYTE(control);
            BYTE(value);
            STOP();
        }

        public void BEGIN()
        {
            top = 0;
            PUSH((addr << 1) | 0);
        }

        public void PUSH(int value)
        {
            buffer[top++] = value;
        }

        public void END()
        {
            START();
            
            for (int i = 0; i < top; i++)
                BYTE(buffer[i]);

            STOP();
        }
    }
}
