# feth-editor

Save editor for Fire Emblem: Three Houses v1.2.0. It also fixes player names being cut off when they contain multibyte UTF-8 characters.

The cross-platform .NET 10 GUI and CLI are built for Windows, macOS, and Linux in CI. The GUI edits current-run saves, NG+ history, and `system` flags; it writes a verified copy instead of overwriting the loaded file. CI artifacts are available from the latest [build run](https://github.com/jinghaihan/feth-editor/actions/workflows/build.yml). The Windows release also includes the original WinForms editor and legacy `FETH_Cli.exe`.

To run the new GUI from source with the .NET 10 SDK:

```sh
dotnet run --project Gui/FethEditor.Gui.csproj -c Release
```

The CLI accepts JSON patches, including NG+ history edits. See [CLI.md](CLI.md) for commands, examples, and safety notes.

## Credits

- [imouto1994/fe3h-editor](https://github.com/imouto1994/fe3h-editor): the original project this editor was derived from.
- [Falo's v1.2.0 Beta1 release on GBAtemp](https://gbatemp.net/threads/fire-emblem-three-houses-general-hacking.544144/post-8948080): the source, game data, and bundled Windows executables used for the v1.2.0 update. The [original archive](https://www.dropbox.com/s/8ip17sw610xkirh/FireEmblemThreeHouse_SaveEditor_v1.2.0_Beta1.7z?dl=1) is linked from that post.

Open `FireEmblemThreeHouse.sln` in Visual Studio to build the legacy .NET Framework 4.7.2 projects.
