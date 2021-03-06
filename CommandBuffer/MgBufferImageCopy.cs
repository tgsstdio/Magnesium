﻿using System;
using System.Runtime.InteropServices;

namespace Magnesium
{
	[StructLayout(LayoutKind.Sequential)]
    public struct MgBufferImageCopy
	{
		public UInt64 BufferOffset { get; set; }
		public UInt32 BufferRowLength { get; set; }
		public UInt32 BufferImageHeight { get; set; }
		public MgImageSubresourceLayers ImageSubresource { get; set; }
		public MgOffset3D ImageOffset { get; set; }
		public MgExtent3D ImageExtent { get; set; }
	}
}

