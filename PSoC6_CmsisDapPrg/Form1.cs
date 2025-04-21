// **********************************************************************
//     PSoC6 CMSIS-DAP Programmer - Windows Forms UI Frontend
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
// - Provides a UI to interact with CMSIS-DAP compatible devices
// - Allows parsing and flashing of PSoC6 ELF or HEX firmware
// - Handles acquire, erase, program, and verify functions
//
//  Author: Rolf Nooteboom <rolf@nooteboom-elektronica.com>
//  Created: 2025
//
// **********************************************************************

using HidSharp;
using HidSharp.Utility;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Xml.Linq;
using static PSoC6_CmsisDapPrg.CmsisDap;


namespace PSoC6_CmsisDapPrg
{
    public partial class Form1 : Form
    {
        CyRecord_c? cyRecord = null;
        CmsisDap dap = new CmsisDap();
        private DeviceInfo? _selectedDevice = null;
        private CmsisDap.Device? _programmer = null;

        public Form1()
        {
            InitializeComponent();
            UIExtension.StatusTextBox = tbStatus;
            UIExtension.GroupBox = gbProgram;
            UIExtension.ProgressBar = pbMain;
            UIExtension.cbProgrammer = cbProgs;
            btScanUSB_Click(this, new EventArgs());
            firmwareFile = Config.Default.LastOpenedFirmware;
        }

        private void btScanUSB_Click(object sender, EventArgs e)
        {
            try
            {
                tbStatus.Clear();
                var devices = dap.Enumerate();
                if (devices.Count == 0)
                {
                    UIExtension.ToStatus("\r\nNo CMSIS-DAP device found.");
                    return;
                }
                cbProgs.DataSource = devices;
                UIExtension.ToStatus("\r\nDevices found:");
                foreach (var dev in devices)
                {
                    UIExtension.ToStatus($"\r\n{dev}");
                }
            }
            catch { }
        }

        string firmwareFile
        {
            get { return tbFirmware.Text; }
            set
            {
                if (value == null || !File.Exists(value))
                {
                    UIExtension.ToStatus("\r\nSelect valid firmware file first.");
                    return;
                }

                if (value.EndsWith(".elf"))
                {
                    try
                    {
                        List<ProgramSegment> ProgramSegments = ElfLoader.LoadSegments(value);
                        HexFileParser.GccElfToCyRecord(ProgramSegments, PSoCMap_c.Device_e.PSoC6, out cyRecord);
                        tbFirmware.Text = value;
                        Config.Default.LastOpenedFirmware = value;
                        Config.Default.Save();
                    }
                    catch (Exception ex)
                    {
                        UIExtension.ToStatus("\r\nError parsing ELF file: " + ex.Message);
                    }
                }
                else if (value.EndsWith(".hex"))
                {
                    try
                    {
                        string HexFile = File.ReadAllText(value);
                        HexFileParser.HexToCyRecord(HexFile, PSoCMap_c.Device_e.PSoC6, out cyRecord, out string strMSG);
                        tbFirmware.Text = value;
                        Config.Default.LastOpenedFirmware = value;
                        Config.Default.Save();
                    }
                    catch (Exception ex)
                    {
                        UIExtension.ToStatus("\r\nError parsing HEX file: " + ex.Message);
                    }
                }
                else UIExtension.ToStatus("\r\nInvalid input file!");
            }
        }

        private void btSelect_Click(object sender, EventArgs e)
        {
            ofd.Filter = "Binary files (.hex, .elf)|*.hex;*.elf|All files|*.*";
            if (ofd.ShowDialog(this) != DialogResult.OK) return;

            firmwareFile = ofd.FileName;
        }

