// **********************************************************************
//     PSoC6 CMSIS-DAP Programmer - High-Level Flashing Logic
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
// - Provides full flashing workflow for PSoC6 over CMSIS-DAP
// - Supports acquisition, erase, program, and verify flows
// - Communicates using SWD or JTAG over DAP USB interface
//
//  Author: Rolf Nooteboom <rolf@nooteboom-elektronica.com>
//  Created: 2025
//
// **********************************************************************

namespace PSoC6_CmsisDapPrg
{
    /// <summary>Enumeration for target acquisition modes.</summary>
    public enum AcquireMode
    {
        ACQ_RESET,              ///< Reset the target.
        ACQ_POWER_CYCLE         ///< Power-cycle the target.
    }

    /// <summary>Enumeration for the SWJ interface type.</summary>
    public enum SWJ_Interface
    {
        SWD,                    ///< Serial Wire Debug.
        JTAG                    ///< JTAG interface.
    }

    /// <summary>Enumeration for time units used in delays.</summary>
    public enum timeUnit_e : int
    {
        TIME_1MS = 1,           ///< 1 millisecond.
        TIME_100MS = 100        ///< 100 millisecond.
    }

    /// <summary>Enumeration for Access Port selection.</summary>
    public enum AP_e
    {
        AP_SYS = 0,             ///< Use AP number 0 (typically for external debugger/DAP access).
        AP_CM0 = 1,             ///< Use AP number 1 (typically for CM0+).
        AP_CM4 = 2,             ///< Use AP number 2 (typically for CM4).
        AP_AUTO = 255           ///< Automatically scan and select an AP.
    }
    public enum ProtectionState_e : byte
    {
        VIRGIN = 0x01,
        NORMAL = 0x02,
        SECURE = 0x03,
        DEAD = 0x04
    }

    public class PSoC6Family
    {
        public string Name { get; }
        public ushort FamilyId { get; }
        public Func<PSoCclass> Factory { get; }

        private PSoC6Family(string name, ushort familyId, Func<PSoCclass> factory)
        {
            Name = name;
            FamilyId = familyId;
            Factory = factory;
        }

        public PSoCclass Create() => Factory();

        public static readonly PSoC6Family PSOC6ABLE2 = new("PSOC6ABLE2", 0x100, () => new PSoC6Able2());
        public static readonly PSoC6Family PSOC6A2M = new("PSOC6A2M", 0x102, () => new PSoC6A2M());
        public static readonly PSoC6Family PSOC6A512K = new("PSOC6A512K", 0x105, () => new PSoC6A512K());
        public static readonly PSoC6Family PSOC6A256K = new("PSOC6A256K", 0x10E, () => new PSoC6A256K());

        public static IEnumerable<PSoC6Family> All => new[] { PSOC6ABLE2, PSOC6A2M, PSOC6A512K, PSOC6A256K };

        public static PSoC6Family FromFamilyId(ushort familyId)
            => All.FirstOrDefault(f => f.FamilyId == familyId)
               ?? throw new ArgumentException($"Unknown Family ID: 0x{familyId:X4}");
    }

    /// <summary>High-level programmer for PSoC6 devices using CMSIS-DAP.</summary>
    public class Psoc6Programmer
    {
        private readonly CmsisDap.Device Device;                                // CMSIS-DAP device instance.
        private readonly PSoCclass PSoC;                                        // Target-specific constants instance.
        public SWJ_Interface Interface { get; set; } = SWJ_Interface.SWD;       // Selected SWJ interface (SWD or JTAG).
        uint SwjClockSpeed = 2000000;

        /// <summary>Constructs a new Psoc6Programmer.</summary>
        /// <param name="Device">CMSIS-DAP device instance.</param>
        /// <param name="PSoC">Target constants instance (e.g. new Psoc6A256K()).</param>
        /// <param name="Interface">Interface (SWD or JTAG).</param>
        /// <param name="SwjClockSpeed">SWD Clock Speed (Hz).</param>
        public Psoc6Programmer(CmsisDap.Device Device, PSoC6Family PSoC6Family, SWJ_Interface Interface = SWJ_Interface.SWD, uint SwjClockSpeed = 2000000)
        {
            this.Device = Device;                                               // Assign CMSIS-DAP device.
            PSoC = PSoC6Family.Create();
            this.Interface = Interface;                                         // Set interface type.
            this.SwjClockSpeed = SwjClockSpeed;
        }

