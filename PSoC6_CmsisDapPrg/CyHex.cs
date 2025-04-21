// **********************************************************************
//     PSoC6 CMSIS-DAP Programmer - Hex Parsing and Mapping Utilities
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
// - Parses Intel HEX files for Cypress/Infineon PSoC6
// - Maps memory regions using defined layout and merges adjacent rows
// - Used with a CMSIS-DAP based flashing tool
//
//  Author: Rolf Nooteboom <rolf@nooteboom-elektronica.com>
//  Created: 2025
//
// **********************************************************************


using HidSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static PSoC6_CmsisDapPrg.CmsisDap;
using static PSoC6_CmsisDapPrg.PSoCMap_c;

//using DebugForm;

/*
Intel Hex File Format.
Pos Description
1   Record Marker: 
    The first character of the line is always a colon (ASCII 0x3A) 
    to identify the line as an Intel HEX file 

2-3 Record Length: [0]
    This field contains the number of data bytes in the register 
    represented as a 2-digit hexadecimal number. This is the 
    total number of data bytes, not including the checksum byte 
    nor the first 9 characters of the line.   

4-7 Address: [1] [2]
    This field contains the address where the data should be 
    loaded into the chip. This is a value from 0 to 65,535 
    represented as a 4-digit hexadecimal value. 

8-9 Record Type: [3]
    This field indicates the type of record for this line. 
    The possible values are: 
    00=Register contains normal data. 
    01=End of File. 
    02=Extended address.
    03=Start Segment Address Record
    04=Extended Linear Address Record
    05=Start Linear Address Record

10-? Data Bytes: {4...]
    The following bytes are the actual data that 
    will be burned into the EPROM. The data is represented as 
    2-digit hexadecimal values. 

Last 2 Checksum: 
     The last two characters of the line are a checksum for the 
     line. The checksum value is calculated by taking the two's 
     complement of the sum of all the preceding data bytes, 
     excluding the checksum byte itself and the colon at the
     beginning of the line.
*/

namespace PSoC6_CmsisDapPrg
{
    public class CyRecord_c
    {
        public List<string> Regions { get; } = new();

        // multi‐block regions
        public List<Segment_c> FlashBlocks = new List<Segment_c>();  // ApplicationFlash
        public List<Segment_c> FlashECCBlocks = new List<Segment_c>();  // PSoC5LP EccFlash
        public List<Segment_c> EepromBlocks = new List<Segment_c>();  // PSoC5LP/Eeprom, PSoC6 Eeprom
        public List<Segment_c> SFlashBlocks = new List<Segment_c>();  // all PSoC6 supervisory‐flash subregions
        public List<Segment_c> XipBlocks = new List<Segment_c>();  // PSoC6 XIP

        // single‐block regions
        public Segment_c? Checksum; // the 2‑byte/1‑MB slots
        public Segment_c? FlashProt; // FlashProtection
        public Segment_c? MetaData; // Meta/Metadata
        public Segment_c? ChipProt; // ChipProtection / eFuse

        // PSoC5LP “cust” & “wo” NVLs
        public Segment_c? NVuser; // CustNVL
        public Segment_c? NVWO; // WoNVL

        // PSoC6 supervisory‐flash subregions
        public Segment_c? SFlashUserData;
        public Segment_c? NAR;
        public Segment_c? PublicKey;
        public Segment_c? TOC2;
        public Segment_c? RTOC2;

        // PSoC6 eFuse (chip‐level protection)
        public Segment_c? Efuse;

        // lookup tables to avoid giant switch statements
        private readonly Dictionary<string, List<Segment_c>> _blockLists;
        private readonly Dictionary<string, Action<Segment_c>> _singleSetters;