        private CmsisDap.Device OpenSelectedProg()
        {
            if (!(UIExtension.GetSelectedProgrammer() is DeviceInfo selectedDevice))
                throw new Exception("No Valid Programmer Selected.");
            if (_programmer != null && selectedDevice == _selectedDevice) return _programmer;
            _selectedDevice = selectedDevice;
            _programmer = dap.Open(selectedDevice);
            if (_programmer == null)
                throw new Exception("No Valid Programmer Selected.");

            UIExtension.ToStatus($"\r\nProgrammer Capabilities: 0x{_programmer.Capabilities:X2}");
            UIExtension.ToStatus($"\r\nProgrammer VendorName: {_programmer.VendorName}");
            UIExtension.ToStatus($"\r\nProgrammer ProductName: {_programmer.ProductName}");
            UIExtension.ToStatus($"\r\nProgrammer SerialNumber: {_programmer.SerialNumber}");
            UIExtension.ToStatus($"\r\nProgrammer ProtocolVersion: {_programmer.ProtocolVersion}");
            UIExtension.ToStatus($"\r\nProgrammer TargetDeviceVendor: {_programmer.TargetDeviceVendor}");
            UIExtension.ToStatus($"\r\nProgrammer TargetDeviceName: {_programmer.TargetDeviceName}");
            UIExtension.ToStatus($"\r\nProgrammer TargetBoardVendor: {_programmer.TargetBoardVendor}");
            UIExtension.ToStatus($"\r\nProgrammer TargetBoardName: {_programmer.TargetBoardName}");
            UIExtension.ToStatus($"\r\nProgrammer FirmwareVersion: {_programmer.FirmwareVersion}");

            return _programmer;
        }

        void Execute(string name, Action action)
        {
            Task.Run(() =>
            {
                try
                {
                    DateTime startAction = DateTime.Now;
                    UIExtension.ToStatus($"\r\n\r\nStart of {name.ToLower()}: {startAction:G}");
                    UIExtension.Progress(0, 100);
                    action();
                    UIExtension.ToStatus($"\r\n{name} successfully.");

                    TimeSpan elapsed = DateTime.Now - startAction;
                    UIExtension.ToStatus($"\r\nTotal time: {elapsed.Seconds}.{elapsed.Milliseconds:D3} seconds");
                }
                catch (Exception ex)
                {
                    UIExtension.ToStatus($"\r\n{name} FAILED: {ex.Message}");
                }
                UIExtension.Progress(0, 0);
            });
        }

        private void btProgram_Click(object sender, EventArgs e)
        {
            if (cyRecord == null)
            {
                UIExtension.ToStatus("\r\nNo Valid File Selected.");
                return;
            }
            
            Execute("Programming", () =>
            {
                Program(cyRecord.FlashBlocks![0].bytes!.ToArray(), cyRecord.FlashBlocks[0].firstAddress);
            });
        }

        /// <summary>
        /// Connects to the target device, performs full flash erase,
        /// and programs the main application image to flash memory.
        /// </summary>
        /// <param name="FlashImage">The binary data to program.</param>
        /// <param name="FlashStartAddress">The start address for programming.</param>
        void Program(byte[] FlashImage, uint FlashStartAddress)
        {
            var Device = OpenSelectedProg();
            // -----------------------------
            // Connect / Acquire the target
            // -----------------------------
            UIExtension.ToStatus("\r\nAcquiring target...");

            Psoc6Programmer Programmer = new Psoc6Programmer(Device!, PSoC6Family.PSOC6ABLE2, SWJ_Interface.SWD, 4000000);
            Programmer.Acquire(AcquireMode.ACQ_RESET, false, AP_e.AP_CM4);

            UIExtension.ToStatus("\r\nTarget acquired successfully.");

            // -----------------------------
            // Erase flash
            // -----------------------------
            UIExtension.ToStatus("\r\nErasing flash...");

            Programmer.EraseFlash(0x10000000, 0x100DFFFF);

            // -----------------------------
            // Program flash
            // -----------------------------
            UIExtension.ToStatus("\r\nProgramming flash...");

            Programmer.ProgramFlash(FlashImage, FlashStartAddress);
        }

        private void btErase_Click(object sender, EventArgs e)
        {
            Execute("Erasing", () =>
            {
                Erase(0x1000_0000, 0x100D_FFFF);
            });
        }

