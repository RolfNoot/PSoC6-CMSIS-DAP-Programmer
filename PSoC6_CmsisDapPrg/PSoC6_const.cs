// **********************************************************************
//     PSoC6 CMSIS-DAP Programmer - Device Mapping and Target Constants
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
// - Defines abstract base class and implementations for different PSoC6 devices
// - Contains memory map constants and SROM API command codes
// - Used for determining memory layout and programming behavior per target
//
//  Author: Rolf Nooteboom <rolf@nooteboom-elektronica.com>
//  Created: 2025
//
// **********************************************************************

using System;

namespace PSoC6_CmsisDapPrg
{
    /// <summary>Abstract base class for PSoC device constants (common and target‑specific).</summary>
    public abstract class PSoCclass
    {
        // Base (Common) Constants (available to all targets)
        public uint MEM_BASE_ROM                                = 0x00000000;                   ///< <summary>Base address of System.</summary>
        public uint MEM_BASE_SRAM                               = 0x08000000;                   ///< <summary>Base address of SRAM.</summary>
        public uint MEM_BASE_FLASH                               = 0x10000000;                   ///< <summary>Base address of application flash.</summary>
        public uint MEM_BASE_AUXFLASH                            = 0x14000000;                   ///< <summary>Base address of auxiliary flash.</summary>
        public uint MEM_SIZE_AUXFLASH                           = 0x00020000;                   ///< <summary>Size of auxiliary flash.</summary>
        public uint MEM_BASE_SFLASH                             = 0x16000000;                   ///< <summary>Base address of supervisory flash.</summary>
        public uint MEM_SIZE_SFLASH                              = 0x00008000;                   ///< <summary>Size of supervisory flash.</summary>
        public uint ROW_SIZE                                    = 512;                          ///< <summary>Flash row size in bytes.</summary>

        // IPC and Structures definitions
        public uint IPC_INTR_STRUCT_SIZE                         = 0x20;                         ///< <summary>Size of IPC interrupt structure.</summary>
        public uint IPC_STRUCT_SIZE                             = 0x20;                         ///< <summary>Size of IPC structure.</summary>
        public uint IPC_STRUCT0                 { get { return MEM_BASE_IPC; } }                  ///< <summary>CM0+ IPC structure.</summary>
        public uint IPC_STRUCT1                 { get { return IPC_STRUCT0 + IPC_STRUCT_SIZE; } } ///< <summary>CM4 IPC structure.</summary>
        public uint IPC_STRUCT2                 { get { return IPC_STRUCT1 + IPC_STRUCT_SIZE; } } ///< <summary>DAP IPC structure.</summary>
        public uint IPC_STRUCT_ACQUIRE_OFFSET                   = 0x00;                         ///< <summary>Offset to acquire IPC lock.</summary>
        public uint IPC_STRUCT_NOTIFY_OFFSET                    = 0x08;                         ///< <summary>Offset for IPC notification events.</summary>
        public uint IPC_STRUCT_DATA_OFFSET                      = 0x0C;                         ///< <summary>Offset for IPC 32-bit data element.</summary>
        public uint IPC_STRUCT_LOCK_STATUS_ACQUIRED_MSK         = 0x80000000;                   ///< <summary>Mask indicating IPC lock acquired.</summary>
        public uint IPC_STRUCT_ACQUIRE_SUCCESS_MSK              = 0x80000000;                   ///< <summary>Mask for successful IPC acquisition.</summary>
        public uint IPC_INTR_STRUCT_INTR_MASK_OFFSET            = 0x08;                         ///< <summary>Interrupt mask register offset in IPC_INTR_STRUCT.</summary>

        // SROM API masks
        public uint SROMAPI_DATA_LOCATION_MSK                   = 0x00000001;                   ///< <summary>SROM API data location mask.</summary>
        public uint SROMAPI_STATUS_MSK                          = 0xF0000000;                   ///< <summary>SROM API status mask.</summary>
        public uint SROMAPI_STAT_SUCCESS                        = 0xA0000000;                   ///< <summary>SROM API success status.</summary>
        
        // Sys call IDs (SROM API Opcodes)
        public uint SROMAPI_SILID_CODE                          = 0x00000001;                   ///< <summary>SROM API Silicon ID opcode.</summary>
        public uint SROMAPI_WRITEROW_CODE                       = 0x05000100;                   ///< <summary>SROM API WriteRow opcode.</summary>
        public uint SROMAPI_PROGRAMROW_CODE                     = 0x06000100;                   ///< <summary>SROM API ProgramRow opcode.</summary>
        public uint SROMAPI_ERASEROW_CODE                       = 0x1C000100;                   ///< <summary>SROM API EraseRow opcode.</summary> // [31:24]: Opcode = 0x1C; [15:8]: 0x01 - blocking; [0]: 0 - arguments in SRAM
        public uint SROMAPI_ERASEALL_CODE                       = 0x0A000001;                   ///< <summary>SROM API EraseAll opcode.</summary>
        public uint SROMAPI_ERASESECTOR_CODE                    = 0x14000100;                   ///< <summary>SROM API EraseSector opcode.</summary>
        public uint SROMAPI_ERASESUBSECTOR_CODE                 = 0x1D000100;                   ///< <summary>SROM API EraseSector opcode.</summary>
        public uint SROMAPI_CHECKSUM_CODE                       = 0x0B000001;                   ///< <summary>SROM API Checksum opcode.</summary>
        public uint SROMAPI_CHECKSUM_DATA_MSK                   = 0x0FFFFFFF;                   ///< <summary>SROM API Checksum data mask.</summary>
        