        public CyRecord_c()
        {
            // map region-name → which List to append to
            _blockLists = new Dictionary<string, List<Segment_c>>()
            {
                ["ApplicationFlash"] = FlashBlocks,
                ["EccFlash"] = FlashECCBlocks,
                ["Eeprom"] = EepromBlocks,
                ["SFlashUserData"] = SFlashBlocks,
                ["NAR"] = SFlashBlocks,
                ["PublicKey"] = SFlashBlocks,
                ["TOC2"] = SFlashBlocks,
                ["RTOC2"] = SFlashBlocks,
                ["XIP"] = XipBlocks,
            };

            // map region-name → which property to set
            _singleSetters = new Dictionary<string, Action<Segment_c>>()
            {
                ["NVuser"] = s => NVuser = s,
                ["NVWO"] = s => NVWO = s,
                ["Checksum"] = s => Checksum = s,
                ["FlashProtection"] = s => FlashProt = s,
                ["MetaData"] = s => MetaData = s,
                ["ChipProtection"] = s => ChipProt = s,
                ["eFuse"] = s => Efuse = s,
                // if you need others as single slots:
                ["SFlashUserData"] = s => SFlashUserData = s,
                ["NAR"] = s => NAR = s,
                ["PublicKey"] = s => PublicKey = s,
                ["TOC2"] = s => TOC2 = s,
                ["RTOC2"] = s => RTOC2 = s,
            };
        }

        /// <summary>
        ///  Universal Add: records the region name in order,
        ///  then either appends to the correct block list
        ///  or sets the single‑slot property.
        /// </summary>
        public void Add(string regionName, Segment_c Segment)
        {
            if (_blockLists.TryGetValue(regionName, out var list))
            {
                list.Add(Segment);
                Regions.Add(regionName);
                return;
            }

            if (_singleSetters.TryGetValue(regionName, out var setter))
            {
                setter(Segment);
                Regions.Add(regionName);
                return;
            }

            throw new ArgumentException(
                $"Region '{regionName}' is not registered as block or single.",
                nameof(regionName));
        }
    }
    public class Segment_c
    {
        public List<byte> bytes = new List<byte>();
        public UInt32 firstAddress;
        public UInt32 lastAddress;
    }
    
    public class HexFileParser
    {
        //                         Flash                                flashECC      NVlatch   NVlock       EEprom    checksum    flashprot    metadata   chipprot
        //  BLOCK_BASE =        { 0x00000000, 0x00100000, 0x00200000, 0x80000000, 0x90000000, 0x90100000, 0x90200000, 0x90300000, 0x90400000, 0x90500000, 0x90600000, 0x10080000 };

        // MAX_BLOCK_LENGTH =    { 0x00040000, 0x00000100, 0x00000100, 0x00010000, 0x00000100, 0x00000100, 0x00000100, 0x00000100, 0x00000100, 0x00000100, 0x00080000 };

        public static int HexToArray(String HexIn, out List<Segment_c> Sections, out string strMSG)
        {
            strMSG = "";
            Segment_c Segment = new Segment_c();
            Sections = new List<Segment_c>(1);
            bool createArray = true;
            String record = "";
            byte dataLength = 0;
            UInt32 address = 0;
            UInt32 nextAddress = 0;
            byte recordType = 0;
            byte[]? RecordData = null;
            int sum = 0;
            int dataCount = 0;

            // Seperate Records in File
            string[] records = HexIn.Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            int RecordCount = 0;
            while (records.Length != RecordCount)
            {
                // Get Current Record
                record = records[RecordCount];
                RecordCount++;

                // Convert Record into Bytes
                RecordData = new byte[record.Length >> 1];
                for (sum = 0, dataCount = 1; dataCount < record.Length; dataCount += 2)
                {
                    RecordData[dataCount >> 1] = Convert.ToByte(record.Substring(dataCount, 2), 16);
                    sum += RecordData[dataCount >> 1];
                }
                if ((sum & 255) != 0)
                {
                    strMSG = "Invalid checksum in Record";
                    return -1;
                }

                // Get Number of Data Bytes in Record
                dataLength = RecordData[0];
                if (dataLength != RecordData.Length - 5)
                {
                    strMSG = "Invalid Record Length";
                    return -1;
                }

                // Get Address Bytes
                address &= 0xFFFF0000;
                address |= (UInt16)((RecordData[1] << 8) + RecordData[2]);

                // Get Record Type
                recordType = RecordData[3];

                switch (recordType)
                {
                    case 0:     // Data Record
                        if (nextAddress != address) createArray = true;
                        if (createArray)
                        {
                            createArray = false;
                            Segment = new Segment_c();
                            Sections.Add(Segment);
                            Segment.firstAddress = address;
                            Segment.bytes = new List<byte>(0);
                        }
                        Segment.bytes!.AddRange(RecordData.Skip(4).Take(dataLength));
                        nextAddress = address + dataLength;
                        break;
                    case 1:     // End Of File Record
                        if (dataLength != 0)
                        {
                            strMSG = "Invalid End Of File Record";
                            return -1;

                        }
                        break;
                    case 2:     // Extended Segment Address Record

                        if (dataLength != 2) return -1;   // Invalid Extended Segment Address Record

                        address = (UInt32)(RecordData[4] << 12) + (UInt32)(RecordData[5] << 4);
                        createArray = true;
                        break;
                    case 3:     // Start Segment Address Record
                        break;
                    case 4:     // Extended Linear Address Record
                        if (dataLength != 2)
                        {
                            strMSG = "Invalid Extended Linear Address Record";
                            return -1;
                        }
                        address = (UInt32)(RecordData[4] << 24) + (UInt32)(RecordData[5] << 16);
                        createArray = true;
                        break;
                    case 5:     // Start Linear Address Record
                        if (dataLength != 4)
                        {
                            strMSG = "Invalid Start Linear Address Record";
                            return -1;
                        }
                        address = (UInt32)(RecordData[4] << 24) + (UInt32)(RecordData[5] << 16) + (UInt32)(RecordData[6] << 8) + (UInt32)RecordData[7];
                        createArray = true;
                        break;
                }
            }
            for (int cnt = 0; cnt < Sections.Count; cnt++) Sections[cnt].lastAddress = Sections[cnt].firstAddress + (uint)Sections[cnt].bytes!.Count - 1;

            return 0;
        }

