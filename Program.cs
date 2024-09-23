
using System;
using System.Collections;
using System.Runtime.InteropServices;
using System.IO.Ports;

namespace mcutils
{
	class Program
	{
		private static string version = "1.1";
		private static Config conf;

		private static int Hex2Int (string value)
		{
			return Convert.ToInt32(value, 16);
		}

		private static int Bin2Int (string value)
		{
			return Convert.ToInt32(value, 2);
		}

		private static int Dec2Int (string value)
		{
			return Convert.ToInt32(value);
		}

		private static IPortIO BuildPort (string value)
		{
			string[] vals = value.Split(',');

			switch (vals[0])
			{
				default:
					if (Hex2Int(vals[0]).ToString("X") == vals[0])
						return new ParallelPort (Hex2Int(vals[0]));

					Console.WriteLine("Error: Unsupported port specified: " + vals[0]);
					break;
			}

			return null;
		}

		private static SPI BuildSPI (string value, IPortIO port, ref string spiIdent)
		{
			string[] vals = value.Split(':');
			int sck, mosi, miso, reset;

			switch (vals[0])
			{
				case "rspar":
					if (port == null) port = new ParallelPort(0x378);

					spiIdent = "Serial Interface: Parallel port " + port.GetBasePort().ToString("X") + " 2:1:6:3 (RSPAR)";
					return new SPI (port, 2, 1, 6, 3);

				case "rsser":
					if (vals.Length != 2)
					{
						Console.WriteLine("Error: RSSER interface requires COM port number to be specified. (i.e. rsser:com4)");
						break;
					}

					string ports = vals[1];
					vals = vals[1].Split(',');

					for (int i = 0; i < vals.Length; i++)
					{
						SerialPort sp = new SerialPort(vals[i], 57600, Parity.None, 8, StopBits.One);
	
						sp.Handshake = Handshake.None;
						sp.ReadBufferSize = 1024;
						sp.WriteBufferSize = 1024;
	
						try {
							sp.Open();
						}
						catch (Exception e)
						{
							if (i == vals.Length-1)
							{
								Console.WriteLine("Error (rsser): Port(s) could not be opened: " + ports);
								return null;
							}
							
							continue;
						}

						SPI spi = new SPI(sp);
						if (!spi.testSerial())
						{
							sp.Close();
							sp.BaudRate = 115200;
	
							try {
								sp.Open();
							}
							catch (Exception e)
							{
								Console.WriteLine("Error (rsser:"+vals[i]+"): " + e.Message);
								break;
							}
	
							if (!spi.testSerial())
							{
								Console.WriteLine("Error (rsser:"+vals[i]+"): Seems like interface is not RSSER.");
								break;
							}
	
							sp.WriteLine("C6");
							spi.DelayMilliseconds(250);
	
							sp.Close();
							sp.BaudRate = 57600;
	
							try {
								sp.Open();
							}
							catch (Exception e)
							{
								Console.WriteLine("Error (rsser:"+vals[i]+"): " + e.Message);
								break;
							}
	
							if (!spi.testSerial())
							{
								Console.WriteLine("Error (rsser:"+vals[i]+"): Seems like interface is not RSSER.");
								break;
							}
						}
	
						spiIdent = "Serial Interface: Serial Port " + vals[i].ToUpper() + " @ "+sp.BaudRate+" (RSSER)";
						return spi;
					}

					break;

				default:
					if (port == null)
					{
						Console.WriteLine("Error: Serial interface requires a port (use --port).");
						return null;
					}

					if (vals.Length != 4)
					{
						Console.WriteLine("Error: SPI mapping requires SCK, MOSI, MISO and RESET bits.");
						break;
					}

					sck = Dec2Int(vals[1]);
					mosi = Dec2Int(vals[2]);
					miso = Dec2Int(vals[3]);
					reset = Dec2Int(vals[4]);

					spiIdent = "Serial Interface: Port " + port.GetBasePort().ToString("X") + " " + sck + ":" + mosi + ":" + miso + ":" + reset;

					return new SPI (port, sck, mosi, miso, reset);
			}

			return null;
		}

