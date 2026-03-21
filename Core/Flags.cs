// ============================================================================
// Project:     GameboyEmu
// File:        Core/Flags.cs
// Description: CPU flag helpers (Zero, Subtract, HalfCarry, Carry)
// Author:      James Booth
// Created:     2024
// License:     MIT License - See LICENSE file in the project root
// Copyright:   (c) 2024-2026 James Booth
// Notice:      Game Boy is a registered trademark of Nintendo Co., Ltd.
//              This emulator is for educational purposes only.
// ============================================================================

namespace GameboyEmu.Core
{
    public class Flags
    {
        private bool z; // Zero bit
        private bool n; // Subtraction bit
        private bool h; // Half Carry bit
        private bool c; // Carry bit

        public Flags()
        {
            Z = false;
            N = false;
            H = false;
            c = false;
        }

        public bool Z
        {
            get { return z; }
            set { z = value; }
        }

        public bool N
        {
            get { return n; }
            set { n = value; }
        }

        public bool H
        {
            get { return h; }
            set { h = value; }
        }

        public bool C
        {
            get { return c; }
            set { c = value; }
        }

        public byte ToByte()
        {
            //           0 0 0 0
            //   7 6 5 4 3 2 1 0
            //   Z N H C 
            var flags = 0x00;
            if (z)
                flags = flags | 0x80;
            if (n)
                flags = flags | 0x40;
            if (h)
                flags = flags | 0x20;
            if (c)
                flags = flags | 0x10;
            return (byte)flags;
        }

        public void FromByte(byte bits, byte flags = 0x00)
        {
            //           0 0 0 0
            //   7 6 5 4 3 2 1 0
            //   Z N H C 
            if ((flags & 0b10000000) == 0x80) z = ((bits & 0x80) == 0x80);
            if ((flags & 0b01000000) == 0x40) n = ((bits & 0x40) == 0x40);
            if ((flags & 0b00100000) == 0x20) h = ((bits & 0x20) == 0x20);
            if ((flags & 0b00010000) == 0x10) c = ((bits & 0x10) == 0x10);
        }

        public void UpdateCarryFlag(int value)
        {
            c = (value >> 8) != 0; 
        }

        public void UpdateZeroFlag(int value)
        {
            z = (byte)value == 0;
        }

        public void SetHalfCarryAdd(byte a, byte b, bool carry = false)
        {
            if (carry)
            {
                int cry = C ? 1 : 0;
                h = ((a & 0xF) + (b & 0xF)) + cry > 0xF;
            }
            else
                h = ((a & 0xF) + (b & 0xF)) > 0xF;
        }

        public void SetHalfCarrySub(byte a, byte b, bool carry = false)
        {
            if (carry)
            {
                int cry = C ? 1 : 0;
                h = (a & 0xF) < (b & 0xF) + cry;
            }
            else
                h = (a & 0xF) < (b & 0xF);
        }
    }
}