// **********************************************************************
//     PSoC6 CMSIS-DAP Programmer - Device Communication Handler
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
// - Provides low-level communication with CMSIS-DAP HID devices
// - Sends DAP commands and parses basic DAP_INFO queries
// - Extracts packet capabilities and device identity details
//
//  Author: Rolf Nooteboom <rolf@nooteboom-elektronica.com>
//  Created: 2025
//
// **********************************************************************

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HidSharp;

namespace PSoC6_CmsisDapPrg
{
    public static class DapReg
    {
        public const byte MATCH = 1 << 4;
        public const byte MASK = 1 << 5;
        public const byte TIMESTAMP = 1 << 7;

        public static class Read
        {
            public const byte IDCODE = 0x02;    // DP, Read, Addr = 0x00
            public const byte CTRLSTAT = 0x06;  // DP, Read, Addr = 0x04
            public const byte RDBUFF = 0x0E;    // DP, Read, Addr = 0x0C

            public const byte CSW = 0x03;       // AP, Read, Addr = 0x00
            public const byte TAR = 0x07;       // AP, Read, Addr = 0x04
            public const byte DRW = 0x0F;       // AP, Read, Addr = 0x0C
        }

        public static class Write
        {
            public const byte ABORT = 0x00;     // DP, Write, Addr = 0x00
            public const byte CTRLSTAT = 0x04;  // DP, Write, Addr = 0x04
            public const byte SELECT = 0x08;    // DP, Write, Addr = 0x08

            public const byte CSW = 0x01;       // AP, Write, Addr = 0x00
            public const byte TAR = 0x05;       // AP, Write, Addr = 0x04
            public const byte DRW = 0x0D;       // AP, Write, Addr = 0x0C
        }
        public static readonly Dictionary<byte, string> Names = new()
        {
            // DP Read
            { Read.IDCODE,      "DapReg.Read.IDCODE" },
            { Read.CTRLSTAT,    "DapReg.Read.CTRLSTAT" },
            { Read.RDBUFF,      "DapReg.Read.RDBUFF" },
            { Read.CSW,         "DapReg.Read.CSW" },
            { Read.TAR,         "DapReg.Read.TAR" },
            { Read.DRW,         "DapReg.Read.DRW" },
            { Write.ABORT,      "DapReg.Write.ABORT" },
            { Write.CTRLSTAT,   "DapReg.Write.CTRLSTAT" },
            { Write.SELECT,     "DapReg.Write.SELECT" },
            { Write.CSW,        "DapReg.Write.CSW" },
            { Write.TAR,        "DapReg.Write.TAR" },
            { Write.DRW,        "DapReg.Write.DRW" }
        };
        public static string GetName(byte req)
        {
            byte baseCode = (byte)(req & 0x0F);
            string name = Names.TryGetValue(baseCode, out var baseName) ? baseName : $"0x{req:X2}";

            if ((req & MATCH) != 0) name += " | MATCH";
            if ((req & MASK) != 0) name += " | MASK";
            if ((req & TIMESTAMP) != 0) name += " | TIMESTAMP";

            return name;
        }
    }

    public class CmsisDap
    {
        public class DeviceInfo
        {
            public string Path { get; set; } = string.Empty;
            public string? Product { get; set; }
            public string? Manufacturer { get; set; }
            public int VendorId { get; set; }
            public int ProductId { get; set; }

            public override string ToString()
            {
                return $"{Manufacturer} {Product} (VID: 0x{VendorId:X4}, PID: 0x{ProductId:X4})";
            }
        }

        public class Device : IDisposable
        {
            private HidStream _stream;
            public bool IsConnected => _stream != null && _stream.CanWrite;
            public int PacketSize { get; private set; } = 64;
            public int PacketCount { get; private set; } = 1;
            public byte Capabilities { get; private set; }
            public string FirmwareVersion { get; private set; }
            public string VendorName { get; private set; }
            public string ProductName { get; private set; }
            public string SerialNumber { get; private set; }
            public string ProtocolVersion { get; private set; }
            public string TargetDeviceVendor { get; private set; }
            public string TargetDeviceName { get; private set; }
            public string TargetBoardVendor { get; private set; }
            public string TargetBoardName { get; private set; }