        /// <summary>Writes a 32-bit value to a DAP register using device.Transfer.</summary>
        /// <param name="req">DAP register request (from DapReg.Write).</param>
        /// <param name="data">32-bit data to write.</param>
        private void WriteDAP(byte req, uint data)
        {
            byte[] response = Device.Transfer(0x00, (req, data));               // Perform transfer.
            byte expectedAck = (Interface == SWJ_Interface.SWD) ? (byte)0x01 : (byte)0x02;  // Determine expected ACK.
            if (response.Length < 3 || response[2] != expectedAck)
                throw new InvalidOperationException("WriteDAP failed: ACK mismatch or invalid response length.");
        }

        /// <summary>Reads a 32-bit value from a DAP register using device.Transfer.</summary>
        /// <param name="req">DAP register request (from DapReg.Read).</param>
        /// <returns>Output 32-bit data.</returns>
        private uint ReadDAP(byte req)
        {
            byte[] response = Device.Transfer(0x00, (req, null));    // Perform read transfer.
            byte expectedAck = (Interface == SWJ_Interface.SWD) ? (byte)0x01 : (byte)0x02;  // Determine expected ACK.
            if (response.Length < 7 || response[2] != expectedAck)
                throw new InvalidOperationException("ReadDAP failed: ACK mismatch or invalid response length.");
            return BitConverter.ToUInt32(response, 3);
        }

        /// <summary>Performs a combined write operation: writes the target address into TAR and writes data into DRW in one USB packet.</summary>
        /// <param name="addr">The target memory address.</param>
        /// <param name="data">The 32-bit data word to write.</param>
        public void WriteIO(uint addr, uint data)
        {
            // Send a single transfer command with two operations:
            // 1. Write to TAR with the provided address.
            // 2. Write to DRW with the provided data.
            byte[] response = Device.Transfer(0x00,
                (DapReg.Write.TAR, addr),
                (DapReg.Write.DRW, data));

            // Expected response structure (example):
            // Byte0: CMD (e.g., 0x05)
            // Byte1: DAP Index (e.g., 0x00)
            // Byte2: ACK for transfer (TAR write)
            if (response.Length < 4 || response[2] != (byte)((Interface == SWJ_Interface.SWD) ? 0x01 : 0x02))
                throw new InvalidOperationException("WriteIO failed: Invalid response or ACK.");
        }

        /// <summary>Performs a combined read operation: writes the target address to TAR, then reads from DRW (dummy read) 
        /// and from RDBUFF (final read) in one USB packet.</summary>
        /// <param name="addr">The target memory address to read from.</param>
        /// <returns>Output 32-bit word read from the target (from RDBUFF).</returns>
        public uint ReadIO(uint addr)
        {
            // Send a single transfer command with three operations:
            // 1. Write to TAR with the given address.
            // 2. Read from DRW (dummy read).
            // 3. Read from RDBUFF (final valid read).
            byte[] response = Device.Transfer(0x00,
                (DapReg.Write.TAR, addr),
                (DapReg.Read.DRW, null),
                (DapReg.Read.RDBUFF, null));

            // Expected response structure (example):
            // Byte0: CMD (e.g., 0x05)
            // Byte1: Count
            // Byte2: ACK for first transfer (TAR write)
            // Byte3-6: Data for DRW read (dummy value – discarded)
            // Byte7-10: Data for RDBUFF read (valid target data)
            if (response.Length < 13 || response[2] != (byte)((Interface == SWJ_Interface.SWD) ? 0x01 : 0x02))
                throw new InvalidOperationException("ReadIO failed: Invalid response or ACK.");

            return BitConverter.ToUInt32(response, 7); // Extract the 32-bit data from the final read.
        }

        /// <summary>Waits for the specified time unit and increments the timer.</summary>
        /// <param name="timer">Reference to cumulative timer variable.</param>
        /// <param name="unit">Time unit (e.g., TIME_1MS).</param>
        /// <returns>Updated timer value.</returns>
        public uint Wait(ref uint timer, timeUnit_e unit)
        {
            Thread.Sleep((int)unit);                                 // Sleep for specified duration.
            return ++timer;                                          // Increment and return timer.
        }