        public static int HexToCyRecord(String HexIn, Device_e Device, out CyRecord_c CyRecord, out string strMSG)
        {
            int hr = HexToArray(HexIn, out List<Segment_c> Sections, out strMSG);
            SegmentsToCyRecord(Device, Sections, out CyRecord);
            
            return hr;
        }

        public static void GccElfToCyRecord(List<ProgramSegment> ProgramSegments, Device_e Device, out CyRecord_c CyRecord)
        {
            List<Segment_c> Segments = new List<Segment_c>();
            foreach (var ProgramSegment in ProgramSegments)
            {
                if (ProgramSegment.Data.Length == 0) continue;
                Segments.Add(new Segment_c { bytes = ProgramSegment.Data.ToList(), firstAddress = ProgramSegment.LoadAddress, lastAddress = (uint)(ProgramSegment.LoadAddress + ProgramSegment.Data.Length - 1) });
            }
            SegmentsToCyRecord(Device_e.PSoC6, Segments, out CyRecord);
            // Merge Flash blocks if data is in the same or adjecent row (also for SFlash, Eeprom, ECC, XIP etc)
            MergeAllAdjacent(CyRecord, Device);
        }

        /// <summary>
        /// Walks each of the CyRecord’s block‑lists (Flash, ECC, EEPROM, SFlash, XIP),
        /// merges any sections whose rows touch or are adjacent (forward or backward),
        /// and removes the corresponding duplicate names from CyRecord.Regions.
        /// </summary>
        public static void MergeAllAdjacent(CyRecord_c record, Device_e Device)
        {
            int RowSize = new PSoCMap_c(Device).RowSize;
            // for each group of regionNames that go into the same list:
            MergeAdjacent(record.FlashBlocks, record.Regions, "ApplicationFlash", RowSize);
            MergeAdjacent(record.FlashECCBlocks, record.Regions, "EccFlash", RowSize);
            MergeAdjacent(record.EepromBlocks, record.Regions, "Eeprom", RowSize);

            var SflashNames = new HashSet<string>
            {
                "SFlashUserData",
                "NAR",
                "PublicKey",
                "TOC2",
                "RTOC2"
            };
            for (int i = 0; i < record.Regions.Count; i++)
            {
                if (SflashNames.Contains(record.Regions[i]))
                    record.Regions[i] = "SFlash";
            }
            MergeAdjacent(record.SFlashBlocks, record.Regions, "SFlash", RowSize);
            MergeAdjacent(record.XipBlocks, record.Regions, "XIP", RowSize);
        }