            /// <summary>
            /// Initializes a new CMSIS-DAP device by opening the HID stream and querying device info.
            /// </summary>
            /// <param name="device">The HID device representing a CMSIS-DAP programmer.</param>
            /// <exception cref="IOException">Thrown if the device stream could not be opened.</exception>
            public Device(HidDevice device)
            {
                if (!device.TryOpen(out _stream))
                {
                    throw new IOException("Failed to open device stream.");
                }

                // Initialize device information.
                Capabilities = SendCommand(new byte[] { CMD_DAP_INFO, INFO_ID_CAPABILITIES }).ElementAtOrDefault(2);
                VendorName = GetDeviceInfoString(INFO_ID_VENDOR_NAME);
                ProductName = GetDeviceInfoString(INFO_ID_PRODUCT_NAME);
                SerialNumber = GetDeviceInfoString(INFO_ID_SERIAL_NUMBER);
                ProtocolVersion = GetDeviceInfoString(INFO_ID_PROTOCOL_VERSION);
                TargetDeviceVendor = GetDeviceInfoString(INFO_ID_TARGETDEV_VENDOR);
                TargetDeviceName = GetDeviceInfoString(INFO_ID_TARGETDEV_NAME);
                TargetBoardVendor = GetDeviceInfoString(INFO_ID_TARGETBOARD_VENDOR);
                TargetBoardName = GetDeviceInfoString(INFO_ID_TARGETBOARD_NAME);
                FirmwareVersion = GetDeviceInfoString(INFO_ID_PRODUCT_FW_VERSION);

                PacketCount = SendCommand(new byte[] { CMD_DAP_INFO, INFO_ID_PACKET_COUNT }).ElementAtOrDefault(2);
                var size = SendCommand(new byte[] { CMD_DAP_INFO, INFO_ID_PACKET_SIZE });
                if (size.Length >= 4)
                {
                    PacketSize = size[2] | (size[3] << 8);
                }
            }

            private string GetDeviceInfoString(byte infoId) => (SendCommand(new byte[] { CMD_DAP_INFO, infoId }) is byte[] response && response.Length > 2)
                ? Encoding.ASCII.GetString(response.Skip(2).ToArray()).TrimEnd('\0')
                : string.Empty;

            private readonly byte[] _txBuffer = new byte[65];
            private readonly byte[] _rxBuffer = new byte[65];

            /// <summary>
            /// Sends a DAP command to the CMSIS-DAP device and returns the response (excluding report ID).
            /// </summary>
            /// <param name="payload">Command payload bytes.</param>
            /// <returns>Device response without the report ID.</returns>
            public byte[] SendCommand(byte[] payload)
            {
                _txBuffer[0] = 0x00; // HID Report ID
                int len = Math.Min(payload.Length, 64);
                Buffer.BlockCopy(payload, 0, _txBuffer, 1, len);
                _stream.Write(_txBuffer, 0, _txBuffer.Length);

                int read = _stream.Read(_rxBuffer, 0, _rxBuffer.Length);
                if (read < 2)
                    throw new IOException("Invalid response");

                byte[] result = new byte[read - 1];
                Buffer.BlockCopy(_rxBuffer, 1, result, 0, read - 1);
                return result;
            }


            /// <summary>
            /// Sends a Set LED command to the device.
            /// </summary>
            /// <param name="ledId">The LED identifier (e.g. LED_ID_CONNECT or LED_ID_RUN).</param>
            /// <param name="on">The LED state; nonzero (typically 1) turns the LED on, 0 turns it off.</param>
            /// <returns>The device response as a byte array.</returns>
            public byte[] SetLed(byte ledId, byte on) => SendCommand(CmsisDap.SetLed(ledId, on));

            /// <summary>
            /// Sends a Connect command to the device to establish a connection to the target.
            /// </summary>
            /// <param name="mode">The connection mode. By default, this is set to CONNECT_SWD.</param>
            /// <returns>The device response as a byte array.</returns>
            public byte[] Connect(ConnectMode mode = ConnectMode.SWD) => SendCommand(CmsisDap.Connect(mode));