        /// <summary>Sends the SWJ sequence to switch from JTAG to SWD mode.</summary>
        /// <returns>True if the response indicates the SWJ sequence command.</returns>
        public void DAP_JTAGtoSWD()
        {
            byte[] resp = Device.SwjSeq(0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                                        0x9E, 0xE7,
                                        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                                        0x00);                  // Send SWJ sequence per CMSIS-DAP.
            if (resp.Length == 0 || resp[0] != CmsisDap.CMD_DAP_SWJ_SEQ)
                throw new InvalidOperationException("DAP_JTAGtoSWD failed: No or invalid response.");
        }


        /// <summary>Stub for switching from SWD to JTAG mode (implement as needed).</summary>
        private void DAP_SWDtoJTAG()
        {
            // Insert SWJ sequence for switching from SWD to JTAG.
        }

        /// <summary>Stub for toggling external reset (XRES) of the target.</summary>
        private void ToggleXRES()
        {
            Device.SwjPins(0x20, 0xA0, 0x00000000);
            Thread.Sleep(10);
            Device.SwjPins(0xA0, 0xA0, 0x00000000);
        }

        /// <summary>Stub for powering on or power-cycling the target.</summary>
        private void PowerOn()
        {
            // Implement power-cycle control.
        }

        /// <summary>Performs the DAP handshake by reading the target IDCODE.</summary>
        public void DAP_Handshake()
        {
            uint elapsed = 0;                                  // Cumulative timer.
            uint id = 0;                                       // IDCODE read from target.
            byte expectedAck = (Interface == SWJ_Interface.SWD) ? (byte)0x01 : (byte)0x02;  // Expected ACK value.
            uint targetID = (Interface == SWJ_Interface.SWD) ? 0x6BA02477u : 0x6BA00477u;// Expected target ID.
            do
            {
                Device.Connect();
                Device.TransferConfigure(0x00, 0x0040, 0x0000);
                Device.SwjClock(SwjClockSpeed);
                if (Interface == SWJ_Interface.SWD)
                    DAP_JTAGtoSWD();                         // Switch to SWD if required.
                else
                    DAP_SWDtoJTAG();                         // Switch to JTAG if required.
                Wait(ref elapsed, timeUnit_e.TIME_1MS);      // Wait between polls.
                try { id = ReadDAP(DapReg.Read.IDCODE); continue; }
                catch { }

                Wait(ref elapsed, timeUnit_e.TIME_1MS);      // Wait between polls.
            } while ((id != targetID) && (elapsed < 300));   // Loop until timeout.
            if (elapsed >= 300)
                throw new TimeoutException("DAP Handshake failed: Timeout.");
            if (id != targetID)
                throw new TimeoutException("DAP Handshake failed: Target ID not matched.");
        }

        /// <summary>Initializes the Debug Access Port (DAP).</summary>
        /// <param name="apNum">Access Port number. 0 – System AP; 1 – CM0+ AP; 2 – CM4 AP.</param>
        public void DAP_Init(byte apNum)
        {
            DAP_Handshake();                                        // Perform handshake.
            if (Interface == SWJ_Interface.JTAG)
            {
                WriteDAP(DapReg.Write.CTRLSTAT, 0x50000032);        // Write CTRLSTAT (JTAG).
            }
            else
            {
                WriteDAP(DapReg.Write.ABORT, 0x0000001E);           // Clear sticky errors (SWD).
                WriteDAP(DapReg.Write.CTRLSTAT, 0x50000000);        // Write CTRLSTAT (SWD).
            }
            WriteDAP(DapReg.Write.SELECT, (uint)(apNum << 24));     // Select AP.
            WriteDAP(DapReg.Write.CSW, 0x23000002);                 // Set CSW.
        }

        /// <summary>Scans APs (0-2) to locate one with valid CPU access.</summary>
        /// <returns>Byte array with valid APs found.</returns>
        public byte[] DAP_ScanAP()
        {
            uint data;
            List<byte> APlist = new List<byte>();
            for (byte i = 0; i < 3; i++)
            {
                try
                {
                    DAP_Init(i);                            // Try initializing AP i.
                    data = ReadIO(0xE000ED00);              // Read CPUID register.
                }
                catch { continue; }
                if ((data & 0xFF000000) == 0x41000000)
                {
                    APlist.Add(i);
                }
            }
            return APlist.ToArray();
        }

