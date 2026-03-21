using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Net;
using System.Diagnostics;
using System;
using System.IO;

namespace GameboyEmu.Core
{
    public class MMU(GameBoy gb)
    {
        public byte[] Memory { get; set; } = new byte[0x10000];
        public byte[] Cartridge { get; set; } = new byte[0x200000];


        private bool ROMBank1 = false;
        private bool ROMBank2 = false;
        public byte CurrentROMBank = 1;
        private bool MemoryMode16 = true;
        private byte CurrentRAMBank = 0;
        private readonly byte[] RAMBanks = new byte[0x8000];
        private bool EnableRAM = false;
        private readonly GameBoy gameboy = gb;

        /// <summary>APU instance – set by GameBoy after construction.</summary>
        public APU Apu { get; set; } = null!;

        public byte IF { get { return Memory[0xFF0F]; } set { Memory[0xFF0F] = value; } }// 0xFF0F - Interrupt Flag (R/W)

        public byte IE { get { return Memory[0xFFFF]; } set { Memory[0xFFFF] = value; } } // 0xFFFF IE - Interrupt Enable (R/W)       

        public void WriteByteToMemory(uint addr, byte value)
        {
            //memory[addr] = value; return;  // Required for JSON Tests

            addr &= 0xFFFF;

            if (addr <= 0x1FFF)
            {
                if (ROMBank1)
                {
                    if ((value & 0xF) == 0xA)
                        EnableRAM = true;
                    else if (value == 0x0)
                        EnableRAM = false;
                }
                else if (ROMBank2)
                {
                    if (false == gameboy.TestBit((byte)(addr>>8), 0)) //bit 0 of upper byte must be 0
                    {
                        if ((value & 0xF) == 0xA)
                            EnableRAM = true;
                        else if (value == 0x0)
                            EnableRAM = false;
                    }
                }
            }

            else if ((addr >= 0x2000) && (addr <= 0x3FFF))
            {
                if (ROMBank1)
                {
                    if (value == 0x00)
                        value++;

                    value &= 31;

                    // Turn off the lower 5-bits.
                    CurrentROMBank &= 224;

                    // Combine the written value with the register.
                    CurrentROMBank |= value;

                    //Debug.WriteLine( "Changing Rom Bank to %d", CurrentROMBank);

                }
                else if (ROMBank2)
                {
                    value &= 0xF;
                    CurrentROMBank = value;
                }
            }


            // writing to address 0x4000 to 0x5FFF switches ram banks (if enabled of course)
            else if ((addr >= 0x4000) && (addr <= 0x5FFF))
            {
                if (ROMBank1)
                {
                    // are we using memory model 16/8
                    if (MemoryMode16)
                    {
                        // in this mode we can only use Ram Bank 0
                        CurrentRAMBank = 0;

                        value &= 3;
                        value <<= 5;

                        if ((CurrentROMBank & 31) == 0)
                        {
                            value++;
                        }

                        // Turn off bits 5 and 6, and 7 if it somehow got turned on.
                        CurrentROMBank &= 31;

                        // Combine the written value with the register.
                        CurrentROMBank |= value;

                        //Debug.WriteLine("Changing Rom Bank to %d", CurrentROMBank);

                    }
                    else
                    {
                        CurrentRAMBank = (byte)(value & 0x3);
                        //Debug.WriteLine("Changing Rom Bank to %d", CurrentROMBank);

                    }
                }
            }

            // writing to addr 0x6000 to 0x7FFF switches memory model
            else if ((addr >= 0x6000) && (addr <= 0x7FFF))
            {
                if (ROMBank1)
                {
                    // we're only interested in the first bit
                    value &= 1;
                    if (value == 1)
                    {
                        CurrentRAMBank = 0;
                        MemoryMode16 = false;
                    }
                    else
                        MemoryMode16 = true;
                }
            }

            else if ((addr >= 0xA000) && (addr < 0xC000))
            {
                if (EnableRAM)
                {
                    if (ROMBank1)
                    {
                        uint newAddress = addr - 0xA000;
                        RAMBanks[newAddress + (CurrentRAMBank * 0x2000)] = value;
                    }
                }
                else if (ROMBank2 && (addr < 0xA200))
                {
                    uint newAddress = addr - 0xA000;
                    RAMBanks[newAddress + (CurrentRAMBank * 0x2000)] = value;
                }
            }

            else if ((addr >= 0xC000) && (addr <= 0xDFFF))
            {
                Memory[addr] = value;
            }

            else if (addr >= 0xE000 && addr < 0xFE00)
            {
                Memory[addr] = value;
                Memory[addr - 0x2000] = value;
            }

            else if (addr >= 0xFEA0 && addr < 0xFEFF && value > 0)
            { }

            else if (0xFF04 == addr)
            {
                Memory[0xFF04] = 0;
                gameboy.DivCounter = 0;
            }

            else if (addr == 0xFF07)
            {
                Memory[addr] = value;

                int timerVal = value & 0x03;

                int clockSpeed = 0;

                switch (timerVal)
                {
                    case 0: clockSpeed = 1024; break;
                    case 1: clockSpeed = 16; break;
                    case 2: clockSpeed = 64; break;
                    case 3: clockSpeed = 256; break; // 256
                }

                if (clockSpeed != gameboy!.TimerCounter)
                {
                    gameboy!.TimerVariable = 0;
                    gameboy!.TimerCounter = clockSpeed;
                }
            }


            else if (addr == 0xFF0F)
            {
                IF = value;
            }

            else if (addr >= 0xFF10 && addr <= 0xFF3F) // Audio registers → APU
            {
                Apu.WriteRegister(addr, value);
            }

            else if (addr == 0xFF44)
            {
                Memory[addr] = 0;
            }

            else if (addr == 0xFF46)
                gameboy.DMATransfer(value);

            else if ((addr >= 0xFF4C) && (addr <= 0xFF7F))
            {
            }

            else if (addr == 0xFFFF)
            {
                IE = value;
            }
            else
                Memory[addr] = value;

        }

