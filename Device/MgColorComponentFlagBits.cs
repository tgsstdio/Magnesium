using System;

namespace Magnesium
{
	[Flags]
	public enum MgColorComponentFlagBits : byte
	{
		R_BIT = 0x00000001,
		G_BIT = 0x00000002,
		B_BIT = 0x00000004,
		A_BIT = 0x00000008,
	}

}