        /// <summary>Polls the IPC lock status until the expected state is reached.</summary>
        /// <param name="ipcId">IPC channel ID.</param>
        /// <param name="isLockExpected">True for lock acquired; false for released.</param>
        public void Ipc_PollLockStatus(byte ipcId, bool isLockExpected)
        {
            uint ipcAddr = (uint)(PSoC.IPC_STRUCT0 + PSoC.IPC_STRUCT_SIZE * ipcId);     // IPC base for channel.
            uint elapsed = 0;
            uint status = 0;
            do
            {
                status = ReadIO(ipcAddr + PSoC.IPC_STRUCT_LOCK_STATUS_OFFSET);          // Read IPC lock status.
                bool locked = (status & 0x80000000) != 0;                               // Determine if lock is acquired.
                if ((isLockExpected && locked) || (!isLockExpected && !locked))
                    return;
                Wait(ref elapsed, timeUnit_e.TIME_1MS);
            } while (elapsed < 1000);
            throw new TimeoutException($"IPC lock status timeout. Expected: {isLockExpected}, Last status: 0x{status:X8}");
        }

        /// <summary>Attempts to acquire an IPC channel by writing to its ACQUIRE register.</summary>
        /// <param name="ipcId">The IPC channel number.</param>
        public bool Ipc_Acquire(byte ipcId)
        {
            uint ipcAddr = (uint)(PSoC.IPC_STRUCT0 + PSoC.IPC_STRUCT_SIZE * ipcId);    // IPC base for channel.
            uint elapsed = 0, status = 0, data;
            do
            {
                try { data = ReadIO(PSoC.MEM_BASE_PPU4); } // Dummy read
                catch { DAP_Init(0); continue; }
                WriteIO(ipcAddr + 0x00, 0x01);                      // Write to ACQUIRE register.
                status = ReadIO(ipcAddr + 0x00);
                if ((status & 0x80000000) != 0)                       // Check acquire success bit.
                    return true;
                Wait(ref elapsed, timeUnit_e.TIME_1MS);
            } while (elapsed < 1000);
            return false;
        }

        /// <summary>Polls for the result of an SROM API call via its status register.</summary>
        /// <param name="addr">Address of the status register.</param>
        /// <returns>Output register value.</returns>
        public uint PollSromApiStatus(uint addr)
        {
            uint elapsed = 0, status = 0, data = 0;
            do
            {
                data = ReadIO(addr);
                status = data & PSoC.SROMAPI_STATUS_MSK;    // Extract status bits.
                if (status == PSoC.SROMAPI_STAT_SUCCESS)
                    return data;
                Wait(ref elapsed, timeUnit_e.TIME_1MS);
            } while (elapsed < 1000);
            throw new TimeoutException($"SROM API status polling failed. Last status: 0x{status:X8}");
        }

        /// <summary>Calls an SROM API command via IPC.</summary>
        /// <param name="callIdAndParams">SROM API opcode with parameters if needed.</param>
        /// <returns>Output result from API call.</returns>
        public uint CallSromApi(uint callIdAndParams)
        {
            // Use IPC for CM0+ (IpcId = 0) if using flash loader running on CM0+ core
            // Use IPC for CM4 (IpcId = 1) if using flash loader running on CM4 core
            // Use IPC for DAP (IpcId = 2) if using external debugger
            const byte ipcId = 2;                                                       // Use IPC channel 2.
            uint ipcAddr = (uint)(PSoC.IPC_STRUCT0 + PSoC.IPC_STRUCT_SIZE * ipcId);     // IPC base for channel.
            uint intrMaskInitial = 0;
            uint intrMaskDap = 1u << (16 + ipcId);
            bool isDataInRam = ((callIdAndParams & PSoC.SROMAPI_DATA_LOCATION_MSK) == 0);
            Ipc_Acquire(ipcId);

            if (isDataInRam)
                WriteIO(ipcAddr + PSoC.IPC_STRUCT_DATA_OFFSET, PSoC.SRAM_SCRATCH_ADDR);
            else
                WriteIO(ipcAddr + PSoC.IPC_STRUCT_DATA_OFFSET, callIdAndParams);
            intrMaskInitial = ReadIO(PSoC.IPC_INTR_STRUCT + PSoC.IPC_INTR_STRUCT_INTR_MASK_OFFSET);
            if (intrMaskInitial != intrMaskDap)
                WriteIO(PSoC.IPC_INTR_STRUCT + PSoC.IPC_INTR_STRUCT_INTR_MASK_OFFSET, intrMaskDap);
            WriteIO(ipcAddr + PSoC.IPC_STRUCT_NOTIFY_OFFSET, 1);
            Ipc_PollLockStatus(ipcId, false);

            uint dataOut = isDataInRam
                ? PollSromApiStatus(PSoC.SRAM_SCRATCH_ADDR)
                : PollSromApiStatus(ipcAddr + PSoC.IPC_STRUCT_DATA_OFFSET);
            if (intrMaskInitial != intrMaskDap)
                WriteIO(PSoC.IPC_INTR_STRUCT + PSoC.IPC_INTR_STRUCT_INTR_MASK_OFFSET, intrMaskInitial);
            return dataOut;
        }

