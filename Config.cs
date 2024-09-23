
using System;
using System.Collections.Generic;

namespace mcutils
{
	public class Config
	{
		private bool ready;

		private List<McuInfo> devices;
		private List<Option> options;

		private static void trimAll(string[] list)
		{
			for (int i = 0; i < list.Length; i++)
				list[i] = list[i].Trim();
		}

		public Config()
		{
			ready = false;

			devices = new List<McuInfo> ();
			options = new List<Option> ();

			McuInfo device = null;
			Option option = null;

			if (!System.IO.File.Exists(AppDomain.CurrentDomain.BaseDirectory + "mcutils.conf"))
				return;

			string[] lines = System.IO.File.ReadAllLines(AppDomain.CurrentDomain.BaseDirectory + "mcutils.conf");
			string[] args;
			string s0;

			int state = 0;
			int j;

			for (int i = 0; i < lines.Length; i++)
			{
				lines[i] = lines[i].Trim();

				if (lines[i].StartsWith("#") || lines[i].Length == 0)
					continue;

				if (lines[i] == "[:Devices]")
				{
					ready = true;
					state = 1;
					continue;
				}

				if (lines[i] == "[:Options]")
				{
					state = 3;
					continue;
				}

			Retry:
				switch(state)
				{
					case 1:
						if (lines[i].StartsWith("[") && lines[i].EndsWith("]"))
						{
							devices.Add(device = new McuInfo());

							device.identifier = lines[i].Substring(1, lines[i].Length-2);
							state = 2;
						}

						break;

					case 2:
						if (lines[i].StartsWith("[") && lines[i].EndsWith("]"))
						{
							state = 1;
							goto Retry;
						}

						j = lines[i].IndexOf('=');
						if (j == -1) break;

						s0 = lines[i].Substring(0, j);

						args = lines[i].Substring(j+1).Split(' ');
						trimAll(args);

						switch (s0)
						{
							case "name":
								device.name = args[0];
								break;

							case "sram":
								device.sramSize = int.Parse(args[0]);
								break;

							case "flash":
								device.flashSize = int.Parse(args[0]);
								device.flashPageSize = int.Parse(args[1]);
								break;

							case "eeprom":
								device.eepromSize = int.Parse(args[0]);
								device.eepromPageSize = int.Parse(args[1]);
								break;
						}

						break;

					case 3:
						if (lines[i].StartsWith("["))
							break;

						args = lines[i].Split('=');
						trimAll(args);

						options.Add(option = new Option());
						option.name = args[0];
						option.value = args[1];
						break;
				}
			}
		}

		public bool isReady()
		{
			return this.ready;
		}

		public McuInfo findMcu (string identifier)
		{
			for (int i = 0; i < devices.Count; i++)
			{
				if (devices[i].identifier == identifier)
					return devices[i];
			}

			return null;
		}
		
		public string findOption (string name)
		{
			for (int i = 0; i < options.Count; i++)
			{
				if (options[i].name == name)
					return options[i].value;
			}

			return null;
		}
	}
}