            /// <summary>
            /// Sends a Disconnect command to terminate the connection to the target device.
            /// </summary>
            /// <returns>The device response as a byte array.</returns>
            public byte[] Disconnect() => SendCommand(CmsisDap.Disconnect());

            /// <summary>
            /// Sends a Reset Target command to reset the connected target device.
            /// </summary>
            /// <returns>The device response as a byte array.</returns>
            public byte[] ResetTarget() => SendCommand(CmsisDap.ResetTarget());

            /// <summary>
            /// Sends a Get Info command to retrieve information from the target device.
            /// </summary>
            /// <param name="infoId">
            /// The information identifier. Examples include:
            ///  - INFO_ID_CAPS for capabilities,
            ///  - INFO_ID_VID for vendor ID,
            ///  - INFO_ID_PID for product ID, etc.
            /// </param>
            /// <returns>A byte array containing the requested information from the device.</returns>
            public byte[] GetInfo(byte infoId) => SendCommand(CmsisDap.GetInfo(infoId));

            /// <summary>
            /// Sends a SWJ Clock command to set the Serial Wire/JTAG clock frequency.
            /// </summary>
            /// <param name="hz">The clock frequency in Hertz.</param>
            /// <returns>The device response as a byte array.</returns>
            public byte[] SwjClock(uint hz) => SendCommand(CmsisDap.SwjClock(hz));

            /// <summary>
            /// Sends a SWJ Pins command to control the state of the Serial Wire/JTAG pins.
            /// </summary>
            /// <param name="output">The bitmask specifying the output state of the pins.</param>
            /// <param name="select">The bitmask specifying which pins to update.</param>
            /// <param name="timeout">A 32-bit timeout value (little-endian) for the operation.</param>
            /// <returns>The device response as a byte array.</returns>
            public byte[] SwjPins(byte output, byte select, uint timeout) => SendCommand(CmsisDap.SwjPins(output, select, timeout));

            public static bool RequiresTransferData(byte req) => ((req ^ 0x02) & 0x12) > 0;
            /// <summary>
            /// Sends a DAP_Transfer command to the device.
            /// </summary>
            /// <param name="dapIndex">The DAP index (ignored in SWD mode).</param>
            /// <param name="transferCount">The number of transfers (1–255).</param>
            /// <param name="req">The transfer request byte.</param>
            /// <param name="payload">Optional transfer data (a WORD per transfer if required).</param>
            /// <returns>The device response as a byte array.</returns>
            /// 
            public byte[] Transfer(byte dapIndex, params (byte req, uint? data)[] transfers) =>
                SendCommand(CmsisDap.Transfer(dapIndex, transfers));
            /// <summary>
            /// Sends a Transfer Block command (CMD_DAP_TFER_BLOCK) to the connected device.
            /// </summary>
            /// <param name="count">The number of transfers to perform in the block (as a 16-bit little-endian value).</param>
            /// <param name="req"> The transfer request byte indicating control flags (e.g. RnW, APnDP, register number). </param>
            /// <param name="payload">Optional additional data bytes (used in write block transfers).</param>
            /// <returns>The response from the device as a byte array.</returns>
            public byte[] TransferBlock(byte dapIndex, byte req, params byte[] payload) => SendCommand(CmsisDap.TransferBlock(dapIndex, req, payload));

            /// <summary>
            /// Sends a Transfer Configure command to set parameters for subsequent data transfers.
            /// </summary>
            /// <param name="idle">The idle delay value.</param>
            /// <param name="wait">The wait time between transfers (16-bit value).</param>
            /// <param name="match">The match value for synchronization (16-bit value).</param>
            /// <returns>The device response as a byte array.</returns>
            public byte[] TransferConfigure(byte idle, ushort wait, ushort match) => SendCommand(CmsisDap.TransferConfigure(idle, wait, match));

            /// <summary>
            /// Sends a Transfer Abort command to abort the current transfer.
            /// </summary>
            /// <returns>The response from the device as a byte array.</returns>
            public byte[] TransferAbort() => SendCommand(CmsisDap.TransferAbort());

            /// <summary>
            /// Sends a Write Abort command to abort the current write operation.
            /// </summary>
            /// <returns>The device response as a byte array.</returns>
            public byte[] WriteAbort() => SendCommand(CmsisDap.WriteAbort());