		private static AVRISP BuildISP (string value, IPortIO port, SPI spi, ref string spiIdent)
		{
			string[] valsb = value.Split('.');

			if (spi == null && valsb.Length == 2)
				spi = BuildSPI (valsb[1], port, ref spiIdent);

			switch (valsb[0])
			{
				case "avrisp":
					if (spi == null)
					{
						Console.WriteLine("Error: AVRISP programmer requires a serial interface (use --spi).");
						break;
					}

					spiIdent += "\nProgrammer: AVRISP with AVR910 Protocol";
					return new AVRISP(spi);

				default:
					Console.WriteLine("Error: Unsupported programmer specified: " + value);
					break;
			}

			return null;
		}

		private static void DumpMcu (McuInfo mcu)
		{
			Console.WriteLine(">> Found device " + mcu.name + " with " + mcu.flashSize + " KB of flash memory (SRAM="+mcu.sramSize+").");
		}

		private static void Upload(string[] args)
		{
			IPortIO port = null;
			SPI spi = null;
			AVRISP isp = null;
			byte[] data = null;
			string filename = null;
			bool verify = false;
			string spiIdent = null;

			string tmp;

			tmp = conf.findOption("port");
			if (tmp != null) port = BuildPort(tmp);

			tmp = conf.findOption("spi");
			if (tmp != null) spi = BuildSPI(tmp, port, ref spiIdent);

			tmp = conf.findOption("using");
			if (tmp != null) isp = BuildISP (tmp, port, spi, ref spiIdent);

			tmp = conf.findOption("verify");
			if (tmp != null) verify = tmp == "true";

			for (int i = 1; i < args.Length; i++)
			{
				if (!args[i].StartsWith("-"))
				{
					filename = args[i];
					continue;
				}

				string[] vals = args[i].Split('=');

				switch (vals[0])
				{
					case "--port":
						port = BuildPort (vals[1]);
						break;

					case "--spi":
						spi = BuildSPI (vals[1], port, ref spiIdent);
						break;

					case "--using":
						isp = BuildISP (vals[1], port, spi, ref spiIdent);
						break;

					case "-v": case "--verify":
						verify = true;
						break;
				}
			}

			if (isp == null)
			{
				Console.WriteLine("Error: In-system programmer not specified (use --using).");
				return;
			}

			if (filename == null)
			{
				Console.WriteLine("Error: Input hex file not specified.");
				return;
			}

			if (filename.EndsWith(".bin") && System.IO.File.Exists(filename))
			{
				data = System.IO.File.ReadAllBytes(filename);
			}

			if (data == null)
			{
				data = IntelHex.Load(filename);
				if (data == null)
				{
					Console.WriteLine("Error: Unable to open input file: " + filename);
					return;
				}
			}

			if (spiIdent != null) Console.WriteLine(spiIdent);

			// --------------------------------------------------

			if (isp.EnterProgrammingMode() != 0)
			{
				Console.WriteLine("Error: " + isp.GetErrorMessage(isp.GetError()) + ".");
				return;
			}

			McuInfo mcu = conf.findMcu(isp.deviceSignature);
			if (mcu == null)
			{
				Console.WriteLine("\nError: Unable to find configuration for device: " + isp.deviceSignature + ".");
				return;
			}

			int flashSize = isp.deviceFlashMemorySize >> 10;

			if (mcu.flashSize != flashSize)
			{
				Console.WriteLine("Warning: Device reported "+(flashSize)+" KB of flash, using this configuration.");
			}

			Console.WriteLine("");
			DumpMcu(mcu);

			Console.WriteLine(">> About to write "+data.Length+" bytes ("+(100.0*data.Length/(flashSize*1024)).ToString("n2")+"% of total memory).");
			Console.WriteLine("");

			if (data.Length > (flashSize*1024))
			{
				Console.WriteLine("Error: Program size exceeds flash size by " + (data.Length - flashSize*1024) + " bytes.");
				return;
			}

			int k = 0;
			int p = 0;

			isp.Erase();
			isp.SetPageSettings(mcu.flashPageSize*2, flashSize*512/mcu.flashPageSize);

			for (int i = 0; i < data.Length; i++)
			{
				isp.buffer[k++] = data[i];

				if (k == isp.pageSize)
				{
					Console.Write("\rWriting "+(100*i/data.Length).ToString()+"% ...");

					if (isp.WriteFlashPage(p) != 0)
					{
						Console.WriteLine("\nError: " + isp.GetErrorMessage(isp.GetError()) + ".");
						isp.Reset();
						return;
					}

					k = 0;
					p++;
				}
			}

			if (k != 0)
			{
				if (isp.WriteFlashPage(p) != 0)
				{
					Console.WriteLine("\nError: " + isp.GetErrorMessage(isp.GetError()) + ".");
					isp.Reset();
					return;
				}

				p++;
			}

			Console.Write("\rWriting "+"100"+"% ...");
			Console.WriteLine(" done.");

			if (verify)
			{
				int n = data.Length;
				int q = 0;

				for (k = 0; k < p && n > 0; k++)
				{
					Console.Write("\rVerifying "+(100*(k+1)/p).ToString()+"% ...");

					if (isp.ReadFlashPage(k) != 0)
					{
						Console.WriteLine("failed.\nError: Unable to read page "+k+" from flash memory.");
						isp.Reset();
						return;
					}

					for (int i = 0; i < isp.pageSize && n > 0; i++, n--)
					{
						if (isp.buffer[i] != data[q++])
						{
							Console.WriteLine("failed.\nError: Flash memory does not match written data! Verification failed.");
							isp.Reset();
							return;
						}
					}
				}

				Console.WriteLine(" correct.");
			}

			isp.Normal();
		}