        public static void MergeAdjacent(List<Segment_c> sections, List<string> Regions, string regionName, int rowSize)
        {
            if (sections.Count < 2) return;

            uint mask = ~(uint)(rowSize - 1);

            // get all the indexes in regionOrder for this region
            var positions = Regions
                .Select((name, idx) => (name, idx))
                .Where(x => x.name == regionName)
                .Select(x => x.idx)
                .ToList();

            if (positions.Count < 2) return;

            // walk backwards so removals don’t upset our indexing
            for (int k = positions.Count - 1; k >= 1; k--)
            {
                int posCurr = positions[k];
                var curr = sections[k];
                var prev = sections[k - 1];

                // row‐aligned boundaries
                uint prevFirstRow = prev.firstAddress & mask;
                uint prevLastRow = prev.lastAddress & mask;
                uint currFirstRow = curr.firstAddress & mask;
                uint currLastRow = curr.lastAddress & mask;

                bool forwardMerge = currFirstRow >= prevLastRow && currFirstRow - prevLastRow <= (uint)rowSize;

                bool backwardMerge = prevFirstRow >= currLastRow && prevFirstRow - currLastRow <= (uint)rowSize;

                if (forwardMerge || backwardMerge)
                {
                    if (forwardMerge)
                    {
                        // FORWARD: curr sits at or after prev
                        uint gap = curr.firstAddress - (prev.lastAddress + 1);
                        if (gap > 0)
                            prev.bytes!.AddRange( Enumerable.Repeat((byte)0x00, (int)gap));
                        if (curr.bytes != null)
                            prev.bytes.AddRange(curr.bytes);
                        prev.lastAddress = curr.lastAddress;
                    }
                    else
                    {
                        // BACKWARD: curr sits before prev
                        uint gap = prev.firstAddress - (curr.lastAddress + 1);
                        if (gap > 0)
                            prev.bytes!.InsertRange( 0, Enumerable.Repeat((byte)0x00, (int)gap));
                        if (curr.bytes != null)
                            prev.bytes.InsertRange(0, curr.bytes);
                        prev.firstAddress = curr.firstAddress;
                    }

                    // remove the now‐merged Segment and its regionOrder entry
                    sections.RemoveAt(k);
                    Regions.RemoveAt(posCurr);
                    // no need to adjust earlier positions[], we're iterating backwards
                }
            }
        }


        public static void SegmentsToCyRecord(Device_e device, List<Segment_c> Sections, out CyRecord_c CyRecord)
        {
            var Regions = new PSoCMap_c(device).Regions;

            CyRecord = new CyRecord_c();

            foreach (var sec in Sections)
            {
                var region = Regions.FirstOrDefault(r => r.Contains(sec.firstAddress));
                if (region == null) continue;

                CyRecord.Add(region.Name, sec);
            }
        }

        public static string SectionToHex(Segment_c Segment, int RecordLength)
        {
            string HexBlockOut = "";
            int RecordChecksum;
            int HexChecksum = 0;
            int BlockCount = 0;
            int ByteCount = 0;

            if (Segment.bytes!.Count < 1) return "";

            while (BlockCount < Segment.bytes.Count)
            {
                UInt32 BlockBaseNew = (UInt32)(Segment.firstAddress + BlockCount);
                if (BlockBaseNew > 0xFFFF) // Add Extended Record
                {
                    uint BlockBaseExtended = BlockBaseNew >> 16;
                    RecordChecksum = 6 + (int)((BlockBaseExtended >> 8) & 255) + (int)(BlockBaseExtended & 255);
                    HexBlockOut += ":02000004" + BlockBaseExtended.ToString("X4") + (-RecordChecksum & 255).ToString("X2") + "\r\n";
                }
                RecordChecksum = 0;

                byte[] RecordArray = new byte[RecordLength];

                UInt32 SegmentAmount = (UInt32)(Segment.bytes.Count - BlockCount);
                if (((BlockBaseNew & 0xFFFF) + SegmentAmount) > 0xFFFF)
                {
                    SegmentAmount = 0x10000 - (BlockBaseNew & 0xFFFF);
                }
                for (; SegmentAmount > 0; SegmentAmount--)
                {

                    RecordChecksum += Segment.bytes[BlockCount];
                    HexChecksum += Segment.bytes[BlockCount];
                    RecordArray[ByteCount] = Segment.bytes[BlockCount];
                    if ((++ByteCount == RecordLength) || (BlockCount == Segment.bytes.Count - 1))
                    {
                        UInt16 RecordAddress = (UInt16)(Segment.firstAddress + BlockCount + 1 - ByteCount);
                        if (ByteCount < RecordLength) Array.Resize(ref RecordArray, ByteCount);
                        RecordChecksum += ByteCount + (int)((RecordAddress >> 8) & 255) + (int)(RecordAddress & 255);
                        HexBlockOut += ":" + ByteCount.ToString("X2") + RecordAddress.ToString("X4") + "00" + String.Concat(RecordArray.Select(b => b.ToString("X2"))) + (-RecordChecksum & 255).ToString("X2") + "\r\n";
                        ByteCount = 0; RecordChecksum = 0;
                    }
                    BlockCount++;
                }
            }
            return HexBlockOut;
        }

