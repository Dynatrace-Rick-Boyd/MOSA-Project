﻿/*
 * (c) 2013 MOSA - The Managed Operating System Alliance
 *
 * Licensed under the terms of the New BSD License.
 *
 * Authors:
 *  Phil Garcia (tgiphil) <phil@thinkedge.com>
 */

namespace Mosa.TinyCPUSimulator.x86.Opcodes
{
	public class Out : BaseX86Opcode
	{
		public override void Execute(CPUx86 cpu, SimInstruction instruction)
		{
			uint a = LoadValue(cpu, instruction.Operand1);
			uint b = LoadValue(cpu, instruction.Operand2);

			int size = instruction.Size != 0 ? instruction.Size : instruction.Operand2.Size;

			if (size == 8)
				cpu.Write8Port(a, (byte)b);
			else if (size == 16)
				cpu.Write16Port(a, (ushort)b);
			else if (size == 32)
				cpu.Write32Port(a, b);
		}
	}
}