		private static void Verify(string[] args)
		{
			IPortIO port = null;
			SPI spi = null;
			AVRISP isp = null;
			byte[] data = null;
			string filename = null;
			string spiIdent = null;

			string tmp;

			tmp = conf.findOption("port");
			if (tmp != null) port = BuildPort(tmp);

			tmp = conf.findOption("spi");
			if (tmp != null) spi = BuildSPI(tmp, port, ref spiIdent);

			tmp = conf.findOption("using");
			if (tmp != null) isp = BuildISP (tmp, port, spi, ref spiIdent);

			for (int i = 1; i < args.Length; i++)
			{
				if (!args[i].StartsWith("-"))
				{
					filename = args[i];
					continue;
				}

				string[] vals = args[i].Split('=');

				switch (vals[0])
				{
					case "--port":
						port = BuildPort (vals[1]);
						break;

					case "--spi":
						spi = BuildSPI (vals[1], port, ref spiIdent);
						break;

					case "--using":
						isp = BuildISP (vals[1], port, spi, ref spiIdent);
						break;
				}
			}

			if (isp == null)
			{
				Console.WriteLine("Error: In-system programmer not specified (use --using).");
				return;
			}

			if (filename == null)
			{
				Console.WriteLine("Error: Input hex file not specified.");
				return;
			}

			if (filename.EndsWith(".bin") && System.IO.File.Exists(filename))
			{
				data = System.IO.File.ReadAllBytes(filename);
			}

			if (data == null)
			{
				data = IntelHex.Load(filename);
				if (data == null)
				{
					Console.WriteLine("Error: Unable to open input file: " + filename);
					return;
				}
			}

			data = IntelHex.Load(filename);
			if (data == null)
			{
				Console.WriteLine("Error: Unable to open input file: " + filename);
				return;
			}

			if (spiIdent != null) Console.WriteLine(spiIdent);

			// --------------------------------------------------

			if (isp.EnterProgrammingMode() != 0)
			{
				Console.WriteLine("Error: " + isp.GetErrorMessage(isp.GetError()) + ".");
				return;
			}

			McuInfo mcu = conf.findMcu(isp.deviceSignature);
			if (mcu == null)
			{
				Console.WriteLine("\nError: Unable to find configuration for device: " + isp.deviceSignature + ".");
				return;
			}

			int flashSize = isp.deviceFlashMemorySize >> 10;

			if (mcu.flashSize != flashSize)
			{
				Console.WriteLine("Warning: Device reported "+(flashSize)+" KB of flash, using this configuration.");
			}

			Console.WriteLine("");
			DumpMcu(mcu);

			Console.WriteLine("");

			isp.SetPageSettings(mcu.flashPageSize*2, flashSize*512/mcu.flashPageSize);

			int n = data.Length;

			if (n > (flashSize*1024))
			{
				Console.WriteLine("Warning: Program size exceeds flash size by " + (data.Length - flashSize*1024) + " bytes. Truncating.");
				n = (flashSize*1024);
			}

			int q = 0;
			int p = (n + isp.pageSize - 1) / isp.pageCount;

			for (int k = 0; n > 0; k++)
			{
				Console.Write("\rVerifying "+(100*(k+1)/p).ToString()+"% ...");

				if (isp.ReadFlashPage(k) != 0)
				{
					Console.WriteLine("failed.\nError: Unable to read page "+k+" from flash memory.");
					isp.Reset();
					return;
				}

				for (int i = 0; i < isp.pageSize && n > 0; i++, n--)
				{
					if (isp.buffer[i] != data[q++])
					{
						Console.WriteLine("failed.\nError: Flash memory does not match source file! Verification failed.");
						isp.Reset();
						return;
					}
				}
			}

			Console.WriteLine(" correct.");
		}

