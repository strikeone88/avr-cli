
using System;

namespace mcutils
{
	public class RegisterBasedSPI : SPI
	{
		public RegisterBasedSPI (IPortIO port, int SCK, int MOSI, int MISO, int RESET) : base(port, SCK, MOSI, MISO, RESET)
		{
			// RESET: Active High
			// SCK: Active Low

			Configure(false, false, true, 1, 0);
			this.bitDelay = 10;
		}

		public int Reg8 (int index)
		{
			RESET(0);
			WriteByte (0x80 | (index & 0x3F));
			int value = WriteByte (0);
			RESET(1);
			return value;
		}

		public void Reg8 (int index, int value)
		{
			RESET(0);
			WriteByte (index & 0x3F);
			WriteByte (value);
			RESET(1);
		}

		public int Reg16 (int index)
		{
			RESET(0);
			WriteByte (0xC0 | (index & 0x3F));

			int value = WriteByte (0);
			value |= (WriteByte (0) << 8);

			RESET(1);
			return ((value & 0x8000) != 0) ? (value - 0x10000) : (value);
		}

		public int Reg16u (int index)
		{
			RESET(0);
			WriteByte (0xC0 | (index & 0x3F));

			int value = WriteByte (0);
			value |= (WriteByte (0) << 8);

			RESET(1);
			return value;
		}

		public void Reg16 (int index, int value)
		{
			RESET(0);
			WriteByte (0x40 | (index & 0x3F));

			WriteByte (value & 0xFF);
			WriteByte (value >> 8);
			RESET(1);
		}
	}
}
