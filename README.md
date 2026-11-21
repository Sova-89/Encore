# Encore

Practice your own boss segments in **Gods of Pharloom** (Hollow Knight: Silksong).

Encore is an add-on for [Gods of Pharloom](https://github.com/reinzer/GodsOfPharloom). It plays any list of its bosses in any order, for example one bench-to-bench segment of Pantheon 5, so you don't have to run the whole pantheon to practice its last five bosses.

> **Status.** Version 1.0.2 is the first numbered release. It works in the author's own runs and was tried by a few players; the newer features are less tested (see "Notes and known issues"). Please report bugs in Issues, with `BepInEx/LogOutput.log` attached.

[Русская версия](README.ru.md)

## Features

- **Presets**: build your own boss order, with benches between bosses if you want them. Presets are saved and stay between game sessions.
- **Difficulty per boss**: Attuned, Ascended or Radiant for every boss separately, the same modes as in the Hall of Gods.
- **Random preset**: type a number of bosses and press Roll to get that many random bosses, with a bench after every five. By default they come from the Pantheon 5 pool only (42 bosses); the Pool button switches to every boss of Gods of Pharloom. The order stays hidden until you play it.
- **Seeds**: every roll has a seed (like `K7M2QX`). Type the same seed, pool and number of bosses on another PC and you get the same order, so two players can run the same unknown order.
- **Share a preset as a string**: Copy puts the selected preset on the clipboard as one line, Paste adds a preset from such a line. Works for any preset, not only random ones.
- **Boss settings** (the Bosses tab): for each of 40 bosses with health phases, fight **only one phase** (1, 2, 3 …) from start to end, or all of them as in the game. By default it is practice (the boss has one phase's health, the fight is not counted in statistics); the **Full health** button turns it into a challenge (the boss keeps its whole health bar, the fight is counted, with its own row in the boss statistics). The roars and exits of the phase changes before the chosen phase are skipped (for 18 bosses; `Mode/SkipPhaseAnimations`). Grand Mother Silk (a separate health pool for every phase) is covered since 0.13.0: the fight ends with the chosen phase, and the roars before it are skipped (the collapse of the arena after phase 3 still plays). Cogwork Dancers and Clover Dancers (also a separate pool for every phase) are covered since 0.14.0: the earlier phases are skipped with the dancers' own "ZERO HP" event and the fight ends with the chosen phase. Bell Eater and Father of the Flame are covered since 0.16.0 (three phases each: for Bell Eater the head and the rear share the health, for Father the four arms and then the core). **Summons** (since 0.17.0): Broodmother's flies (at most 2 / 1 / none alive at once) and Moss Mother's crawlers (1 / none) and rocks (none) have a limit of their own on a second line of the row; such a fight is practice and is not counted. Since 0.18.0 Savage Beastfly (both rooms: enemies) and Crawfather (crows) can have a limit up to 5, above the vanilla number (missing enemies are copies). **Your own health** (since 0.19.0): a third row on the Bosses tab takes a number 1-9999 instead of the vanilla health (with a phase chosen - the health of that phase, with "All" - of the whole fight, the phases keep their shares); it also works for the bosses without phases (Skull Tyrant, Lost Garmond, Shakra, Garmond & Zaza). Such a fight is practice and is not counted; it does not combine with "Full health". **Summon health** (since 0.20.0): a fourth row, "Summon health", takes a number 1-9999 for every kind of summon whose health Gods of Pharloom keeps in its table (Crawfather's three crows, Savage Beastfly's Kilik / fly / Vicious Caranid in the chapel and Tarmite in the Far Fields, Forebrothers' Flintstone Flyer and Smokerock Sifter, Sister Splinter's Splinterbark); the brackets show the vanilla number, an empty field means vanilla; practice, like the other fight settings. **Immortality** (since 0.22.0): the "Immortal" button on the Health row makes the boss unkillable (it keeps at least 1 health), so one phase can be trained without end; the fight ends with the "Stop" button of the Encore window or with Hornet's death. Not for the bosses whose every phase is a separate pool of health (Grand Mother Silk, both Dancers, Bell Eater, Father of the Flame); practice, like the other fight settings. **Summon chances** (since 0.26.0): rows "Kind chances" (Crawfather's Pin Wielder / Dagger / Tinies, Moss Mother's crawlers / rocks; percentages always add up to 100) and "Chance" (Crawfather's call, 0-100 %, vanilla 75; Savage Beastfly's roar, 0-95 %, vanilla 33); practice, like the other fight settings. **Attacks** (since 0.29.0): an "Attacks" row with a button for each attack of the boss (22 bosses so far); left click turns an attack off, right click keeps only that one; practice, like the other fight settings. Attacks that the boss does not pick with a random choice are not affected: the first attack of Bell Beast (it rises from the ground with a charge), a timer, a phase change. The extra rows of a boss fold with the `+/-` button. **The setting belongs to a fight of a preset** (since 0.15.0): the same boss can have different phases in different presets, or twice in one preset; it travels with the preset when you copy it as a string.
- **Bindings, skills and gear** (since 0.25.0; the "Bindings" tab, laid out like the bindings screen of Gods of Pharloom, pictures only): the four bindings of Gods of Pharloom (Needle, Silk, Tools, Mask) and the skills of Hornet (Swift Step, Clawline, Eva Heal, Silk Soar, Cling Grip, Needolin, Needle Strike, and the two cloaks as the red cap) as tiles, for the whole game; since 0.27.0 also the needle level, masks, silk spool, silk hearts, tool pouch and crafting kit (left click next, right click back), Crestless / Cursed Hornet, the crests, and the tools in the slots of the equipped crest. They change only outside a segment. Hotkeys for the bindings are `Keys/BindNeedle`, `BindSilk`, `BindTools`, `BindMask`, `BindAll` (not set by default). Disabled skills are remembered in `Build/SkillsOff` and turned off again before every attempt. A fight with a binding or a disabled skill is a challenge: it has its own rows in the statistics (`build NS -dash`).
- **Game speed** (since 0.24.0; `-` / `Speed: N%` / `+` at the top right of the window, or `Mode/GameSpeed`): 25% to 300% for the whole game, always on, at 100% the mod does not touch the time. Above 100% it is a challenge: the fights are counted and have their own rows in the statistics (`speed 150`), and a preset keeps separate attempts and clears for every speed; below 100% it is practice and is not counted. A change works at once; if it is made in the middle of an attempt of a segment, the rest of that attempt is not counted (the next one is). Little tested; another mod that sets the game time directly will conflict with it.
- **Modes of restart** (one button, bottom right): **Normal**, **Restart on death** (hits do not matter, but when Hornet dies the segment starts over from the first boss; since 0.23.0) and **Hitless** (take a hit and the segment starts over from its first boss, with full health and tools). Checked in the game.
- **Statistics** (its own tab): attempts, clears and hitless clears for every preset; fights, wins, hits per fight and deaths for every boss, so you can see where a run usually breaks.
- **Pantheon 5 out of the box**: nine ready presets, Pantheon 5 cut at its benches.
- **Runs on Gods of Pharloom itself**: its arenas, boss health, bindings and hit counter all work. Encore gives no statue badges and no pantheon completions; it is for practice only.
- **Status line** during a segment: boss number, name, difficulty, hits taken. After a clear the mod tells you which bosses hit you.
- **Two small tools built in** (since 0.21.0, work in the whole game, not only in segments): **Hit Counter** (a "HITS: N" box in the top right corner counts the hits Hornet takes; a key resets it) and **Always Double Incoming Damage** (a key switches a multiplier of the incoming damage on and off, 2 by default). Both come from the author's earlier standalone mods and keep their settings files; if the standalone versions are still installed, the built-in ones step aside. See "Settings".
- English and Russian interface. In Russian, bosses are named as in the game's official Russian localization.

## Requirements

- Hollow Knight: Silksong with [BepInEx 5](https://thunderstore.io/c/hollow-knight-silksong/p/BepInEx/BepInExPack_Silksong/)
- [Gods of Pharloom](https://github.com/reinzer/GodsOfPharloom) (tested with 0.0.1.10; the classes Encore uses are the same in 0.0.1.3 and 0.0.1.6). Encore does not load without it.

## Install

1. Close the game.
2. Download `Encore.zip` from the [Releases](../../releases) page (or install Encore from Thunderstore with a mod manager).
3. Extract it into `Hollow Knight Silksong/BepInEx/plugins/`. You should get `BepInEx/plugins/Encore/Encore.dll`. Keep only one Encore DLL there.

## How to use

1. Go to the Atrium or the Hall of Gods of Gods of Pharloom.
2. Press **End** to open the menu (it is used with the mouse):
   - left: presets (New, Copy, Delete, rename), sharing (Copy / Paste a preset as a string) and the random preset: a number of bosses, the Roll button, the seed field and the Pool button (P5 / all);
   - middle: boss order. Arrows move a boss, `×` removes it, the difficulty button cycles Attuned → Ascended → Radiant;
   - right: all Gods of Pharloom bosses, click one to add it to the end.
3. Press **Start** in the menu, or **Page Down** at any time, to play the selected preset.

Three tabs under the title: **Presets** (the three columns above), **Statistics** (the table of the selected preset replaces the two right columns; the preset list stays on the left) and **Bosses** (settings that change a boss in the fight: a scrollable list of the bosses of the selected preset, for each fight which phase you fight and whether the boss keeps full health; the arrows in the header switch the preset; the chosen phase is also shown in the preset's own list). At the bottom of the menu: **Hitless** turns hitless mode on or off. While a segment is running (or a restart is waiting), a **Stop** button appears next to Close: it cancels the restart and ends the segment.

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
| `Mode / Hitless` | false | Hitless mode: any hit restarts the segment (same as the button in the menu) |
| `Mode / RestartOnDeath` | false | Restart the segment after Hornet dies, hits do not restart (the button in the menu sets both keys; Hitless wins when both are on) |
| `Random / BenchEvery` | 5 | A bench after every N bosses in a random preset; 0 = no benches |
| `Random / HideOrder` | true | Hide the boss order of a random preset (the Reveal button shows it) |
| `General / LogFrameLoad` | false | Write the mod's per-frame cost to the BepInEx log every 30 seconds |
| `Modules / HitCounter` | true | The built-in Hit Counter (false: no box, no key, no patches) |
| `Modules / AlwaysDoubleDamage` | true | The built-in Always Double Incoming Damage (false: no key, no patches) |

The built-in tools keep their own settings files (edit them by hand, they are not in Configuration Manager):

| File in `BepInEx/config` | Setting | Default | What it does |
|---|---|---|---|
| `abdulbasit.silksong.hitcounter.cfg` | `Controls / ResetKey` | P | Reset the counter to 0 |
| | `Display / ShowCounter` | true | Show the "HITS: N" box |
| `abdulbasit.silksong.alwaysdoubleincoming.cfg` | `Controls / ToggleKey` | Quote (`'`) | Switch the damage multiplier on / off |
| | `Damage / Multiplier` | 2 | Incoming damage × this number (below 1 counts as 1) |
| | `Damage / Enabled` | false | Current state (saved when you press the key) |
| | `Display / ShowStatus` | true | Show "DOUBLE DAMAGE: ON / OFF" for 3 seconds |

If `HitCounter.dll` or `AlwaysDoubleDamage.dll` from the standalone mods are still in `BepInEx/plugins`, remove them (with the `.pdb` and `.deps.json`), otherwise the built-in copy of that tool turns itself off.

Presets are stored in `BepInEx/config/Encore/presets.json`. The file can be edited by hand and shared with other players. Statistics are stored next to it in `stats.json`.

## Notes and known issues

- A segment can only be started from the Atrium or the Hall of Gods: Encore needs a statue or a pantheon door nearby to return you to.
- A seed gives the same order only for the same pool and the same list of bosses in Gods of Pharloom. The number of bosses does not change the start: with one seed, 5 bosses are the first five of 10. Benches and the hidden order come from the settings (`Random / BenchEvery`, `Random / HideOrder`), so to be sure that two players get identical presets, use the share string.
- A seed you type is kept: pressing Roll again gives the same order. Clear the field (or press Roll with the seed the mod made itself) to get a new random seed. Capital and small letters and spaces around the seed do not matter.
- A share string starts with `ENC1.` (or `ENC2.` when some boss in the preset has a phase setting; the string then carries the phase and the Full health switch of every fight) and is safe to paste into a chat; text around it is ignored. A string with a wrong checksum (copied in part or edited) is refused. Bosses that your Gods of Pharloom does not have stay in the preset but are skipped at the start.
- A random preset keeps the difficulty it had before the roll: set all its bosses to Radiant once and every new roll stays Radiant. Rolling replaces the preset named Random; rename it if you want to keep a roll.
- In hitless mode and in "restart on death" mode a death restarts the segment (in hitless mode it counts as a hit too; on Radiant any hit kills): Gods of Pharloom returns you to the hall, and Encore starts the same preset again once Hornet can move. Press "Stop", switch the mode to Normal, or start a preset by hand to cancel the pending restart.
- Boss statistics are counted per boss and difficulty and are shared by all presets. A preset's own numbers follow it when you rename it.
- A bench cannot be the first or the last entry of a preset, and two benches in a row count as one.
- `Moss Mother (Weave_03)` is the double Moss Mother fight that opens Pantheon 5. The plain `Moss Mother` switches to the same arena on Ascended and Radiant, as its statue does.
- While you type a preset name, the letter B also opens the bindings menu of Gods of Pharloom. Press B again to close it.
- Less tested so far: the summon limits, health and chances, the speed of the game, the double incoming damage and the share strings between two players. Checked in the game: the attacks of Bell Beast, Last Judge, Widow, Broodmother and Lost Lace, the immortal boss, phases, bindings, the Hit Counter and the restart on death.
- Fights with boss settings (phase, health, summons, attacks, immortal boss) are practice and do not go to the statistics.

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

Where to change things: default presets and the file format are in `src/Model.cs`, launch rules and hitless mode in `src/Runner.cs` and `src/Retry.cs`, statistics in `src/Stats.cs` and `src/MenuStats.cs`, the window layout in `src/MenuDraw.cs`, colors and fonts in `src/Theme.cs`, default keys in `src/Plugin.cs`, Russian boss names in `src/Names.cs`.

## Credits

- [Gods of Pharloom](https://github.com/reinzer/GodsOfPharloom) by reinzer: Encore only builds boss lists for it.
- Hollow Knight: Silksong by Team Cherry.

## License

[MIT](LICENSE)