        /// <summary>Acquires the target by resetting it, setting test mode, scanning for AP, and verifying PC.</summary>
        /// <param name="mode">Acquisition mode (ACQ_RESET or ACQ_POWER_CYCLE).</param>
        /// <param name="TestModeBit">If true, perform normal acquire; if false, perform alternative acquire.</param>
        /// <param name="AP">Access Port selection (AP_CM0, AP_CM4, AP_DAP or AP_AUTO for automatic scan).</param>
        public void Acquire(AcquireMode mode, bool TestModeBit, AP_e AP)
        {
            if (mode == AcquireMode.ACQ_RESET)
                ToggleXRES();                                       // Reset via XRES.
            else if (mode == AcquireMode.ACQ_POWER_CYCLE)
                PowerOn();                                          // Power cycle target.
            Thread.Sleep(100); // Allow 100ms to start up

            byte apNumber = (byte)AP;
            if (AP == AP_e.AP_AUTO)
            {
                byte[] AvailableAPs = DAP_ScanAP();  // Check for available APs, This includes Handshake (wait for device to boot after reset and DAP initialization
                apNumber = AvailableAPs[0];    //  Use first available AP
            }
            else
            {
                DAP_Init(apNumber);
            }


            if (TestModeBit)
            {
                //DAP_Init(apNumber);
                WriteIO(PSoC.SRSS_TST_MODE, PSoC.SRSS_TST_MODE_TEST_MODE_MSK);
                // if (!DAP_ScanAP(out byte apNum))
                //     return false;
                uint testMode = ReadIO(PSoC.SRSS_TST_MODE);
                if ((testMode & PSoC.SRSS_TST_MODE_TEST_MODE_MSK) == 0)
                    throw new InvalidOperationException("Test mode not set.");

                WriteIO(0xE000EDF4, 0x0000000F);                        // Select PC register.
                uint pc = ReadIO(0xE000EDF8);                         // Read PC register.
                if (((pc >= PSoC.MEM_BASE_ROM) && (pc < PSoC.MEM_BASE_ROM + PSoC.MEM_SIZE_ROM)) ||
                    ((pc >= PSoC.MEM_BASE_FLASH) && (pc < PSoC.MEM_BASE_FLASH + PSoC.MEM_SIZE_SFLASH)))
                    return;
                throw new InvalidOperationException("PC not in ROM or FLASH.");
            }
            else
            {

                /// <summary>Alternative acquisition method which halts CPU, loads infinite loop into SRAM, and resumes CPU.</summary>
                uint MemVTbase = apNumber == 2 ? PSoC.MEM_VTBASE_CM4 : PSoC.MEM_VTBASE_CM0;
                uint vtBase = ReadIO(MemVTbase) & 0xFFFF0000u;
                if (vtBase == 0 || vtBase == 0xFFFF0000u) return;

                uint resetAddress = ReadIO(vtBase + 4);
                if (resetAddress == 0) return;

                WriteIO(0xE000EDF0, 0xA05F0003);
                uint dhcsr = ReadIO(0xE000EDF0);
                if ((dhcsr & 0x03u) != 0x03u)
                    throw new InvalidOperationException("CPU not halted.");

                WriteIO(0xE0002000, 0x00000003);
                resetAddress = (resetAddress & 0x1FFFFFFCu) | 0xC0000001;
                WriteIO(0xE0002008, resetAddress);
                try
                {
                    WriteIO(0xE000ED0C, 0x05FA0004);
                }
                catch { }

                DAP_Init(apNumber);

                uint elapsed = 0;
                do
                {
                    dhcsr = ReadIO(0xE000EDF0);
                    if ((dhcsr & 3) == 3)
                        break;
                    Wait(ref elapsed, timeUnit_e.TIME_1MS);
                } while (elapsed < 110);
                if (elapsed >= 110)
                    throw new TimeoutException("CPU failed to halt after reset.");

                WriteIO(0x08000300, 0xE7FEE7FE);
                WriteIO(0xE000EDF8, 0x08000301);
                WriteIO(0xE000EDF4, 0x0001000F);
                WriteIO(0xE000EDF8, 0x0800FFF0);
                WriteIO(0xE000EDF4, 0x00010011);
                WriteIO(0xE000EDF4, 0x00000010);

                uint psrRegVal = ReadIO(0xE000EDF8);
                psrRegVal |= 0x01000000;
                WriteIO(0xE000EDF8, psrRegVal);
                WriteIO(0xE000EDF4, 0x00010010);
                WriteIO(0xE0002000, 0x00000002);
                WriteIO(0xE000EDF0, 0xA05F0001);
            }
        }