            /// <summary>
            /// Sends a Delay command to the device with the specified delay in milliseconds.
            /// </summary>
            /// <param name="delay">The delay time in milliseconds.</param>
            /// <returns>The device response as a byte array.</returns>
            public byte[] Delay(byte delay) => SendCommand(CmsisDap.Delay(delay));

            /// <summary>
            /// Sends a SWJ Sequence command with the specified sequence of bytes.
            /// </summary>
            /// <param name="seq">A variable-length array of bytes representing the SWJ sequence.</param>
            /// <returns>The device response as a byte array.</returns>
            public byte[] SwjSeq(params byte[] seq) => SendCommand(CmsisDap.SwjSeq(seq));

            /// <summary>
            /// Configures the SWD interface with the given parameters.
            /// </summary>
            /// <param name="param1">The first parameter for SWD configuration.</param>
            /// <param name="param2">The second parameter for SWD configuration.</param>
            /// <returns>The device response as a byte array.</returns>
            public byte[] SwdConfigure(byte param1, byte param2) => SendCommand(CmsisDap.SwdConfigure(param1, param2));

            /// <summary>
            /// Sends a JTAG Sequence command with the specified sequence of bytes.
            /// </summary>
            /// <param name="seq">A variable-length array of bytes representing the JTAG sequence.</param>
            /// <returns>The device response as a byte array.</returns>
            public byte[] JtagSeq(params byte[] seq) => SendCommand(CmsisDap.JtagSeq(seq));

            /// <summary>
            /// Configures the JTAG interface with the specified parameters.
            /// </summary>
            /// <param name="p1">The first parameter for JTAG configuration.</param>
            /// <param name="p2">The second parameter for JTAG configuration.</param>
            /// <returns>The device response as a byte array.</returns>
            public byte[] JtagConfigure(byte p1, byte p2) => SendCommand(CmsisDap.JtagConfigure(p1, p2));

            /// <summary>
            /// Sends a JTAG IDCODE command to retrieve the target’s JTAG IDCODE.
            /// </summary>
            /// <returns>A byte array containing the JTAG IDCODE from the device.</returns>
            public byte[] JtagIdCode() => SendCommand(CmsisDap.JtagIdCode());

            public void Dispose()
            {
                _stream?.Dispose();
            }
        }

        public static readonly (int vid, int pid)[] KnownDevices =
        {
            (0xC251, 0xF001), // LPC-Link-II
            (0xC251, 0xF002), // OPEN-SDA
            (0xC251, 0x2722), // ULINK2
            (0x0D28, 0x0204), // MBED
            (0x03EB, 0x2111)  // Atmel
        };

        public List<DeviceInfo> Enumerate()
        {
            var deviceList = DeviceList.Local;
            var hidDevices = deviceList.GetHidDevices().ToList();

            return hidDevices
                .Where(d => KnownDevices.Any(pair => d.VendorID == pair.vid && d.ProductID == pair.pid))
                .Select(d =>
                {
                    string? product = null, manufacturer = null;
                    try { product = d.GetProductName(); } catch { }
                    try { manufacturer = d.GetManufacturer(); } catch { }

                    return new DeviceInfo
                    {
                        Path = d.DevicePath,
                        Product = product,
                        Manufacturer = manufacturer,
                        VendorId = d.VendorID,
                        ProductId = d.ProductID
                    };
                }).ToList();
        }

        public Device Open(DeviceInfo info)
        {
            var device = DeviceList.Local.GetHidDevices().FirstOrDefault(d => d.DevicePath == info.Path);
            if (device == null)
            {
                throw new IOException("Device not found.");
            }

            return new Device(device);
        }

