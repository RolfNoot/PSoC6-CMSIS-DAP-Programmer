// **********************************************************************
//     PSoC6 CMSIS-DAP Programmer - ELF Segment Loader
// ______________________________________________________________________
//
// Copyright (c) 2025 Rolf Nooteboom
// SPDX-License-Identifier: AGPL-3.0-or-later WITH additional terms
//
// Licensed under the GNU Affero General Public License v3.0 or later (AGPL-3.0)
// with the following modifications:
// - This software may be used **only for non-commercial purposes**.
// - All derivative works must be shared under the same license and
//   must be reported back to the original author (Rolf Nooteboom).
// - The original copyright, license, and attribution notices must be retained.
//
// References:
// - CMSIS-DAP: https://arm-software.github.io/CMSIS_5/DAP/html/index.html
// - Infineon PSoC 6 Programming Specification 002-15554 Rev. *O
//
// Description:
// - Minimal ELF32 loader for PSoC6 programming tools
// - Extracts PT_LOAD segments for flash programming
// - Zero-fills memory gaps and supports full ELF parsing
//
//  Author: Rolf Nooteboom <rolf@nooteboom-elektronica.com>
//  Created: 2025
//
// **********************************************************************

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PSoC6_CmsisDapPrg
{
        /// <summary>
        /// Represents a program header segment, including its type, load address, size, and data (if PT_LOAD).
        /// </summary>
        public class ProgramSegment
    {
        public uint Type { get; }
        public string TypeName { get; }
        public uint LoadAddress { get; }
        public uint FileSize { get; }
        public byte[] Data { get; }

        public ProgramSegment(uint type, string typeName, uint loadAddress, uint fileSize, byte[] data)
        {
            Type = type;
            TypeName = typeName;
            LoadAddress = loadAddress;
            FileSize = fileSize;                // unpadded data size
            Data = data;
        }
    }

    /// <summary>
    /// ELF32 loader that returns all program headers (segments).
    /// For PT_LOAD segments, Data is zero-filled to MemorySize; otherwise Data is empty.
    /// </summary>
    public static class ElfLoader
    {
        private const uint PT_LOAD = 1;
        private static readonly Dictionary<uint, string> SegmentTypeNames = new()
        {
            {0, "PT_NULL"}, {1, "PT_LOAD"}, {2, "PT_DYNAMIC"},
            {3, "PT_INTERP"}, {4, "PT_NOTE"},    {5, "PT_SHLIB"},
            {6, "PT_PHDR"},  {7, "PT_TLS"},
            // add other types as needed
        };

        /// <summary>
        /// Parses the ELF file and returns a list of all program header segments.
        /// </summary>
        public static List<ProgramSegment> LoadSegments(string elfPath)
        {
            using var fs = new FileStream(elfPath, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);

            // 1) Verify ELF32 magic
            var id = br.ReadBytes(16);
            if (id.Length != 16 || id[0] != 0x7F || id[1] != (byte)'E' || id[2] != (byte)'L' || id[3] != (byte)'F')
                throw new InvalidDataException("Not an ELF file");
            if (id[4] != 1) throw new NotSupportedException("Only ELF32 supported");

            // 2) Read ELF header to find program header table
            br.ReadUInt16();       // e_type
            br.ReadUInt16();       // e_machine
            br.ReadUInt32();       // e_version
            br.ReadUInt32();       // e_entry
            uint phOff = br.ReadUInt32();  // program header offset
            br.ReadUInt32();       // e_shoff
            br.ReadUInt32();       // e_flags
            br.ReadUInt16();       // e_ehsize
            ushort phEntSize = br.ReadUInt16();
            ushort phCount = br.ReadUInt16();
            // skip remaining header fields
            br.ReadUInt16(); br.ReadUInt16(); br.ReadUInt16(); br.ReadUInt16();

            var segments = new List<ProgramSegment>();

            // 3) Iterate all program headers
            for (int i = 0; i < phCount; i++)
            {
                br.BaseStream.Seek(phOff + i * phEntSize, SeekOrigin.Begin);
                uint pType = br.ReadUInt32();
                uint pOff = br.ReadUInt32();
                br.ReadUInt32();           // p_vaddr (virtual address)
                uint pAddr = br.ReadUInt32();  // p_paddr (load address)
                uint pFileSz = br.ReadUInt32();
                uint pMemSz = br.ReadUInt32();
                br.ReadUInt32();           // p_flags
                br.ReadUInt32();           // p_align

                // Prepare data buffer
                byte[] data;
                if (pType == PT_LOAD)
                {
                    data = new byte[pMemSz];  // zero-filled by default
                    if (pFileSz > 0)
                    {
                        br.BaseStream.Seek(pOff, SeekOrigin.Begin);
                        int read = br.Read(data, 0, (int)pFileSz);
                        if (read != pFileSz)
                            throw new EndOfStreamException("Failed to read segment data");
                    }
                }
                else
                {
                    data = Array.Empty<byte>();
                }

                // Map type number to name (fallback to hex)
                SegmentTypeNames.TryGetValue(pType, out var name);
                name ??= pType.ToString("X");

                segments.Add(new ProgramSegment(pType, name, pAddr, pFileSz, data));
            }

            return segments;
        }
    }
}