        /// <summary>Retrieves the Silicon ID by calling SROM API commands.</summary>
        /// <param name="fileID">A 4-byte array where fileID[0,1] are the silicon ID and fileID[2,3] are the family ID.</param>
        public void GetSiliconInfo(out UInt16 FamilyId, out UInt16 SiliconId, out byte RevisionId, out byte ProtectionState)
        {
            uint dataOut0 = CallSromApi(SromCodeWithParams(PSoC.SROMAPI_SILID_CODE, 0));
            uint dataOut1 = CallSromApi(SromCodeWithParams(PSoC.SROMAPI_SILID_CODE, 1));

            FamilyId = (UInt16)(dataOut0 & 0xFFFF);
            SiliconId = (UInt16)(dataOut1 & 0xFFFF);
            RevisionId = (byte)((dataOut0 >> 16) & 0xFF);
            ProtectionState = (byte)((dataOut1 >> 16) & 0x0F);
        }

        /// <summary>Helper: Combines SROM API opcode with a parameter (shifted left by 8 bits).</summary>
        /// <param name="opcode">SROM API opcode.</param>
        /// <param name="param">Parameter to embed.</param>
        /// <returns>Combined opcode and parameter.</returns>
        private uint SromCodeWithParams(uint opcode, uint param)
        {
            return opcode | (param << 8);    // Combine opcode and parameter.
        }

        /// <summary>Erases the application flash using the SROM API.</summary>
        /// <param name="StartAddr">The address to start erasing (Erasing will be row aligned).</param>
        /// <param name="EndAddr">The address to start erasing (Erasing will be row aligned).</param>
        public void EraseFlash(uint StartAddr, uint EndAddr)
        {
            uint subsectorSize = PSoC.ROW_SIZE * 8;
            uint sectorSize = PSoC.ROW_SIZE * 512;


            // Align addresses to row boundaries
            StartAddr &= ~(PSoC.ROW_SIZE - 1);
            EndAddr = (EndAddr + PSoC.ROW_SIZE - 1) & ~(PSoC.ROW_SIZE - 1);

            // If full erase is requested and aligned
            if (StartAddr == PSoC.MEM_BASE_FLASH && (EndAddr - StartAddr) >= PSoC.MEM_SIZE_FLASH)
            {
                CallSromApi(PSoC.SROMAPI_ERASEALL_CODE);
                return;
            }

            while (StartAddr < EndAddr)
            {
                uint remaining = EndAddr - StartAddr;
                uint opcode;

                if ((StartAddr % sectorSize == 0) && remaining >= sectorSize)
                {
                    opcode = PSoC.SROMAPI_ERASESECTOR_CODE;
                }
                else if ((StartAddr % subsectorSize == 0) && remaining >= subsectorSize)
                {
                    opcode = PSoC.SROMAPI_ERASESUBSECTOR_CODE;
                }
                else
                {
                    opcode = PSoC.SROMAPI_ERASEROW_CODE;
                }

                // Inline erase logic
                WriteIO(PSoC.SRAM_SCRATCH_ADDR, opcode);
                WriteIO(PSoC.SRAM_SCRATCH_ADDR + 0x04, StartAddr);
                CallSromApi(opcode);

                // Advance by opcode size
                StartAddr += opcode == PSoC.SROMAPI_ERASESECTOR_CODE ? sectorSize :
                             opcode == PSoC.SROMAPI_ERASESUBSECTOR_CODE ? subsectorSize :
                             PSoC.ROW_SIZE;
                UIExtension.Progress(StartAddr, EndAddr);
            }
        }

