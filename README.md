# 🔧 PSoC6 CMSIS-DAP Programmer

**Flash programming tool for Cypress/Infineon PSoC6 microcontrollers using CMSIS-DAP over USB.**

---

## Overview

This project provides a **CMSIS-DAP flashing and debug framework** for Cypress/Infineon **PSoC6** devices. It includes both a WinForms GUI and reusable components for direct access to:

- PSoC6 device families (ABLE2, 2M, 512K, 256K) via class-based model  
- USB HID CMSIS-DAP device discovery and command interface  
- Flashing firmware (`.hex`, `.elf`) to targets using SROM APIs  
- DAP low-level access (transfer, handshake, memory read/write)  
- Row-based verify and erase routines using official SROM calls  
- ELF loader and HEX parser included as standalone utilities  
- USB dump analyzer to interpret raw CMSIS-DAP transactions  

---

🔧 Prerequisites

To build and run this tool from source, ensure you have:
  - Windows OS (WinForms required)
  - .NET 8.0 SDK
  - Visual Studio 2022+ with .NET Desktop Development workload
  - USB CMSIS-DAP compatible debugger (e.g., KitProg3, J-Link in CMSIS-DAP mode)
  - NuGet Package: HidSharp v2.1.0

You can clone the repo and build using Visual Studio or dotnet build.

---

## 📁 Project Structure

- `Form1.cs` – WinForms GUI logic for scanning, programming, verifying
- `CmsisDap.cs` – Low-level USB HID CMSIS-DAP communication
- `Psoc6Programmer.cs` – High-level flashing and acquisition logic
- `ElfLoader.cs` – ELF parsing and segment conversion
- `HexFileParser.cs` – Intel HEX parsing and memory mapping
- `PSoCclass.cs` – Target-specific memory maps and constants

---

## ⚠️ License

AGPL-3.0-only with additional non-commercial terms.  
See [LICENSE](LICENSE) for details.

---

## ✉️ Author

**Rolf Nooteboom**  
[rolf@nooteboom-elektronica.com](mailto:rolf@nooteboom-elektronica.com)

---


## 📚 References

- [CMSIS-DAP Specification (ARM)](https://arm-software.github.io/CMSIS_5/DAP/html/index.html)
- Infineon PSoC 6 Programming Specification 002-15554 Rev. *O

Keywords: psoc6 cmsis-dap psoc-programmer firmware-flashing arm-cortex elf-parser hex-file secure-boot daplink hidsharp swd jtag srom-api infineon cypress usb-debug winforms dotnet psoc6-able2 psoc6-2m psoc6-512k psoc6-256k
