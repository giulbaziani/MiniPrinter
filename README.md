<p align="center">
  <img src="assets/logo.png" width="160" height="160" alt="MiniPrinter logo">
</p>

<h1 align="center">MiniPrinter</h1>

<p align="center">
  Open-source Windows app for cheap Chinese Bluetooth mini thermal printers.<br>
  Design labels and stickers, preview them, and print over BLE or COM.
</p>

<p align="center">
  <a href="#license"><img src="https://img.shields.io/badge/license-MIT-2B7FFF.svg" alt="MIT License"></a>
  <img src="https://img.shields.io/badge/platform-Windows%2010%2F11-0F7B6C.svg" alt="Windows">
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4.svg" alt=".NET 8">
  <img src="https://img.shields.io/badge/Bluetooth-BLE%20%2B%20SPP-1C2430.svg" alt="Bluetooth">
  <img src="https://img.shields.io/badge/languages-English%20%2B%20Georgian-C62828.svg" alt="EN + KA">
</p>

**MiniPrinter** is a desktop replacement for vendor phone apps such as Luck Jingle. It talks to pocket thermal printers that print **labels, name tags, price stickers, QR codes and barcodes** — including the **ORICO Mini Printer S1** and many OEM clones.

> Search keywords: Windows Bluetooth thermal printer, mini sticker printer, label maker, ORICO S1, Luck Jingle, LuckPrinter, Marklife, ESC/POS, cat printer, TSPL, BLE label printer, Georgian label printer.

## Why this exists

Most 10–30 USD “mini sticker printers” from AliExpress / Amazon only work with a closed Android/iOS app. There is no Windows driver. MiniPrinter:

- connects over **Bluetooth LE** (and COM/SPP if Windows paired the device)
- lets you set **width and height in millimetres**
- shows a **live preview**
- ships **built-in templates** (text, price, address, QR, barcode, inventory…)
- lets you **create and save your own templates**
- is **English by default**, with a **Georgian** UI
- is **MIT licensed** so others can add printers and protocols

## Supported printers

There is no single protocol for “Chinese mini printers”. MiniPrinter auto-detects common BLE UART profiles and you can override the driver:

| Protocol | Typical hardware |
| --- | --- |
| **Luck Jingle / ESC/POS session** | ORICO Mini Printer S1, Luck Jingle S1, many 57 mm pocket printers (`ff00` GATT) |
| **ESC/POS `GS v 0`** | ISSC UART / generic 58 mm thermal printers |
| **Cat printer `0x5178`** | GB01, GT01, MX*, “cat printer” toys (`ae30`) |
| **TSPL** | TSC-style Bluetooth label printers (COM/SPP) |

BLE services probed: `0000ff00`, Microchip ISSC UART, `0000ae30`, `0000fd00`, `000018f0`.

**Not in this release:** Niimbot’s proprietary protocol.

Tested target: **ORICO Mini Printer S1** (Luck Jingle app, ~57×30 mm, 203 dpi).

## Features

- Scan and connect Bluetooth printers; clear **Disconnected** state until you connect
- Label size in mm + presets (including ORICO S1 57×30)
- Gap labels or continuous roll
- Live 1-bit preview (dithered)
- QR (QRCoder) and Code 128 barcodes
- Saved templates in `%AppData%\MiniPrinter`
- Error codes such as `MP-1003` with localized messages (`en.json` / `ka.json`)

## Requirements

- Windows 10 version 2004+ or Windows 11
- Bluetooth adapter
- [.NET 8 SDK](https://dotnet.microsoft.com/download) to build

## Build and run

```powershell
git clone https://github.com/YOUR_USER/MiniPrinter.git
cd MiniPrinter
dotnet run --project src\MiniPrinter\MiniPrinter.csproj
```

Publish a folder you can copy to another PC:

```powershell
dotnet publish src\MiniPrinter\MiniPrinter.csproj -c Release -r win-x64 --self-contained true -o dist
```

## Print on an ORICO S1

1. Power on the printer.
2. Turn on Windows Bluetooth (pairing in Luck Jingle is optional).
3. **Scan** → select `S1_…` or Mini Printer → **Connect**.
4. Size **57×30 mm**, pick a template, type text, **Print**.
5. If the page is garbled, set protocol to Luck Jingle, then ESC/POS, then Cat Printer.

## Project layout

```
src/MiniPrinter/          WPF app (.NET 8, Windows 10 SDK)
  Localization/           en.json, ka.json, error codes
  Bluetooth/              BLE + serial transports
  Printing/               raster + ESC/POS, Luck Jingle, cat printer, TSPL
  Templates/              built-in + saved layouts
assets/                   logo.png, logo.ico
```

## GitHub topics (for Discoverability)

When you publish the repository, set **About**:

- **Description:** `Windows app for Chinese Bluetooth mini thermal label printers (ORICO S1, Luck Jingle, ESC/POS, cat printers).`
- **Website:** your release or docs URL (optional)
- **Topics:**  
  `windows` `wpf` `csharp` `dotnet` `bluetooth` `ble` `thermal-printer` `label-printer` `sticker-printer` `escpos` `tspl` `orico` `luck-jingle` `marklife` `cat-printer` `qr-code` `barcode` `open-source` `mit-license` `georgia`

CLI:

```powershell
gh repo edit --description "Windows app for Chinese Bluetooth mini thermal label printers (ORICO S1, Luck Jingle, ESC/POS, cat printers)."
gh repo edit --add-topic windows --add-topic wpf --add-topic csharp --add-topic bluetooth --add-topic ble --add-topic thermal-printer --add-topic label-printer --add-topic sticker-printer --add-topic escpos --add-topic tspl --add-topic orico --add-topic luck-jingle --add-topic marklife --add-topic cat-printer --add-topic qr-code --add-topic barcode --add-topic open-source
```

## Contributing

Printer dumps, BLE UUID lists, and new protocol encoders are welcome. See [CONTRIBUTING.md](CONTRIBUTING.md).

## License

[MIT](LICENSE) — use it, fork it, sell a supported build if you want. Keep the license notice.

## Disclaimer

MiniPrinter is not affiliated with ORICO, Luck Jingle, Marklife, Phomemo, Niimbot, or TSC. Protocol details come from public reverse-engineering and community docs. You use it with your own hardware at your own risk.