		private static void Download(string[] args)
		{
			IPortIO port = null;
			SPI spi = null;
			AVRISP isp = null;
			byte[] data = null;
			string filename = null;
			string spiIdent = null;

			string tmp;

			tmp = conf.findOption("port");
			if (tmp != null) port = BuildPort(tmp);

			tmp = conf.findOption("spi");
			if (tmp != null) spi = BuildSPI(tmp, port, ref spiIdent);

			tmp = conf.findOption("using");
			if (tmp != null) isp = BuildISP (tmp, port, spi, ref spiIdent);

			for (int i = 1; i < args.Length; i++)
			{
				if (!args[i].StartsWith("-"))
				{
					filename = args[i];
					continue;
				}

				string[] vals = args[i].Split('=');

				switch (vals[0])
				{
					case "--port":
						port = BuildPort (vals[1]);
						break;

					case "--spi":
						spi = BuildSPI (vals[1], port, ref spiIdent);
						break;

					case "--using":
						isp = BuildISP (vals[1], port, spi, ref spiIdent);
						break;
				}
			}

			if (isp == null)
			{
				Console.WriteLine("Error: In-system programmer not specified (use --using).");
				return;
			}

			if (filename == null)
			{
				Console.WriteLine("Error: Output hex/bin file not specified.");
				return;
			}

			if (spiIdent != null) Console.WriteLine(spiIdent);

			// --------------------------------------------------
			if (isp.EnterProgrammingMode() != 0)
			{
				Console.WriteLine("Error: " + isp.GetErrorMessage(isp.GetError()) + ".");
				return;
			}

			McuInfo mcu = conf.findMcu(isp.deviceSignature);
			if (mcu == null)
			{
				Console.WriteLine("\nError: Unable to find configuration for device: " + isp.deviceSignature + ".");
				return;
			}

			int flashSize = isp.deviceFlashMemorySize >> 10;

			if (mcu.flashSize != flashSize)
			{
				Console.WriteLine("Warning: Device reported "+(flashSize)+" KB of flash, using this configuration.");
			}

			Console.WriteLine("");
			DumpMcu(mcu);

			Console.WriteLine("");

			isp.SetPageSettings(mcu.flashPageSize*2, flashSize*512/mcu.flashPageSize);

			data = new byte[flashSize*1024];

			int n = data.Length;
			int q = 0;

			for (int k = 0; n > 0; k++)
			{
				if (isp.ReadFlashPage(k) != 0)
				{
					Console.WriteLine("\nError: Unable to read page "+k+" from flash memory.");
					isp.Reset();
					return;
				}

				for (int i = 0; i < isp.pageSize && n > 0; i++, n--)
				{
					data[q++] = (byte)isp.buffer[i];
				}

				Console.Write("\rReading "+(100*q/data.Length).ToString()+"% ...");
			}

			Console.WriteLine(" done.");

			if (filename.EndsWith(".bin"))
				System.IO.File.WriteAllBytes(filename, data);
			else
				IntelHex.Save(filename, data, 16);
		}

