using Stride.Core.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace FluidGPU
{
	[StructLayout(LayoutKind.Sequential)]
	public struct SphericalImpulsePoint
	{
		Vector4 PosAndSize;
		Vector4 Value;

		public SphericalImpulsePoint(Vector4 posAndSize, Vector4 value)
		{
			PosAndSize = posAndSize;
			Value = value;
		}
	}
}