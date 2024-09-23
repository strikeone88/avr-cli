using System;

namespace mcutils
{
	public class AVRISP
	{
		public const int ERR_NONE					=	0;
		public const int ERR_PMODE_FAILED 			= 	1;
		public const int ERR_DEVICE_CODE_FAILED 	= 	2;
		public const int ERR_NOT_ATMEL_OR_LOCKED 	= 	3;
		public const int ERR_FAMILY_NOT_AVR 		= 	4;
		public const int ERR_COMMAND_ERROR			=	5;
		public const int ERR_BUSY					=	6;
		public const int ERR_PAGE_SETTINGS_NOT_SET	=	7;

		private static string[] errorMessages = new string[] {
			"None", 
			"Unable to enter programming mode", 
			"Device code could not be read",
			"Device was not manufactured by Atmel or is locked",
			"Not an AVR device",
			"Unable to execute command",
			"Device is busy",
			"Page settings not set"
		};

		/* ~ */
		protected SPI spi;
		protected int lastError;

		public int deviceManufacturer;
		public int deviceFamilyCode;
		public int devicePartNumber;
		public int deviceFlashMemorySize;
		public string deviceSignature;

 		public int pageSize;
		public int pageCount;
		public int[] buffer;

		public AVRISP(SPI spi)
		{
			this.spi = spi;
			this.buffer = null;
		}

		public SPI GetSPI ()
		{
			return this.spi;
		}

		protected void Dump ()
		{
			Console.Write(spi.data[0].ToString("X2") + " " + 
			              spi.data[1].ToString("X2") + " " + 
			              spi.data[2].ToString("X2") + " " +
			              spi.data[3].ToString("X2") + "\n");
		}

		protected int Error (int error)
		{
			return this.lastError = error;
		}

		public int GetError ()
		{
			return this.lastError;
		}

		public string GetErrorMessage (int error)
		{
			return errorMessages[error % errorMessages.Length];
		}

		public void Reset ()
		{
			spi.Reset();
			spi.DelayMicroseconds (2500);

			this.buffer = null;
		}

		public void Normal ()
		{
			spi.Normal();
		}

		protected bool Command (int c0, int c1, int c2, int c3)
		{
			spi.Command(c0, c1, c2, c3);

			if (spi.data[1] != c0 || spi.data[2] != c1)
			{
				spi.Command(c0, c1, c2, c3);

				if (spi.data[1] != c0 || spi.data[2] != c1)
					return false;
			}

			return true;
		}

		public int EnterProgrammingMode ()
		{
			this.Reset();

			for (int tries = 0; tries < 3; tries++)
			{
				spi.Command(0xAC, 0x53, 0x21, 0x00);

				if (spi.data[1] == 0xAC && spi.data[2] == 0x53 && spi.data[3] == 0x21)
				{
					return this.ReadSignature();
				}
			}

			return Error(ERR_PMODE_FAILED);
		}

		public int ReadSignature ()
		{
			if (!this.Command (0x30, 0x21, 0x00, 0x00))
				return Error(ERR_DEVICE_CODE_FAILED);

			deviceManufacturer = spi.data[3];

			if (deviceManufacturer != 0x1E)
				return Error(ERR_NOT_ATMEL_OR_LOCKED);

			/* ~ */
			if (!this.Command (0x30, 0x21, 0x01, 0x00))
				return Error(ERR_DEVICE_CODE_FAILED);

			deviceFamilyCode = spi.data[3];

			if ((deviceFamilyCode & 0xF0) != 0x90)
				return Error(ERR_FAMILY_NOT_AVR);

			deviceFlashMemorySize = (1 << (deviceFamilyCode & 0x0F)) << 10;

			/* ~ */
			if (!this.Command (0x30, 0x21, 0x02, 0x00))
				return Error(ERR_DEVICE_CODE_FAILED);

			devicePartNumber = spi.data[3];

			/* ~ */
			deviceSignature = deviceManufacturer.ToString("X2") + deviceFamilyCode.ToString("X2") + devicePartNumber.ToString("X2");
			return Error(ERR_NONE);
		}

		public int BSY()
		{
			if (!this.Command(0xF0, 0x00, 0x00, 0x00))
				return ERR_COMMAND_ERROR;

			return ((spi.data[3] & 1) != 0) ? ERR_BUSY : ERR_NONE;
		}

		public int Erase ()
		{
			if (!this.Command(0xAC, 0x80, 0x00, 0x00))
				return Error(ERR_COMMAND_ERROR);

			while (BSY() == ERR_BUSY)
			{
				spi.DelayMilliseconds(100);
			}

			return Error(ERR_NONE);
		}

		public void SetPageSettings (int pageSizeBytes, int pageCount)
		{
			this.pageSize = pageSizeBytes;
			this.pageCount = pageCount;

			this.buffer = new int[this.pageSize];
		}

		public void ClearBuffer ()
		{
			if (this.buffer == null)
				return;

			for (int i = 0; i < this.pageSize; i++)
				this.buffer[i] = 0;
		}

		public int WriteFlashPage (int page)
		{
			if (this.buffer == null)
				return Error(ERR_PAGE_SETTINGS_NOT_SET);

			int address = page * (pageSize >> 1);
			int src = address;

			if (spi.IsSerial())
				spi.SpiStart();

			for (int i = 0; i < pageSize; i+=2)
			{
				int ah = (address >> 8) & 255;
				int al = address & 255;

				if (spi.IsSerial())
				{
					spi.SpiEnsure(4);
					spi.SpiPush(0x40);
					spi.SpiPush(ah);
					spi.SpiPush(al);
					spi.SpiPush(buffer[i+0]);

					spi.SpiEnsure(4);
					spi.SpiPush(0x48);
					spi.SpiPush(ah);
					spi.SpiPush(al);
					spi.SpiPush(buffer[i+1]);
				}
				else
				{
					if (!this.Command(0x40, ah, al, buffer[i+0]))
						return Error(ERR_COMMAND_ERROR);

					if (!this.Command(0x48, ah, al, buffer[i+1]))
						return Error(ERR_COMMAND_ERROR);
				}

				address++;
			}

			spi.SpiFlush();

			if (!this.Command(0x4C, src >> 8, src & 255, 0x00))
				return Error(ERR_COMMAND_ERROR);

			while (BSY() == ERR_BUSY)
				spi.DelayMilliseconds(50);

			return Error(ERR_NONE);
		}

		public int ReadFlashPage (int page)
		{
			if (this.buffer == null)
				return Error(ERR_PAGE_SETTINGS_NOT_SET);

			int address = page * (pageSize >> 1);

			ClearBuffer();

			for (int i = 0; i < pageSize; i+=2)
			{
				int ah = (address >> 8) & 255;
				int al = address & 255;

				if (!this.Command(0x20, ah, al, 0x00))
					return Error(ERR_COMMAND_ERROR);

				buffer[i+0] = spi.data[3];

				if (!this.Command(0x28, ah, al, 0x00))
					return Error(ERR_COMMAND_ERROR);

				buffer[i+1] = spi.data[3];

				address++;
			}

			return Error(ERR_NONE);
		}
	}
}