		private static void Reset(string[] args)
		{
			IPortIO port = null;
			SPI spi = null;
			AVRISP isp = null;
			string filename = null;
			string spiIdent = null;

			string tmp;

			tmp = conf.findOption("port");
			if (tmp != null) port = BuildPort(tmp);

			tmp = conf.findOption("spi");
			if (tmp != null) spi = BuildSPI(tmp, port, ref spiIdent);

			tmp = conf.findOption("using");
			if (tmp != null && spi == null)
			{
				isp = BuildISP (tmp, port, spi, ref spiIdent);
				spi = isp.GetSPI();
			}

			for (int i = 1; i < args.Length; i++)
			{
				if (!args[i].StartsWith("-"))
				{
					filename = args[i];
					continue;
				}

				string[] vals = args[i].Split('=');

				switch (vals[0])
				{
					case "--port":
						port = BuildPort (vals[1]);
						break;

					case "--spi":
						spi = BuildSPI (vals[1], port, ref spiIdent);
						break;
				}
			}

			if (spi == null)
			{
				Console.WriteLine("Error: Serial interface not specified (use --spi).");
				return;
			}

			if (spiIdent != null) Console.WriteLine(spiIdent+"\n");

			spi.Normal();

			Console.WriteLine(">> Device reset.");
		}

		private static void Stop(string[] args)
		{
			IPortIO port = null;
			SPI spi = null;
			AVRISP isp = null;
			string filename = null;
			string spiIdent = null;

			string tmp;

			tmp = conf.findOption("port");
			if (tmp != null) port = BuildPort(tmp);

			tmp = conf.findOption("spi");
			if (tmp != null) spi = BuildSPI(tmp, port, ref spiIdent);

			tmp = conf.findOption("using");
			if (tmp != null && spi == null)
			{
				isp = BuildISP (tmp, port, spi, ref spiIdent);
				spi = isp.GetSPI();
			}

			for (int i = 1; i < args.Length; i++)
			{
				if (!args[i].StartsWith("-"))
				{
					filename = args[i];
					continue;
				}

				string[] vals = args[i].Split('=');

				switch (vals[0])
				{
					case "--port":
						port = BuildPort (vals[1]);
						break;

					case "--spi":
						spi = BuildSPI (vals[1], port, ref spiIdent);
						break;
				}
			}

			if (spi == null)
			{
				Console.WriteLine("Error: Serial interface not specified (use --spi).");
				return;
			}

			if (spiIdent != null) Console.WriteLine(spiIdent+"\n");

			spi.Reset();

			Console.WriteLine(">> Device stopped (left in low-RESET signal).");
		}