        public byte ReadByteFromMemory(uint addr)
        {

            //return memory[addr]; // Required for JSON Tests

            if (addr >= 0x4000 && addr <= 0x7FFF)
            {
                uint newAddr = addr;
                newAddr += (uint)((CurrentROMBank - 1) * 0x4000);
                return Cartridge[newAddr];
            }

            else if (addr >= 0xA000 && addr <= 0xBFFF)
            {
                uint newAddr = addr - 0xA000;
                return RAMBanks[newAddr + (CurrentRAMBank * 0x2000)];
            }

            else if (addr == 0xFF00)
            {
                return gameboy!.GetKeypadState();
            }

            else if (addr == 0xFF04) return (byte)new Random().Next(0, 255);

            else if (addr >= 0xFF10 && addr <= 0xFF3F) // Audio registers → APU
            {
                return Apu.ReadRegister(addr);
            }

            else if (addr == 0xFF0F)
            {
                return IF;
            }

            else if (addr == 0xFFFF)
            {
                return IE;
            }

            return Memory[addr];
        }

        public uint ReadWordFromMemory(uint addr)
        {
            return (uint)ReadByteFromMemory(addr + 1) << 8 | ReadByteFromMemory(addr);
        }

        public void WriteWordToMemory(uint addr, uint value)
        {
            WriteByteToMemory(addr + 1, (byte)(value >> 8));
            WriteByteToMemory(addr, (byte)value);
        }

        public void LoadMemory(string filePath, int startAddr, int length)
        {
            Array.Copy(File.ReadAllBytes(filePath), 0, Memory, startAddr, length);
        }

        public void InitROMBanks()
        {
            byte b = Memory[0x147];
            switch (b)
            {
                case 1:
                case 2:
                case 3:
                    ROMBank1 = true;
                    return;
                case 5:
                case 6:
                    ROMBank2 = true;
                    return;
            }
        }

    }

}