        /// <summary>
        /// Acquires the target device and erases flash memory
        /// between specified address range.
        /// </summary>
        /// <param name="StartAddress">The start address of the erase range.</param>
        /// <param name="EndAddress">The end address of the erase range.</param>
        void Erase(uint StartAddress, uint EndAddress)
        {
            var Device = OpenSelectedProg();
            // -----------------------------
            // Connect / Acquire the target
            // -----------------------------
            UIExtension.ToStatus("\r\nAcquiring target...");

            Psoc6Programmer Programmer = new Psoc6Programmer(Device!, PSoC6Family.PSOC6ABLE2, SWJ_Interface.SWD, 4000000);
            Programmer.Acquire(AcquireMode.ACQ_RESET, false, AP_e.AP_CM4);

            UIExtension.ToStatus("\r\nTarget acquired successfully.");

            // -----------------------------
            // Erase flash
            // -----------------------------
            UIExtension.ToStatus("\r\nErasing flash...");

            Programmer.EraseFlash(StartAddress, EndAddress);
        }

        private void btVerify_Click(object sender, EventArgs e)
        {
            if (cyRecord == null)
            {
                UIExtension.ToStatus("\r\nNo Valid File Selected.");
                return;
            }

            Execute("Verifying", () =>
            {
                Verify(cyRecord.FlashBlocks![0].bytes!.ToArray(), cyRecord.FlashBlocks[0].firstAddress);
            });
        }

        /// <summary>
        /// Acquires the target and verifies that flash contents match
        /// the original firmware image.
        /// </summary>
        /// <param name="FlashImage">Expected contents of the flash memory.</param>
        /// <param name="FlashStartAddress">The address where verification begins.</param>
        void Verify(byte[] FlashImage, uint FlashStartAddress)
        {
            var Device = OpenSelectedProg();
            UIExtension.ToStatus("\r\nAcquiring target...");
            // -----------------------------
            // Connect / Acquire the target
            // -----------------------------
            UIExtension.ToStatus("\r\nAcquiring target...");

            Psoc6Programmer Programmer = new Psoc6Programmer(Device!, PSoC6Family.PSOC6ABLE2, SWJ_Interface.SWD, 4000000);
            Programmer.Acquire(AcquireMode.ACQ_RESET, false, AP_e.AP_CM4);

            UIExtension.ToStatus("\r\nTarget acquired successfully.");

            // -----------------------------
            // Verify flash
            // -----------------------------
            UIExtension.ToStatus("\r\nVerifying flash...");

            Programmer.VerifyFlash(cyRecord!.FlashBlocks![0].bytes!.ToArray(), cyRecord.FlashBlocks[0].firstAddress);
        }

        private void btInfo_Click(object sender, EventArgs e)
        {
            Execute("Get Info", () =>
            {
                GetInfo();
            });
        }

        /// <summary>
        /// Acquires the target and identifies silicon using SROM Call.
        /// </summary>
        void GetInfo()
        {
            var Device = OpenSelectedProg();
            // -----------------------------
            // Connect / Acquire the target
            // -----------------------------
            UIExtension.ToStatus("\r\nAcquiring target...");

            Psoc6Programmer Programmer = new Psoc6Programmer(Device!, PSoC6Family.PSOC6ABLE2, SWJ_Interface.SWD, 4000000);
            Programmer.Acquire(AcquireMode.ACQ_RESET, false, AP_e.AP_CM4);

            UIExtension.ToStatus("\r\nTarget acquired successfully.");

            // -----------------------------
            // Verify flash
            // -----------------------------
            UIExtension.ToStatus("\r\nReading PSoC6 Info...");

            Programmer.GetSiliconInfo(out ushort FamilyId, out ushort SiliconId, out byte RevisionId, out byte ProtectionState);
            string familyName = PSoC6Family.FromFamilyId(FamilyId).Name;
            string protection = Enum.IsDefined(typeof(ProtectionState_e), ProtectionState)
                ? ((ProtectionState_e)ProtectionState).ToString()
                : $"UNKNOWN (0x{ProtectionState:X2})";

            string info = "\r\n" + $"""
                ------------------------------------------------------
                 PSoC6 Device Info
                ------------------------------------------------------
                 Family ID     : 0x{FamilyId:X4}  ({familyName})
                 Silicon ID    : 0x{SiliconId:X4}
                 Revision ID   : 0x{RevisionId:X2}
                 Protection    : 0x{ProtectionState:X2}    ({protection})
                ------------------------------------------------------
                """;

            UIExtension.ToStatus(info);
        }