		private static void Identify(string[] args)
		{
			IPortIO port = null;
			SPI spi = null;
			AVRISP isp = null;
			string spiIdent = null;

			string tmp;

			tmp = conf.findOption("port");
			if (tmp != null) port = BuildPort(tmp);

			tmp = conf.findOption("spi");
			if (tmp != null) spi = BuildSPI(tmp, port, ref spiIdent);

			tmp = conf.findOption("using");
			if (tmp != null) isp = BuildISP (tmp, port, spi, ref spiIdent);

			for (int i = 1; i < args.Length; i++)
			{
				string[] vals = args[i].Split('=');

				switch (vals[0])
				{
					case "--port":
						port = BuildPort (vals[1]);
						break;

					case "--spi":
						spi = BuildSPI (vals[1], port, ref spiIdent);
						break;

					case "--using":
						isp = BuildISP (vals[1], port, spi, ref spiIdent);
						break;
				}
			}

			if (isp == null)
			{
				Console.WriteLine("Error: In-system programmer not specified (use --using).");
				return;
			}

			if (spiIdent != null) Console.WriteLine(spiIdent);

			// --------------------------------------------------
			if (isp.EnterProgrammingMode() != 0)
			{
				Console.WriteLine("Error: " + isp.GetErrorMessage(isp.GetError()) + ".");
				return;
			}

			McuInfo mcu = conf.findMcu(isp.deviceSignature);
			if (mcu == null)
			{
				Console.WriteLine("\nError: Unable to find configuration for device: " + isp.deviceSignature + ".");
				return;
			}

			Console.WriteLine("");
			DumpMcu(mcu);
		}

		private static void Command(string[] args)
		{
			IPortIO port = null;
			SPI spi = null;
			AVRISP isp = null;
			ArrayList cmd = new ArrayList ();
			string spiIdent = null;

			string tmp;

			tmp = conf.findOption("port");
			if (tmp != null) port = BuildPort(tmp);

			tmp = conf.findOption("spi");
			if (tmp != null) spi = BuildSPI(tmp, port, ref spiIdent);

			tmp = conf.findOption("using");
			if (tmp != null) isp = BuildISP (tmp, port, spi, ref spiIdent);

			for (int i = 1; i < args.Length; i++)
			{
				string[] vals = args[i].Split('=');

				if (!vals[0].StartsWith("-"))
				{
					cmd.Add(vals[0]);
					continue;
				}

				switch (vals[0])
				{
					case "--port":
						port = BuildPort (vals[1]);
						break;

					case "--spi":
						spi = BuildSPI (vals[1], port, ref spiIdent);
						break;

					case "--using":
						isp = BuildISP (vals[1], port, spi, ref spiIdent);
						break;
				}
			}

			if (isp == null)
			{
				Console.WriteLine("Error: In-system programmer not specified (use --using).");
				return;
			}

			if (cmd == null)
			{
				Console.WriteLine("Error: Command not specified (hex string).");
				return;
			}

			if (spiIdent != null) Console.WriteLine(spiIdent);

			// --------------------------------------------------
			if (isp.EnterProgrammingMode() != 0)
			{
				Console.WriteLine("Error: " + isp.GetErrorMessage(isp.GetError()) + ".");
				return;
			}

			spi = isp.GetSPI();

			McuInfo mcu = conf.findMcu(isp.deviceSignature);

			Console.WriteLine("");
			if (mcu != null)
				DumpMcu(mcu);
			else
				Console.WriteLine(">> Found device " + (isp.deviceSignature) + " with " + (isp.deviceFlashMemorySize >> 10) + " KB of flash memory.");

			Console.WriteLine("");

			for (int i = 0; i < cmd.Count; i++)
			{
				string value = (string)cmd[i];

				for (int j = 0; j < value.Length; j += 2)
				{
					Console.Write(spi.WriteByte(IntelHex.ByteValue(value, j)).ToString("X2") + " ");
				}

				spi.DelayMilliseconds(50);
				Console.WriteLine("");
			}

			isp.Normal();
		}

