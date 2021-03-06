/*
 * (c) 2008 MOSA - The Managed Operating System Alliance
 *
 * Licensed under the terms of the New BSD License.
 *
 * Authors:
 *  Phil Garcia (tgiphil) <phil@thinkedge.com>
 */

using Mosa.DeviceSystem;
using Mosa.Kernel.x86;
using Mosa.Platform.Internal.x86;

namespace Mosa.CoolWorld.x86.HAL
{
	/// <summary>
	///
	/// </summary>
	public class HardwareAbstraction : IHardwareAbstraction
	{
		/// <summary>
		/// Requests an IO read/write port interface from the kernel
		/// </summary>
		/// <param name="port">The port number.</param>
		/// <returns></returns>
		IReadWriteIOPort IHardwareAbstraction.RequestIOPort(ushort port)
		{
			return new IOPort(port);
		}

		/// <summary>
		/// Requests a block of memory from the kernel
		/// </summary>
		/// <param name="address">The address.</param>
		/// <param name="size">The size.</param>
		/// <returns></returns>
		IMemory IHardwareAbstraction.RequestPhysicalMemory(uint address, uint size)
		{
			// Map physical memory space to virtual memory space

			Boot.Console.WriteLine("");
			Boot.Console.WriteLine(":" + address.ToString("X"));
			Boot.Console.WriteLine(":" + size.ToString("X"));

			//address = address & 0xFFFFF000;	// force alignment
			uint end = address + size;
			for (uint at = address; at < end; address = address + 4096)
			{
				Boot.Console.WriteLine(at.ToString("X"));
				PageTable.MapVirtualAddressToPhysical(address, address);
			}
			Boot.Console.WriteLine("Y");
			return new Memory(address, size);
		}

		/// <summary>
		/// Disables all interrupts.
		/// </summary>
		void IHardwareAbstraction.DisableAllInterrupts()
		{
			Native.Cli();
		}

		/// <summary>
		/// Enables all interrupts.
		/// </summary>
		void IHardwareAbstraction.EnableAllInterrupts()
		{
			Native.Sti();
		}

		/// <summary>
		/// Processes the interrupt.
		/// </summary>
		/// <param name="irq">The irq.</param>
		void IHardwareAbstraction.ProcessInterrupt(byte irq)
		{
			Mosa.DeviceSystem.HAL.ProcessInterrupt(irq);
		}

		/// <summary>
		/// Sleeps the specified milliseconds.
		/// </summary>
		/// <param name="milliseconds">The milliseconds.</param>
		void IHardwareAbstraction.Sleep(uint milliseconds)
		{
		}

		/// <summary>
		/// Allocates the memory.
		/// </summary>
		/// <param name="size">The size.</param>
		/// <param name="alignment">The alignment.</param>
		/// <returns></returns>
		IMemory IHardwareAbstraction.AllocateMemory(uint size, uint alignment)
		{
			uint address = KernelMemory.AllocateMemory(size);

			return new Memory(address, size);
		}

		/// <summary>
		/// Gets the physical address.
		/// </summary>
		/// <param name="memory">The memory.</param>
		/// <returns></returns>
		uint IHardwareAbstraction.GetPhysicalAddress(IMemory memory)
		{
			return Mosa.Kernel.x86.PageTable.GetPhysicalAddressFromVirtual(memory.Address);
		}

		/// <summary>
		/// Debugs the write.
		/// </summary>
		/// <param name="msg">The message.</param>
		void IHardwareAbstraction.DebugWrite(string message)
		{
			Mosa.CoolWorld.x86.Boot.Console.Write(message);
		}

		/// <summary>
		/// Debugs the write line.
		/// </summary>
		/// <param name="msg">The message.</param>
		void IHardwareAbstraction.DebugWriteLine(string message)
		{
			Mosa.CoolWorld.x86.Boot.Console.WriteLine(message);
		}
	}
}
