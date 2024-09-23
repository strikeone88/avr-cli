
using System;

namespace mcutils
{
	public class McuInfo
	{
		public string identifier;
		public string name;

		public int sramSize;
		public int flashSize;
		public int flashPageSize;

		public bool hasEeprom;
		public int eepromSize;
		public int eepromPageSize;

		public McuInfo()
		{
			this.hasEeprom = false;
		}
	}
}
