/*
 * (c) 2014 MOSA - The Managed Operating System Alliance
 *
 * Licensed under the terms of the New BSD License.
 *
 * Authors:
 *  Phil Garcia (tgiphil) <phil@thinkedge.com>
 */

using System.IO;
using System.Text;

namespace Mosa.TinyCPUSimulator.x86.Emulate
{
	/// <summary>
	/// Represents an special debug ports used by MOSA during simulations
	/// </summary>
	public class MosaDebug : BaseSimDevice
	{
		public const ushort StandardIOBase = 0xEA;

		protected readonly TextWriter debug;

		protected uint value32 = 0;
		protected uint bytes32 = 0;

		protected StringBuilder sb = new StringBuilder();

		/// <summary>
		/// Initializes a new instance of the <see cref="MosaDebug"/> class.
		/// </summary>
		/// <param name="simCPU">The sim cpu.</param>
		public MosaDebug(SimCPU simCPU)
			: base(simCPU)
		{
			debug = null;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="MosaDebug"/> class.
		/// </summary>
		/// <param name="simCPU">The sim cpu.</param>
		/// <param name="output">The output.</param>
		public MosaDebug(SimCPU simCPU, TextWriter output)
			: base(simCPU)
		{
			debug = output;
		}

		public override ushort[] GetPortList()
		{
			return GetPortList(StandardIOBase, 3);
		}

		public override void MemoryWrite(ulong address, byte size)
		{
			return;
		}

		protected void Output(string s)
		{
			if (debug != null)
			{
				debug.WriteLine(s);
			}
			else
			{
				System.Diagnostics.Debug.WriteLine(s);
			}
		}

		public override void PortWrite(uint port, byte value)
		{
			if (port == StandardIOBase)
			{
				Output("0x" + value.ToString("X2"));

				return;
			}

			if (port == StandardIOBase + 1)
			{
				value32 = (value32 << 8) | value;
				bytes32++;

				if (bytes32 == 4)
				{
					Output("0x" + value32.ToString("X8"));

					bytes32 = 0;
					value32 = 0;
				}

				return;
			}

			if (port == StandardIOBase + 2)
			{
				if (value == 0)
				{
					Output(sb.ToString());

					sb.Clear();

					return;
				}

				sb.Append((char)value);

				return;
			}
		}
	}
}