        public uint SROMAPI_BLOW_FUSE_CODE                      = 0x01000001;                   ///< <summary>SROM API BlowFuse opcode.</summary>
        public uint SROMAPI_READ_FUSE_CODE                      = 0x03000001;                   ///< <summary>SROM API ReadFuse opcode.</summary>
        public uint SROMAPI_GENERATE_HASH_CODE                  = 0x1E000000;                   ///< <summary>SROM API GenerateHASH opcode.</summary>
        public uint SROMAPI_CHECK_FACTORY_HASH_CODE             = 0x27000001;                   ///< <summary>SROM API CheckFactoryHASH opcode.</summary>
        public uint SROMAPI_TRANSITION_TO_SECURE_CODE           = 0x2F000000;
        
        public uint SRAM_SCRATCH_ADDR           { get { return MEM_BASE_SRAM + 0x00003000; } }   ///< <summary>SRAM scratch area for SROM API parameters.</summary>
        public uint SRSS_TST_MODE_GLOBAL                         = 0x40260100;                   ///< <summary>Global SRSS Test Mode register address.</summary>
        public uint SRSS_TST_MODE_TEST_MODE_MSK_GLOBAL          = 0x80000000;                   ///< <summary>Global SRSS TEST_MODE enable mask.</summary>

        // Virtual properties for target‑specific constants
        public virtual uint MEM_SIZE_ROM                        => 0x00010000;                  ///< <summary>System ROM size.</summary>
        public virtual uint MEM_SIZE_FLASH                      => 0x00040000;                  ///< <summary>System ROM size.</summary>
        public virtual uint MEM_BASE_IPC                        => 0x40220000;                  ///< <summary>Base address for IPC structures.</summary>
        public virtual uint MEM_VTBASE_CM0                      => 0x40201120;                  ///< <summary>CM0_VECTOR_TABLE_BASE.</summary>
        public virtual uint MEM_VTBASE_CM4                      => 0x40200200;                  ///< <summary>CM4_VECTOR_TABLE_BASE.</summary>
        public virtual uint MEM_BASE_PPU4                       => 0x40010100;                  ///< <summary>Base address for PPU[4].</summary>
        public virtual uint IPC_INTR_STRUCT                     => 0x40221000;                  ///< <summary>IPC interrupt structure base.</summary>
        public virtual uint IPC_STRUCT_LOCK_STATUS_OFFSET       => 0x1C;                        ///< <summary>IPC lock status offset.</summary>
        public virtual uint SRSS_TST_MODE                       => 0x40260100;                  ///< <summary>SRSS Test Mode register.</summary>
        public virtual uint SRSS_TST_MODE_TEST_MODE_MSK         => 0x80000000;                  ///< <summary>SRSS Test Mode mask.</summary>
    }

    /// <summary>Target-specific constants for PSOC6ABLE2 devices.</summary>
    public class PSoC6Able2 : PSoCclass
    {
        public override uint MEM_SIZE_ROM                       => 0x00020000;                  ///< <summary>128 KB ROM for PSOC6ABLE2.</summary>
        public override uint MEM_SIZE_FLASH                     => 0x00100000;                  ///< <summary>1MB FLASH for PSOC6ABLE2.</summary>
        public override uint MEM_BASE_IPC                       => 0x40230000;                  ///< <summary>Base IPC address for PSOC6ABLE2.</summary>
        public override uint MEM_VTBASE_CM0                     => 0x402102B0;                  ///< <summary>CM0_VECTOR_TABLE_BASE for PSOC6ABLE2.</summary>
        public override uint MEM_VTBASE_CM4                     => 0x402102C0;                  ///< <summary>CM4_VECTOR_TABLE_BASE for PSOC6ABLE2.</summary>
        public override uint MEM_BASE_PPU4                      => 0x40014100;                  ///< <summary>PPU[4] base for PSOC6ABLE2.</summary>
        public override uint IPC_INTR_STRUCT                    => 0x40231000;                  ///< <summary>IPC interrupt structure for PSOC6ABLE2.</summary>
        public override uint IPC_STRUCT_LOCK_STATUS_OFFSET      => 0x10;                        ///< <summary>IPC lock status offset for PSOC6ABLE2.</summary>
        public override uint SRSS_TST_MODE                      => 0x40260100;                  ///< <summary>SRSS Test Mode register for PSOC6ABLE2.</summary>
        public override uint SRSS_TST_MODE_TEST_MODE_MSK        => 0x80000000;                  ///< <summary>SRSS test mode mask for PSOC6ABLE2.</summary>
    }

    /// <summary>Target-specific constants for PSOC6A2M devices.</summary>
    public class PSoC6A2M : PSoC6A256K
    {
        public override uint MEM_SIZE_FLASH                     => 0x00200000;                  ///< <summary>2M FLASH for PSoC6A2M.</summary>
    }

    /// <summary>Target-specific constants for PSOC6A512K devices.</summary>
    public class PSoC6A512K : PSoC6A256K
    {
        public override uint MEM_SIZE_FLASH                     => 0x00080000;                  ///< <summary>512KB FLASH for PSoC6A512K.</summary>
    }

    /// <summary>Target-specific constants for PSOC6A256K devices.</summary>
    public class PSoC6A256K : PSoCclass
    {
        public override uint MEM_SIZE_FLASH                     => 0x00040000;                  ///< <summary>256KB FLASH for PSOC6A256K.</summary>
    }
}
