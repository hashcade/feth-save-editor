# feth-editor

Save editor for Fire Emblem: Three Houses v1.2.0. This fork also fixes player names being cut off when they contain multibyte UTF-8 characters.

The Windows release includes the graphical editor and `FETH_Cli.exe`. The CLI accepts JSON patches, making save inspection and editing scriptable for local agents. See [CLI.md](CLI.md) for commands, examples, and safety notes.

## Credits

- [imouto1994/fe3h-editor](https://github.com/imouto1994/fe3h-editor): the original GitHub repository this project was forked from.
- [Falo's v1.2.0 Beta1 release on GBAtemp](https://gbatemp.net/threads/fire-emblem-three-houses-general-hacking.544144/post-8948080): the source, game data, and bundled Windows executables used for the v1.2.0 update. The [original archive](https://www.dropbox.com/s/8ip17sw610xkirh/FireEmblemThreeHouse_SaveEditor_v1.2.0_Beta1.7z?dl=1) is linked from that post.

Open `FireEmblemThreeHouse.sln` in Visual Studio to build the .NET Framework 4.7.2 projects.
