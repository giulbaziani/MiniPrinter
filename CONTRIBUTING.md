# Contributing to MiniPrinter

Thanks for helping make cheap thermal printers usable on Windows.

## Ways to help

- Confirm a printer model (Bluetooth name, GATT UUIDs, paper width, what protocol worked)
- Add a protocol encoder under `src/MiniPrinter/Printing/Protocols`
- Add translations in `src/MiniPrinter/Localization/*.json` (keep `error.NNNN` in sync with `ErrorCode`)
- Improve the WPF UI without going back to a dark-only theme

## Development

```powershell
dotnet run --project src\MiniPrinter\MiniPrinter.csproj
```

New user-facing strings go in **both** `en.json` and `ka.json`. New failures need an `ErrorCode` (MP-1xxx) and `error.1xxx` entries — do not throw raw English or Georgian exception text to the UI.

## Pull requests

1. One focused change per PR.
2. Note which printer you tested (or that you could not test hardware).
3. Do not commit `bin/`, `obj/`, or personal `%AppData%` files.