        /// <summary>Programs application flash row by row using the ProgramRow SROM API.</summary>
        /// <param name="flashData">Byte array containing the flash image.</param>
        /// <param name="flashStartAddress">Start Address of the flash image.</param>
        public void ProgramFlash(byte[] flashData, uint flashStartAddress)
        {
            uint totalRows = (uint)flashData.Length / PSoC.ROW_SIZE;

            for (uint rowID = 0; rowID < totalRows; rowID++)
            {
                uint flashStartAddr = flashStartAddress + rowID * PSoC.ROW_SIZE;
                int rowOffset = (int)(rowID * PSoC.ROW_SIZE);

                // Setup SROM parameters, use Program Row assuming rows are already erased
                WriteIO(PSoC.SRAM_SCRATCH_ADDR, PSoC.SROMAPI_PROGRAMROW_CODE);
                uint parameters = (6u << 0) | (1u << 8) | (0u << 16) | (0u << 24);
                WriteIO(PSoC.SRAM_SCRATCH_ADDR + 0x04, parameters);
                WriteIO(PSoC.SRAM_SCRATCH_ADDR + 0x08, flashStartAddr);
                WriteIO(PSoC.SRAM_SCRATCH_ADDR + 0x0C, PSoC.SRAM_SCRATCH_ADDR + 0x10);

                // Write 512-byte row using TransferBlock with offset
                TransferBlock(PSoC.SRAM_SCRATCH_ADDR + 0x10, flashData, rowOffset, (int)PSoC.ROW_SIZE);

                // Call the SROM API to program the row
                CallSromApi(PSoC.SROMAPI_PROGRAMROW_CODE);
                UIExtension.Progress(rowID, totalRows);
            }
        }

        public void TransferBlock(uint baseAddr, byte[] flashData, int offset, int length)
        {
            const int MAX_USB_BYTES = 64;
            const int HEADER_SIZE = 5; // DAP_TransferBlock Command | DAP Index | Transfer Count 2 byte | Transfer Request 
            const int WORD_SIZE = 4;
            const int MAX_WORDS = (MAX_USB_BYTES - HEADER_SIZE) / WORD_SIZE;

            int paddedLength = ((length + 3) / 4) * 4;

            for (int relOffset = 0; relOffset < paddedLength; relOffset += MAX_WORDS * WORD_SIZE)
            {
                int chunkSize = Math.Min(MAX_WORDS * WORD_SIZE, paddedLength - relOffset);
                int wordsThisChunk = chunkSize / WORD_SIZE;

                // Setup CSW + TAR for this chunk
                byte[] setupResp = Device.Transfer(0x00,
                    (DapReg.Write.CSW, 0x23000012),             // Set up auto increment for TAR
                    (DapReg.Write.TAR, baseAddr + (uint)relOffset));
                if (setupResp.Length < 4 || setupResp[2] != (byte)((Interface == SWJ_Interface.SWD) ? 0x01 : 0x02))
                    throw new InvalidOperationException($"TransferBlock CSW/TAR setup failed at offset {offset + relOffset}");

                byte[] payload = new byte[chunkSize];
                int available = length - relOffset;
                int copyLen = Math.Min(available, chunkSize);
                Buffer.BlockCopy(flashData, offset + relOffset, payload, 0, copyLen);

                byte[] response = Device.TransferBlock(0x0, DapReg.Write.DRW, payload);
                if (response.Length < 4 || response[3] != 0x01)
                    throw new InvalidOperationException($"TransferBlock write failed at offset {offset + relOffset}");
            }
        }


        /// <summary>Verifies application flash by comparing byte-by-byte.</summary>
        /// <param name="FlashData">Byte array of the expected flash image.</param>
        /// <param name="FlashSize">Total number of bytes in the flash image.</param>
        public void VerifyFlash(byte[] FlashData, uint FlashStartAddress)
        {
            byte[] chipData = new byte[PSoC.ROW_SIZE];
            uint totalRows = (uint)FlashData.Length / PSoC.ROW_SIZE;

            for (uint rowID = 0; rowID < totalRows; rowID++)
            {
                uint rowOffset = rowID * PSoC.ROW_SIZE;
                uint rowAddress = FlashStartAddress + rowOffset;

                for (uint i = 0; i < PSoC.ROW_SIZE; i += 4)
                {
                    uint word = ReadIO(rowAddress + i);
                    uint expected = (uint)FlashData[rowOffset + i] |
                                    (uint)FlashData[rowOffset + i + 1] << 8 |
                                    (uint)FlashData[rowOffset + i + 2] << 16 |
                                    (uint)FlashData[rowOffset + i + 3] << 24;

                    if (word != expected)
                        throw new InvalidOperationException($"Flash verification failed at address 0x{rowAddress + i:X8}");
                }
            }
        }