        public static int SectionsToHEX(List<Segment_c> Sections, out string HEXout)
        {
            const int RecordLength = 0x40; HEXout = "";
            Segment_c HEXarray;
            for (int cnt = 0; cnt < Sections.Count; cnt++)
            {
                HEXarray = Sections[cnt];
                HEXout += SectionToHex(HEXarray, RecordLength);
            }

            HEXout += ":00000001FF"; // End of File
            return 0;
        }

        public static int CyRecordToHex(CyRecord_c CyRecord, out string HexOut)
        {
            const int RecordLength = 0x40;
            var sb = new StringBuilder();

            int flashIdx = 0;
            int eccIdx = 0;
            int eepromIdx = 0;
            int sflashIdx = 0;
            int xipIdx = 0;

            foreach (var region in CyRecord.Regions)
            {
                switch (region)
                {
                    case "ApplicationFlash":
                        if (flashIdx < CyRecord.FlashBlocks.Count) sb.Append(SectionToHex(CyRecord.FlashBlocks[flashIdx++], RecordLength));
                        break;

                    case "EccFlash":
                        if (eccIdx < CyRecord.FlashECCBlocks.Count) sb.Append(SectionToHex(CyRecord.FlashECCBlocks[eccIdx++], RecordLength));
                        break;

                    case "Eeprom":
                        if (eepromIdx < CyRecord.EepromBlocks.Count) sb.Append(SectionToHex(CyRecord.EepromBlocks[eepromIdx++], RecordLength));
                        break;

                    case "XIP":
                        if (xipIdx < CyRecord.XipBlocks.Count) sb.Append(SectionToHex(CyRecord.XipBlocks[xipIdx++], RecordLength));
                        break;

                    case "SFlashUserData":
                    case "NAR":
                    case "PublicKey":
                    case "TOC2":
                    case "RTOC2":
                        if (sflashIdx < CyRecord.SFlashBlocks.Count)
                            sb.Append(SectionToHex(CyRecord.SFlashBlocks[sflashIdx++], RecordLength));
                        break;

                    case "NVuser":
                        if (CyRecord.NVuser != null) sb.Append(SectionToHex(CyRecord.NVuser, RecordLength));
                        break;

                    case "NVWO":
                        if (CyRecord.NVWO != null) sb.Append(SectionToHex(CyRecord.NVWO, RecordLength));
                        break;

                    case "Checksum":
                        if (CyRecord.Checksum != null) sb.Append(SectionToHex(CyRecord.Checksum, RecordLength));
                        break;

                    case "FlashProtection":
                        if (CyRecord.FlashProt != null)  sb.Append(SectionToHex(CyRecord.FlashProt, RecordLength));
                        break;

                    case "Metadata":
                        if (CyRecord.MetaData != null) sb.Append(SectionToHex(CyRecord.MetaData, RecordLength));
                        break;

                    case "ChipProtection":
                        if (CyRecord.ChipProt != null) sb.Append(SectionToHex(CyRecord.ChipProt, RecordLength));
                        break;

                    case "eFuse":
                        if (CyRecord.Efuse != null) sb.Append(SectionToHex(CyRecord.Efuse, RecordLength));
                        break;
                }
            }
            // end‐of‐file record
            sb.Append(":00000001FF");

            HexOut = sb.ToString();
            return 0;
        }
    }
    public class PSoCMap_c
    {
        public enum Device_e
        {
            PSoC1,
            PSoC3,
            PSoC4,
            PSoC5,
            PSoC6,
            Detect
        }
        public class MemoryRegion
        {
            public string Name { get; }
            public uint Start { get; }
            public uint End { get; }

