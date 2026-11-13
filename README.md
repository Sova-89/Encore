# Encore

Practice your own boss segments in **Gods of Pharloom** (Hollow Knight: Silksong).

Encore is an add-on for [Gods of Pharloom](https://github.com/reinzer/GodsOfPharloom). It plays any list of its bosses in any order, for example one bench-to-bench segment of Pantheon 5, so you don't have to run the whole pantheon to practice its last five bosses.

> **Pre-release.** The mod works in the author's own runs, but few people have tried it yet. Expect bugs and please report them in Issues, with `BepInEx/LogOutput.log` attached.

[Русская версия](README.ru.md)

## Features

- **Presets**: build your own boss order, with benches between bosses if you want them. Presets are saved and stay between game sessions.
- **Difficulty per boss**: Attuned, Ascended or Radiant for every boss separately, the same modes as in the Hall of Gods.
- **Pantheon 5 out of the box**: nine ready presets, Pantheon 5 cut at its benches.
- **Runs on Gods of Pharloom itself**: its arenas, boss health, bindings and hit counter all work. Encore gives no statue badges and no pantheon completions; it is for practice only.
- **Status line** during a segment: boss number, name, difficulty, hits taken.
- English and Russian interface. In Russian, bosses are named as in the game's official Russian localization.

## Requirements

- Hollow Knight: Silksong with [BepInEx 5](https://thunderstore.io/c/hollow-knight-silksong/p/BepInEx/BepInExPack_Silksong/)
- [Gods of Pharloom](https://github.com/reinzer/GodsOfPharloom) (built and tested with 0.0.1.3). Encore does not load without it.

## Install

1. Close the game.
2. Download `Encore.zip` from the [Releases](../../releases) page.
3. Extract it into `Hollow Knight Silksong/BepInEx/plugins/`. You should get `BepInEx/plugins/Encore/Encore.dll`.

## How to use

1. Go to the Atrium or the Hall of Gods of Gods of Pharloom.
2. Press **End** to open the menu (it is used with the mouse):
   - left: presets (New, Copy, Delete, rename);
   - middle: boss order. Arrows move a boss, `×` removes it, the difficulty button cycles Attuned → Ascended → Radiant;
   - right: all Gods of Pharloom bosses, click one to add it to the end.
3. Press **Start** in the menu, or **Page Down** at any time, to play the selected preset.

After a win or a death you return to the nearest statue or pantheon door.

## Settings

Keys and language can be changed in the game with [BepInEx Configuration Manager](https://thunderstore.io/c/hollow-knight-silksong/p/jakobhellermann/BepInExConfigurationManager/) (F1 by default), or in `BepInEx/config/sova.encore.cfg`:

| Setting | Default | What it does |
|---|---|---|
| `Keys / Menu` | End | Open or close the preset menu |
| `Keys / StartSelected` | Page Down | Start the selected preset |
| `General / Language` | auto | `auto` (system language), `en` or `ru` |
| `General / ShowHud` | true | Status line during a segment |
| `General / GameFont` | true | Use the game's fonts in the window; turn off if the text looks wrong |

Presets are stored in `BepInEx/config/Encore/presets.json`. The file can be edited by hand and shared with other players.

## Notes and known issues

- A segment can only be started from the Atrium or the Hall of Gods: Encore needs a statue or a pantheon door nearby to return you to.
- A bench cannot be the first or the last entry of a preset, and two benches in a row count as one.
- `Moss Mother (Weave_03)` is the double Moss Mother fight that opens Pantheon 5. The plain `Moss Mother` switches to the same arena on Ascended and Radiant, as its statue does.
- While you type a preset name, the letter B also opens the bindings menu of Gods of Pharloom. Press B again to close it.

## Building

You need the .NET SDK and the game with BepInEx and Gods of Pharloom installed.

```
build.bat "C:\Program Files (x86)\Steam\steamapps\common\Hollow Knight Silksong"
```

or

```
dotnet build src/Encore.csproj -c Release -p:GameDir="<game folder>"
```

The DLL appears in `src/bin/Release/netstandard2.1/Encore.dll`.

Where to change things: default presets and the file format are in `src/Model.cs`, launch rules in `src/Runner.cs`, the window layout in `src/MenuDraw.cs`, colors and fonts in `src/Theme.cs`, default keys in `src/Plugin.cs`, Russian boss names in `src/Names.cs`.

## Credits

- [Gods of Pharloom](https://github.com/reinzer/GodsOfPharloom) by reinzer: Encore only builds boss lists for it.
- Hollow Knight: Silksong by Team Cherry.

## License

[MIT](LICENSE)