        // Used Wireshark filter: usb.bus_id == 2 && usb.device_address == 1 && (usb.setup.bRequest == 0x09 || usb.endpoint_address.number == 3  || usb.transfer_type == 0x02) && (usbhid.setup.bRequest != 0x19)
        // Adapt usb.bus_id == 2 && usb.device_address to your programmer
        private void btAnalyzeFile_Click(object sender, EventArgs e)
        {
            ofd.Filter = "Dump files (.dump, .dmp, .txt, .cap)|*.dump;*.dmp;*.txt;*.cap|All files|*.*";
            if (ofd.ShowDialog(this) != DialogResult.OK) return;

            var lines = File.ReadLines(ofd.FileName)
                            .Where(line => !string.IsNullOrWhiteSpace(line))
                            .Take(1000)
                            .ToList();
            int row = 0;
            foreach (var line in lines)
            {
                string hexStr = line.Trim().ToLower();
                byte[] data;
                try
                {
                    data = HexStringToByteArray(hexStr);
                }
                catch (Exception ex)
                {
                    UIExtension.ToStatus($"\r\n// Row {row}: {hexStr}");
                    UIExtension.ToStatus($"\r\n// Error parsing hex: {ex.Message}");
                    row++;
                    continue;
                }
                if ((row & 1) > 0) // Assume we have the resonse every next line
                {
                    UIExtension.ToStatus($"\r\n//>Row {row}: RAW: \"{hexStr}\"");
                    row++;
                    continue;
                }
                var (expectedLength, cmdString) = ParseCommand(data);
                string truncatedHex = expectedLength > 0
                    ? "0x" + BitConverter.ToString(data, 0, expectedLength).Replace("-", "")
                    : "";
                UIExtension.ToStatus($"\r\n// Row {row}: RAW: \"{truncatedHex}\"");
                UIExtension.ToStatus("\r\n" + cmdString);

                row++;
            }
        }