		public static void Main(string[] args)
		{
/* 
if (false)
{
	IPortIO port = new ParallelPort (0x378);
	OLED_SSD1306 oled = new OLED_SSD1306 (port, 2, 1, 0x3C);

	oled.Init(128, 64);

	int cx = oled.width >> 1;
	int cy = oled.height >> 1;

	System.Drawing.Bitmap ii = new System.Drawing.Bitmap(System.Drawing.Bitmap.FromFile(@"C:\Documents and Settings\Master\Escritorio\Untitled.png"));

	for (int y = 0; y < ii.Height; y++)
	{
		for (int x = 0; x < ii.Width; x++)
		{
			System.Drawing.Color cc = ii.GetPixel(x,y);

			if (cc.GetBrightness() > 0)
				oled.PutPixel(x, y, 1);
			else
				oled.PutPixel(x, y, 0);
		}
	}

	oled.Display();

	return;
}

if (false)
{
	IPortIO port = new ParallelPort (0x378);
	I2C gyro = new I2C (port, 2, 1, 0x00):
	RegisterBasedSPI spi = new RegisterBasedSPI (port, 2, 1, 6, 3);

	int s;
	
	spi.Reg8(0x20, 0x0F);
	
	for (int i = 0; i < 3; i++)
	{
		Console.WriteLine (spi.Reg8(0x20).ToString("X"));
	}
	// 1100 0110
	// 0110 0011
	
	if (spi.Reg8(L3GD20.WHO_AM_I) != 0xD3)
	{
		Console.WriteLine("Device is not a L3GD20.\n");
		Console.ReadKey(true);
		return;
	}

	Console.WriteLine("\n");
	Console.ReadKey(true);
	return;
	
	spi.Reg8(L3GD20.CTRL_REG1, 0x0F);
	spi.Reg8(L3GD20.CTRL_REG4, 0x00);
	
	Console.WriteLine ("CTRL_REG1 = " + spi.Reg8(L3GD20.CTRL_REG1).ToString("X2"));
	Console.WriteLine ("CTRL_REG4 = " + spi.Reg8(L3GD20.CTRL_REG4).ToString("X2"));
	Console.WriteLine ("STATUS_REG = " + spi.Reg8(L3GD20.STATUS_REG).ToString("X2"));
	
	Console.WriteLine ("");
	
	System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch ();
	
	float G_GAIN = (float)(70.0f / 1000);
	float Xa = 0, Ya = 0, Za = 0;
	
	float dt = 0.01f;
	int dt_ms = (int)(dt*1000);
	
	while (Console.KeyAvailable == false)
	{
		sw.Start();
	
		float x = spi.Reg16(L3GD20.OUT_X_L)*G_GAIN;
		float y = spi.Reg16(L3GD20.OUT_Y_L)*G_GAIN;
		float z = spi.Reg16(L3GD20.OUT_Z_L)*G_GAIN;
	
		Xa += x*dt;
		Ya += y*dt;
		Za += z*dt;
	
		Console.Write(String.Format("X={0:0.00}, Y={1:0.00}, Z={2:0.00}                    \r", Xa, Ya, Za));
	
		while (sw.ElapsedMilliseconds < dt_ms);
	
		sw.Reset();
	}
	
	spi.Reg8(L3GD20.CTRL_REG1, 0);
	return;
}
*/
			if (args.Length == 1 && (args[0] == "-v" || args[0] == "--version"))
			{
				Console.WriteLine(version);
				return;
			}

			Console.WriteLine("RedStar Microcontroller Utilities "+version+" Copyright (C) 2015-2018 RedStar Technologies");

			conf = new Config();

			if (!conf.isReady())
			{
				Console.WriteLine("Error: Missing configuration file msutils.conf\n");
				return;
			}

			if (args.Length == 0)
			{
				Console.WriteLine(
					"Syntax: mcutils [command] [options]\n\n"+
					"Commands:\n"+
					"upload [--port=aaa] [--spi=bbb] --using=ccc <hexfile>\n"+
					"verify [--port=aaa] [--spi=bbb] --using=ccc <hexfile>\n"
				);
				return;
			}

			Console.WriteLine("");

			switch (args[0])
			{
				case "upload":
					Upload(args);
					break;

				case "verify":
					Verify(args);
					break;

				case "download":
					Download(args);
					break;

				case "reset":
					Reset(args);
					break;

				case "stop":
					Stop(args);
					break;

				case "identify":
					Identify(args);
					break;

				case "cmd":
					Command(args);
					break;

				default:
					Console.WriteLine("Error: Unknown command specified: " + args[0]);
					break;
			}

			Console.WriteLine("");
		}
	}
}