            public MemoryRegion(string name, uint start, uint size)
            {
                Name = name;
                Start = start;
                End = start + size;
            }
            public bool Contains(uint addr) => addr >= Start && addr < End;
        }
        public Device_e Device { get; }
        public MemoryRegion[] Regions { get; }
        public int RowSize { get; }

        public PSoCMap_c(Device_e device)
        {
            Device = device;
            switch (device)
            {
                case Device_e.PSoC1:
                    Regions = MemoryRegions.PSoC1;
                    RowSize = 256;
                    break;
                case Device_e.PSoC3:
                case Device_e.PSoC5:
                    Regions = MemoryRegions.PSoC5;
                    RowSize = 256;
                    break;
                case Device_e.PSoC4:
                    Regions = MemoryRegions.PSoC4;
                    RowSize = 256;
                    break;
                case Device_e.PSoC6:
                default:
                    Regions = MemoryRegions.PSoC6;
                    RowSize = 512;
                    break;
            }
        }
        public static class MemoryRegions
        {
            public static readonly MemoryRegion[] PSoC1 = new[]
{
                new MemoryRegion("ApplicationFlash",    0x0000_0000, 0x0010_0000),
                new MemoryRegion("FlashProtection",     0x0010_0000, 0x0001_0000),
                new MemoryRegion("Checksum",            0x0020_0000, 0x0000_0100),
            };

            public static readonly MemoryRegion[] PSoC4 = new[]
            {
                new MemoryRegion("ApplicationFlash",    0x0000_0000, 0x9000_0000),
                new MemoryRegion("Checksum",            0x9030_0000, 0x0010_0000),
                new MemoryRegion("FlashProtection",     0x9040_0000, 0x0010_0000),
                new MemoryRegion("Metadata",            0x9050_0000, 0x0010_0000),
                new MemoryRegion("ChipProtection",      0x9060_0000, 0x0000_0001),
            };

            public static readonly MemoryRegion[] PSoC5 = new[]
            {
                new MemoryRegion("ApplicationFlash",    0x0000_0000, 0x8000_0000),
                new MemoryRegion("EccFlash",            0x8000_0000, 0x0004_0000),    // 1MB
                new MemoryRegion("CustNVL",             0x9000_0000, 0x0010_0000),    // 1MB
                new MemoryRegion("WoNVL",               0x9010_0000, 0x0010_0000),    // 1MB
                new MemoryRegion("Eeprom",              0x9020_0000, 0x0010_0000),    // 1MB
                new MemoryRegion("Checksum",            0x9030_0000, 0x0010_0000),    // 1MB
                new MemoryRegion("FlashProtection",     0x9040_0000, 0x0010_0000),    // 1MB
                new MemoryRegion("MetaData",            0x9050_0000, 0x0010_0000),    // 1MB
            };

            public static readonly MemoryRegion[] PSoC6 = new[]
            {
                new MemoryRegion("ApplicationFlash",    0x1000_0000, 0x0020_0000),  // up to 2 MB
                new MemoryRegion("Eeprom",              0x1400_0000, 0x0000_8000),  //  32 KB
                new MemoryRegion("SFlashUserData",      0x1600_0800, 0x0000_0800),  //   2 KB
                new MemoryRegion("NAR",                 0x1600_1A00, 0x0000_0200),  // 512 B
                new MemoryRegion("PublicKey",           0x1600_5A00, 0x0000_0C00),  // ~3 KB
                new MemoryRegion("TOC2",                0x1600_7C00, 0x0000_0200),  // 512 B
                new MemoryRegion("RTOC2",               0x1600_7E00, 0x0000_0200),  // 512 B
                new MemoryRegion("XIP",                 0x1800_0000, 0x7800_0000),
                new MemoryRegion("Checksum",            0x9030_0000, 0x0000_0100),  // 2 bytes
                new MemoryRegion("MetaData",            0x9050_0000, 0x0000_0100),  // 12 bytes
                new MemoryRegion("eFuse",               0x9070_0000, 0x0000_1000),  // up to 4 KB
            };
        }

    }
}