        // CMSIS-DAP Command IDs
        public const byte CMD_DAP_INFO = 0x00;
        public const byte CMD_DAP_LED = 0x01;
        public const byte CMD_DAP_CONNECT = 0x02;
        public const byte CMD_DAP_DISCONNECT = 0x03;
        public const byte CMD_DAP_TFER_CONFIGURE = 0x04;
        public const byte CMD_DAP_TFER = 0x05;
        public const byte CMD_DAP_TFER_BLOCK = 0x06;
        public const byte CMD_DAP_TFER_ABORT = 0x07;
        public const byte CMD_DAP_WRITE_ABORT = 0x08;
        public const byte CMD_DAP_DELAY = 0x09;
        public const byte CMD_DAP_RESET_TARGET = 0x0A;
        public const byte CMD_DAP_SWJ_PINS = 0x10;
        public const byte CMD_DAP_SWJ_CLOCK = 0x11;
        public const byte CMD_DAP_SWJ_SEQ = 0x12;
        public const byte CMD_DAP_SWD_CONFIGURE = 0x13;
        public const byte CMD_DAP_JTAG_SEQ = 0x14;
        public const byte CMD_DAP_JTAG_CONFIGURE = 0x15;
        public const byte CMD_DAP_JTAG_IDCODE = 0x16;

        // DAP Info IDs
        public const byte INFO_ID_VENDOR_NAME = 0x01;
        public const byte INFO_ID_PRODUCT_NAME = 0x02;
        public const byte INFO_ID_SERIAL_NUMBER = 0x03;
        public const byte INFO_ID_PROTOCOL_VERSION = 0x04;
        public const byte INFO_ID_TARGETDEV_VENDOR = 0x05;
        public const byte INFO_ID_TARGETDEV_NAME = 0x06;
        public const byte INFO_ID_TARGETBOARD_VENDOR = 0x07;
        public const byte INFO_ID_TARGETBOARD_NAME = 0x08;
        public const byte INFO_ID_PRODUCT_FW_VERSION = 0x09;
        public const byte INFO_ID_CAPABILITIES = 0xF0;
        public const byte INFO_ID_TEST_DOMAIN_TIMER = 0xF1;
        public const byte INFO_ID_UART_RX_BUFFER_SIZE = 0xFB;
        public const byte INFO_ID_UART_TX_BUFFER_SIZE = 0xFC;
        public const byte INFO_ID_SWO_BUFFER_SIZE = 0xFD;
        public const byte INFO_ID_PACKET_COUNT = 0xFE;
        public const byte INFO_ID_PACKET_SIZE = 0xFF;

        // Capabilities bitmask
        public const byte INFO_CAPS_SWD = 0x01;
        public const byte INFO_CAPS_JTAG = 0x02;

        // LED Control
        public const byte LED_ID_CONNECT = 0x00;
        public const byte LED_ID_RUN = 0x01;
        public const byte LED_ON = 0x01;
        public const byte LED_OFF = 0x00;

        // Connect Modes
        public enum ConnectMode
        {
            DEFAULT = 0x00,
            SWD = 0x01,
            JTAG = 0x02
        }

        // Pin Bitmask
        public static class Pins
        {
            public const byte SWCLK_TCK = 1 << 0;
            public const byte SWDIO_TMS = 1 << 1;
            public const byte TDI = 1 << 2;
            public const byte TDO = 1 << 3;
            public const byte nTRST = 1 << 5;
            public const byte nRESET = 1 << 7;
        }

        // CMD_DAP_INFO: Returns info for the given infoId.
        public static byte[] GetInfo(byte infoId) => new[] { CMD_DAP_INFO, infoId };

        // CMD_DAP_LED: Sets the LED (for example, CONNECT or RUN).
        public static byte[] SetLed(byte ledId, byte on) => new[] { CMD_DAP_LED, ledId, (byte)(on > 0 ? 1 : 0) };

        // CMD_DAP_CONNECT: Connect using the given mode (SWD, JTAG, etc.).
        public static byte[] Connect(ConnectMode mode = ConnectMode.SWD) => new[] { CMD_DAP_CONNECT, (byte) mode };

        // CMD_DAP_DISCONNECT: Disconnect the target.
        public static byte[] Disconnect() => new[] { CMD_DAP_DISCONNECT };

        // CMD_DAP_RESET_TARGET: Reset the target.
        public static byte[] ResetTarget() => new[] { CMD_DAP_RESET_TARGET };

        // CMD_DAP_TFER_CONFIGURE: Configure transfer parameters.
        public static byte[] TransferConfigure(byte idle, ushort wait, ushort match) =>
            new[] { CMD_DAP_TFER_CONFIGURE, idle, (byte)(wait & 0xFF), (byte)(wait >> 8), (byte)(match & 0xFF), (byte)(match >> 8) };