        /// <summary>Verifies the flash checksum using the SROM API.</summary>
        /// <param name="ExpectedChecksum">Expected checksum value.</param>
        /// <returns>True if the computed checksum matches the expected value.</returns>
        public void VerifyChecksum(uint ExpectedChecksum)
        {
            uint opCode = PSoC.SROMAPI_CHECKSUM_CODE |
                          (0u << 22) |
                          (1u << 21) |
                          ((0u & 0x1FFF) << 8);

            uint result = CallSromApi(opCode);
            uint checksum = result & PSoC.SROMAPI_CHECKSUM_DATA_MSK;

            if (checksum != ExpectedChecksum)
                throw new InvalidOperationException($"Checksum mismatch: expected 0x{ExpectedChecksum:X8}, got 0x{checksum:X8}");
        }

        /// <summary>Programs a generic flash region (e.g., AUXflash or SFlash) row by row using the WriteRow SROM API.</summary>
        /// <param name="FlashData">Byte array containing the flash image.</param>
        /// <param name="FlashSize">Size in bytes of the flash region.</param>
        /// <param name="BaseAddr">Base address of the target flash region.</param>
        /// <returns>True if all rows are programmed successfully.</returns>
        public void ProgramFlashGeneric(byte[] FlashData, uint FlashSize, uint BaseAddr)
        {
            uint totalRows = FlashSize / PSoC.ROW_SIZE;

            for (uint rowID = 0; rowID < totalRows; rowID++)
            {
                uint flashStartAddr = BaseAddr + rowID * PSoC.ROW_SIZE;
                WriteIO(PSoC.SRAM_SCRATCH_ADDR, PSoC.SROMAPI_WRITEROW_CODE);
                uint parameters = (6u << 0) | (1u << 8) | (0u << 16) | (0u << 24);
                WriteIO(PSoC.SRAM_SCRATCH_ADDR + 0x04, parameters);
                WriteIO(PSoC.SRAM_SCRATCH_ADDR + 0x08, flashStartAddr);
                WriteIO(PSoC.SRAM_SCRATCH_ADDR + 0x0C, PSoC.SRAM_SCRATCH_ADDR + 0x10);

                for (uint i = 0; i < PSoC.ROW_SIZE; i += 4)
                {
                    uint index = rowID * PSoC.ROW_SIZE + i;
                    uint dataWord = ((uint)FlashData[index + 3] << 24) |
                                    ((uint)FlashData[index + 2] << 16) |
                                    ((uint)FlashData[index + 1] << 8) |
                                    FlashData[index];
                    WriteIO(PSoC.SRAM_SCRATCH_ADDR + 0x10 + i, dataWord);
                }

                CallSromApi(PSoC.SROMAPI_WRITEROW_CODE);
            }
        }

        /// <summary>Verifies a generic flash region by reading back and comparing each row.</summary>
        /// <param name="FlashData">Byte array of the expected flash image.</param>
        /// <param name="FlashSize">Total number of bytes of the flash image.</param>
        /// <param name="BaseAddr">Base address of the flash region.</param>
        /// <returns>True if all rows match the expected data.</returns>
        public void VerifyFlashGeneric(byte[] FlashData, uint FlashSize, uint BaseAddr)
        {
            byte[] chipData = new byte[PSoC.ROW_SIZE];
            uint totalRows = FlashSize / PSoC.ROW_SIZE;

            for (uint rowID = 0; rowID < totalRows; rowID++)
            {
                uint rowAddress = BaseAddr + rowID * PSoC.ROW_SIZE;

                for (uint i = 0; i < PSoC.ROW_SIZE; i += 4)
                {
                    uint dataOut = ReadIO(rowAddress + i);
                    chipData[i + 0] = (byte)(dataOut & 0xFF);
                    chipData[i + 1] = (byte)((dataOut >> 8) & 0xFF);
                    chipData[i + 2] = (byte)((dataOut >> 16) & 0xFF);
                    chipData[i + 3] = (byte)((dataOut >> 24) & 0xFF);
                }

                for (uint i = 0; i < PSoC.ROW_SIZE; i++)
                {
                    if (chipData[i] != FlashData[rowID * PSoC.ROW_SIZE + i])
                        throw new InvalidOperationException($"VerifyFlashGeneric failed at row {rowID}, byte offset {i}");
                }
            }
        }
    }
}