        /// <summary>
        /// Parses the raw DAP response (up to 64 bytes) and returns both the expected length and the command string.
        /// </summary>
        /// <param name="data">The full USB response byte array.</param>
        /// <returns>A tuple with (expectedLength, commandText).</returns>
        private static (int expectedLength, string commandText) ParseCommand(byte[] data)
        {
            if (data.Length < 1)
                return (0, "// No data");
            int expected = 0;
            string commandText = "";
            byte cmd = data[0];
            switch (cmd)
            {
                case CMD_DAP_INFO:
                    expected = (data.Length >= 2 ? 2 : data.Length);
                    commandText = data.Length >= 2 ? $"Device.GetInfo(0x{data[1]:X2});" : "// Invalid Info";
                    break;
                case CMD_DAP_LED:
                    expected = (data.Length >= 3 ? 3 : data.Length);
                    commandText = data.Length >= 3 ? $"Device.SetLed(0x{data[1]:X2}, 0x{data[2]:X2});" : "// Invalid LED";
                    break;
                case CMD_DAP_CONNECT:
                    expected = (data.Length >= 2 ? 2 : data.Length);
                    commandText = data.Length >= 2 ? $"Device.Connect(0x{data[1]:X2});" : "// Invalid Connect";
                    break;
                case CMD_DAP_DISCONNECT:
                    expected = 1;
                    commandText = "Device.Disconnect();";
                    break;
                case CMD_DAP_TFER_CONFIGURE:
                    if (data.Length >= 6)
                    {
                        expected = 6;
                        byte idle = data[1];
                        ushort wait = (ushort)(data[2] | (data[3] << 8));
                        ushort match = (ushort)(data[4] | (data[5] << 8));
                        commandText = $"Device.TransferConfigure(0x{idle:X2}, 0x{wait:X4}, 0x{match:X4});";
                    }
                    else { expected = data.Length; commandText = "// Invalid TFER_CONFIGURE"; }
                    break;
                // Layout: [0]=0x05, [1]=DAP index, [2]=Transfer Count, [3]=Transfer Request, Transfer Data (WORD)
                case CmsisDap.CMD_DAP_TFER:
                    if (data.Length >= 3)
                    {
                        byte dapIndex = data[1];
                        byte transferCount = data[2];
                        int offset = 3;
                        List<string> args = new();

                        for (int i = 0; i < transferCount; i++)
                        {
                            if (offset >= data.Length) break;

                            byte req = data[offset++];
                            string reqName = DapReg.GetName(req); // 🔍 Lookup symbolic name

                            if (CmsisDap.Device.RequiresTransferData(req))
                            {
                                if (offset + 4 > data.Length) break;
                                uint value = BitConverter.ToUInt32(data, offset);
                                offset += 4;
                                args.Add($"({reqName}, 0x{value:X8})");
                            }
                            else
                            {
                                args.Add($"({reqName}, null)");
                            }
                        }

                        expected = offset;
                        string transfers = string.Join(", ", args);
                        commandText = $"Device.Transfer(0x{dapIndex:X2}, {transfers});";
                    }
                    else
                    {
                        expected = data.Length;
                        commandText = "// Invalid TFER";
                    }
                    break;


                case CMD_DAP_TFER_BLOCK:
                    // Layout: [0]=0x06, [1-2]=Count (LE), [3]=req, then (Count*4) bytes.
                    if (data.Length >= 4)
                    {
                        ushort count = (ushort)(data[1] | (data[2] << 8));
                        expected = 4 + (count * 4);
                        byte req = data[3];
                        var blockData = data.Skip(4).Take(count * 4).ToArray();
                        string blockStr = (blockData.Length > 0)
                            ? string.Join(", ", blockData.Select(b => $"0x{b:X2}"))
                            : "";
                        commandText = $"Device.TransferBlock(0x{count:X4}, 0x{req:X2}"
                            + (blockData.Length > 0 ? $", {blockStr}" : "") + ");";
                    }
                    else { expected = data.Length; commandText = "// Invalid TFER_BLOCK"; }
                    break;
                case CMD_DAP_TFER_ABORT:
                    expected = 1;
                    commandText = "Device.TransferAbort();";
                    break;
                case CMD_DAP_WRITE_ABORT:
                    expected = 1;
                    commandText = "Device.WriteAbort();";
                    break;
                case CMD_DAP_DELAY:
                    expected = (data.Length >= 2 ? 2 : data.Length);
                    commandText = data.Length >= 2 ? $"Device.Delay(0x{data[1]:X2});" : "// Invalid Delay";
                    break;
                case CMD_DAP_RESET_TARGET:
                    expected = 1;
                    commandText = "Device.ResetTarget();";
                    break;
                case CMD_DAP_SWJ_PINS:
                    if (data.Length >= 7)
                    {
                        expected = 7;
                        byte output = data[1];
                        byte select = data[2];
                        uint timeout = (uint)(data[3] | (data[4] << 8) | (data[5] << 16) | (data[6] << 24));
                        commandText = $"Device.SwjPins(0x{output:X2}, 0x{select:X2}, 0x{timeout:X8});";
                    }
                    else { expected = data.Length; commandText = "// Invalid SWJ_PINS"; }
                    break;
                case CMD_DAP_SWJ_CLOCK:
                    if (data.Length >= 5)
                    {
                        expected = 5;
                        uint hz = (uint)(data[1] | (data[2] << 8) | (data[3] << 16) | (data[4] << 24));
                        commandText = $"Device.SwjClock({hz});";
                    }
                    else { expected = data.Length; commandText = "// Invalid SWJ_CLOCK"; }
                    break;
                case CMD_DAP_SWJ_SEQ:
                    if (data.Length >= 2)
                    {
                        byte bitCount = data[1];
                        int byteCount = (bitCount + 7) / 8;
                        expected = 2 + byteCount;
                        var seq = data.Skip(2).Take(byteCount).ToArray();
                        string seqStr = (seq.Length > 0) ? string.Join(", ", seq.Select(b => $"0x{b:X2}")) : "";
                        commandText = $"Device.SwjSeq(0x{bitCount:X2}" + (seq.Length > 0 ? $", {seqStr}" : "") + ");";
                    }
                    else { expected = data.Length; commandText = "// Invalid SWJ_SEQ"; }
                    break;
                case CMD_DAP_SWD_CONFIGURE:
                    expected = (data.Length >= 3 ? 3 : data.Length);
                    commandText = data.Length >= 3 ? $"Device.SwdConfigure(0x{data[1]:X2}, 0x{data[2]:X2});" : "// Invalid SWD_CONFIGURE";
                    break;
                case CMD_DAP_JTAG_SEQ:
                    if (data.Length >= 2)
                    {
                        byte len = data[1];
                        expected = 2 + len;
                        var seq = data.Skip(2).Take(len).ToArray();
                        string seqStr = (seq.Length > 0)
                            ? string.Join(", ", seq.Select(b => $"0x{b:X2}"))
                            : "";
                        commandText = $"Device.JtagSeq({seqStr});";
                    }
                    else { expected = data.Length; commandText = "// Invalid JTAG_SEQ"; }
                    break;
                case CMD_DAP_JTAG_CONFIGURE:
                    expected = (data.Length >= 3 ? 3 : data.Length);
                    commandText = data.Length >= 3 ? $"Device.JtagConfigure(0x{data[1]:X2}, 0x{data[2]:X2});" : "// Invalid JTAG_CONFIGURE";
                    break;
                case CMD_DAP_JTAG_IDCODE:
                    expected = 1;
                    commandText = "Device.JtagIdCode();";
                    break;
                default:
                    expected = data.Length;
                    commandText = $"// Unknown command: 0x{cmd:X2}";
                    break;
            }
            return (expected, commandText);
        }