        // CMD_DAP_SWJ_CLOCK: Set the SWJ clock frequency, passed as a 32‑bit value (little‑endian).
        public static byte[] SwjClock(uint hz) =>
            new[] { CMD_DAP_SWJ_CLOCK, (byte)(hz & 0xFF), (byte)(hz >> 8), (byte)(hz >> 16), (byte)(hz >> 24) };

        // CMD_DAP_SWJ_PINS: Set(or read) the state of SWJ pins.
        // 'output' is the output pin mask, 'select' selects which pins to update,
        // and 'timeout' is a 32‑bit value (little‑endian) that may control a timeout.
        public static byte[] SwjPins(byte output, byte select, uint timeout) =>
            new[] { CMD_DAP_SWJ_PINS, output, select, (byte)(timeout & 0xFF), (byte)(timeout >> 8), (byte)(timeout >> 16), (byte)(timeout >> 24) };


        // CMD_DAP_TFER: Send a single transfer request.
        // 'req' contains control bits (e.g. read/write, APnDP, register number).
        // 'payload' holds additional data (for a write transfer).
        public static byte[] Transfer(byte dapIndex, params (byte req, uint? data)[] transfers) =>
           new[] { CmsisDap.CMD_DAP_TFER, dapIndex, (byte)transfers.Length }.Concat(transfers.SelectMany(t =>
                   new[] { t.req }.Concat(
                       CmsisDap.Device.RequiresTransferData(t.req) ? BitConverter.GetBytes(t.data ?? 0) : Array.Empty<byte>() )
               )).ToArray();


        // CMD_DAP_TFER_BLOCK: Send a block transfer request.
        // 'req' is the transfer request byte, and optional 'payload' for write transfers.
        public static byte[] TransferBlock(byte dapIndex, byte req, params byte[] payload) =>
            new byte[] { CMD_DAP_TFER_BLOCK, dapIndex, (byte)((payload.Length >> 2) & 0xFF), (byte)(payload.Length >> 10), req }.Concat(payload).ToArray();

        //
        // CMD_DAP_TFER_ABORT: Abort the current transfer.
        public static byte[] TransferAbort() => new[] { CMD_DAP_TFER_ABORT };

        // CMD_DAP_WRITE_ABORT: Abort the current write operation.
        public static byte[] WriteAbort() => new[] { CMD_DAP_WRITE_ABORT };

        // CMD_DAP_DELAY: Insert a delay; 'delay' is typically in milliseconds.
        public static byte[] Delay(byte delay) => new[] { CMD_DAP_DELAY, delay };

        /// Builds a DAP_Transfer command.
        /// Layout: [0]=0x05 | [1]=DAP Index | [2]=Transfer Count | [3]=Transfer Request | [4..]=Transfer Data (if required).
       public static byte[] SwjSeq(params byte[] seq) => new byte[] { CMD_DAP_SWJ_SEQ, (byte)(seq.Length * 8) }.Concat(seq).ToArray();

        // CMD_DAP_SWD_CONFIGURE: Configure the SWD interface.
        // Two parameter bytes are passed.
        public static byte[] SwdConfigure(byte param1, byte param2) => new byte[] { CMD_DAP_SWD_CONFIGURE, param1, param2 };

        // CMD_DAP_SWJ_SEQ: Send a sequence on the SWJ port.
        // Accepts a variable-length sequence. The first byte of the payload is the sequence length.
        public static byte[] JtagSeq(params byte[] seq) => new byte[] { CMD_DAP_JTAG_SEQ, (byte)seq.Length }.Concat(seq).ToArray();

        // CMD_DAP_JTAG_CONFIGURE: Configure JTAG with two parameter bytes
        public static byte[] JtagConfigure(byte p1, byte p2) => new[] { CMD_DAP_JTAG_CONFIGURE, p1, p2 };

        // CMD_DAP_JTAG_IDCODE: Retrieve the JTAG IDCODE.
        public static byte[] JtagIdCode() => new[] { CMD_DAP_JTAG_IDCODE };
    }
}