        static byte[] HexStringToByteArray(string hex)
        {
            if (hex.Length % 2 != 0)
                throw new ArgumentException("Invalid hex string length.");
            byte[] bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            return bytes;
        }
    }

    /// <summary>
    /// Extension methods UI interface.
    /// </summary>
    public static class UIExtension
    {
        public static TextBox? StatusTextBox { get; set; }
        public static ProgressBar? ProgressBar { get; set; }
        public static GroupBox? GroupBox { get; set; }
        public static ComboBox cbProgrammer { get; set; }

        public static void ThreadSafe(this Control control, MethodInvoker action)
        {
            if (control.InvokeRequired)
                control.Invoke(action);
            else
                action();
        }
        public static void ToStatus(string line)
        {
            if (StatusTextBox == null) Console.Write(line);
            else
            {
                StatusTextBox.ThreadSafe(() =>
                {
                    StatusTextBox.AppendText(line);
                });
            }
        }
        public static void ToStatus(string id, byte[] response)
        {
            string hexString = "0x" + BitConverter.ToString(response).Replace("-", "");
            ToStatus("\r\n" + id + hexString);
        }
        public static void Progress(uint value, uint max)
        {
            if (max > 0xFFFFFF)
            {
                max >>= 8;
                value >>= 8;
            }
            Progress((int)value, (int)max);
        }
        public static void Progress(int value, int max)
        {
            if (ProgressBar == null) return;
            ProgressBar.ThreadSafe(() =>
            {
                if (max != 0)
                {
                    ProgressBar.Maximum = max;
                    ProgressBar.Value = value;
                    GroupBox!.Enabled = false;
                }
                else
                {
                    ProgressBar.Maximum = 1;
                    ProgressBar.Value = 1;
                    GroupBox!.Enabled = true;
                }

            });
        }
        public static object? GetSelectedProgrammer()
        {
            object? selectedProg = null;
            cbProgrammer.ThreadSafe(() =>
            {
                selectedProg = cbProgrammer.SelectedItem;
            });
            return selectedProg;

        }
    }
    
}
