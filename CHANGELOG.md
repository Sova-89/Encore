# Changelog

## 1.0.2

The first numbered release (before it, Thunderstore had 0.5.0 and GitHub had no release). The code is the same as 0.29.1, only the version number changed. What Encore does now, compared with 0.5.0:

- **Bosses tab**: fight only one phase of a boss, the boss's own health, summon limits, summon health and summon chances, an immortal boss, and turning single attacks of a boss off (22 bosses). Skipping of the roars and exits of earlier phases for many bosses.
- **Bindings tab**: the four bindings of Gods of Pharloom, skills, needle level, masks, silk, tools, crests and the tools in their slots, for the whole game.
- **Game speed** 25-300 %, **restart modes** (normal / restart on death / hitless), **Hit Counter** and **Always Double Incoming Damage** built in.
- Share strings ENC1-ENC8 (older strings still load).
- Checked in the game on 20-21 November: attacks of Bell Beast, Last Judge, Widow, Broodmother and Lost Lace, the immortal boss, a chosen phase, bindings, Hit Counter, restart on death, and the first Bell Beast fight after a game start with attacks turned off.
- Little tested: summon limits, health and chances, game speed, double incoming damage, share strings between two players.

## 0.29.1 (pre-release)

- **Fix: the first Bell Beast fight after starting the game did not begin** when some of its attacks were turned off (the boss stayed in its first state "Dormant" and Hornet could not move). Encore now changes the attack choice only after the game has started the boss's FSM, and it looks at the objects by name first, so it no longer touches the other FSMs of a heavy room.
- The log now says in which state the boss FSM was when the attacks were changed, when it left that state, and warns if it is still in the same state 10 seconds later.
- Tested in the game: the attacks of Bell Beast, Last Judge, Widow, Broodmother and Lost Lace. The fix was checked on the first Bell Beast fight after a game start (the boss left "Dormant" in 0.2 s and the fight began; after the opening charge it used only the jump).

## 0.29.0 (pre-release)

- **Boss attacks one by one.** On the "Bosses" tab a boss now has an **Attacks** row: one button per attack, lit when the attack is on. A left click turns an attack off (or on again), a right click keeps **only that attack**, **All** turns everything back on. At least one attack always stays on. The setting belongs to the fight of a preset, like the health and the summons, and travels with the preset (new string format **ENC8**; strings without attacks are still ENC1-ENC7). A fight with attacks turned off is practice and is not counted in the statistics.
- 22 bosses of Pantheon 5 have a list so far: Bell Beast, Lace (Deep Docks and Cradle), Fourth Chorus, Moorwing, Sister Splinter, Widow, Last Judge, Broodmother, Nyleth, Trobbio (and Tormented), Raging Conchfly, Groal, Crust King Khann, Phantom, Second Sentinel, Unravelled, First Sinner, Gurr, Seth, Pinstress, Karmelita, Lost Lace. The attack names are my own descriptive ones (not the game's), taken from the events of the bosses' state machines.
- How it works: a boss picks its next attack with a random-choice action in its FSM (states like "Attack Choice", "Close Range", "Choice P2"): every attack has an event, a weight, a "not more than N in a row" limit and a "force it after N misses" limit. For a turned-off attack the weight becomes 0 and the "force" limit is pushed out of reach, in every choice of the boss's FSM where the event is met (so the follow-ups, "Followup?", of that attack go too). If one entry is left in a choice, its "in a row" limit is lifted (otherwise the game rerolls 100 times and takes the first entry of the list, which might be a turned-off one). A choice that would be left with no weight at all is not touched.
- Known limits: if the attack you kept is not available in the boss's current phase, the boss uses its usual attacks in that phase (its choice has nothing to pick from). Attacks that the boss does not pick with a random choice (a timer, a phase change, a reaction to a hit) are not affected. Bosses that pick their attacks in another way (Grand Mother Silk, the Dancers, Crawfather's crows, Beastflies' roar) have no list yet.
- Plain logic (the lists, the mask, the plan of one choice) is in `AttackData.cs` with tests; the part that touches the game is `Attacks.cs`. The tests also check every event of every list against the dumps of the bosses' state machines.
- Not tested in the game.

## 0.28.5 (pre-release)

- **Hunter levels are a switch now, like the cloaks.** There is one tile per crest again; the three Hunter levels share one tile. When the crest is on, a left click goes to the next level (1 -> 2 -> 3 -> 1) and a right click to the previous one, and the number in the corner of the tile shows the level. While another crest is on, the Hunter tile shows the level you used last (the highest at first); one click puts it on, the next clicks change the level. The slots and the tools in them follow the level that is on (every level has its own slots).
- 0.28.4 had a separate tile for each level; this replaces it. Crests with a single version are unchanged.
- The ordering and the stepping are plain logic in `CrestMath` with tests.
- Not tested in the game.

## 0.28.4 (pre-release)

- **Hunter levels can be switched in the "Bindings" tab.** The crest row used to show only the best version of each crest, so the base Hunter and Hunter 2 could not be put on again once Hunter 3 was unlocked. Now every unlocked level of a crest is its own tile, side by side and in order (Hunter 1, 2, 3, then the other crests), with the level number in the corner of the tile. Click a tile to put that level on; the slots below and the tools in them follow the crest that is on (every level keeps its own slots, as in the game). A crest with a single version has no number. The hover text says "Level 2 of 3".
- The rows of crests are even now (9 crests: 5 and 4, not 7 and 2).
- How it works: the game keeps the older levels unlocked after an upgrade (`ToolCrest.Unlock` unlocks the previous level and copies its slots) and nothing in the game puts an upgraded crest back; only the inventory hides the older ones. The chain of levels is read from the crest's `previousVersion` field; if that cannot be read, the list falls back to the old behaviour (best version only). The ordering is plain logic in `CrestMath` with tests.
- Not tested in the game.

## 0.28.3 (pre-release)

- **Fixed: clicking the skills in the ring on the left (Silk Soar, Cling Grip) changed the needle level instead.** The needle's click area was a wide box that lay over the left half of those two skills. Now the needle's picture is drawn big, but only a narrow strip over the staff itself can be clicked; the Needle Strike picture and the other tiles were narrowed so that no two click areas overlap (checked by computing all of them). The hover glow is not drawn on tall tiles any more.
- Not tested in the game.

## 0.28.2 (pre-release)

- **The "Bindings" tab is fitted to the game's screen after a side-by-side comparison with a screenshot of the real one.** The needle is the full-height staff with the red ring on top (it is part of the needle's picture; the extra red dot is gone); the Needle Strike picture keeps its rays (the stray-scrap cleaning no longer touches it) and is dim, not nearly invisible, when the skill is not in the save; masks, silk, kit, pouch, cap, bindings, "Crestless" and "Cursed" are about 10 % bigger, the ring of skills is wider with bigger skill icons and a bigger spool middle and pearls; the frame is a little taller.
- **Selection like in the game:** the two selection corners and the name and description on the right stay on the last tile the mouse was on (at the start, "Needle Binding"), instead of vanishing when the mouse leaves. The corners follow the picture itself, not its box.
- Not tested in the game. The top-right and bottom-left frame pieces are drawn at full length (in the screenshot of the game they are half-grown, probably a frame of the opening animation).

## 0.28.1 (pre-release)

- **The "Bindings" tab is now a copy of the real bindings screen of Gods of Pharloom** (0.28.0's ring medallions were wrong). The layout follows a screenshot of the game's own screen: the needle as a tall staff on the left with the game's red orb above it, the Needle Strike picture, masks (with their number) and silk (with its number), the tool kit and the tool pouch (with numbers), the red cap (cloaks), under them the silk spool in a ring of six skills (Needolin, Sprint, Harpoon Dash, Eva heal, Super Jump, Wall Jump) with three pearls for the silk hearts under the spool; right of a fading line the four bindings, "Crestless Hornet" and "Cursed Hornet", under them the crests, the slots and the tool picker; right of the second line the name and the description of what the mouse is on (in the game's text font) and the three buttons. Pictures stand bare (no rings); the thing under the mouse gets two selection corners, like the game's cursor.
- **The frame is the game's own thorny vine frame.** The four corner pieces (`Border_Fleur_*` of the game's `UI_inventory_animated` tk2d sprite collection) are cut out of the atlas at run time (they lie turned in the atlas, so they are turned back) and drawn at the four corners of the tab; the top-right one is the bottom-left piece turned half a circle, as in the game. "Bindings" is written in the top-left corner. If the pieces are not found, drawn flourishes are used.
- Silk hearts (0–3) are the spool in the middle of the ring (click = next value, as before); a lit pearl is one heart. The wide spool picture is shown when there are hearts, the narrow one when there are none.
- Under the hood: `Icons` gets `tk2d:` keys (finds `tk2dSpriteCollectionData` by reflection, cuts the sprite out by its UVs, turns it if `flipped`) and direct pictures for the orb, the ring, the spool middle and the pearls; the pane texts are three new styles in `Theme`.
- Not tested in the game: whether the frame pieces and the new pictures are found at run time (log lines "значок «tk2d:…» ← …" / "gear:orb / ring / wide / slim / pearl"), and the exact places of the ring, the spool middle and the pearls.

## 0.28.0 (pre-release)

- **The whole Encore window now looks like the game's own interface.** A thin double frame in bone colour with a curled thorn flourish in each corner, tabs without boxes (the chosen one is lighter and has a fading line under it), corner brackets on every column, and a fading line with a diamond under every column heading. The corner flourish is the game's own picture (`overscan_corner_fleur009`) when the game has it loaded; if it is not found, a drawn one is used. The speed buttons and the bottom buttons moved inwards to leave the corners free.
- **The "Bindings" tab is built from the game's inventory pieces.** Every picture sits in a thin round "socket" ring like the tool sockets of the inventory (no more square tiles). The needle is on the game's round maroon slot, masks on its leaf-shaped back, silk on the spool-shaped back (if the game's pieces are not found, plain rings are drawn instead). The crests, "Crestless Hornet" and "Cursed Hornet" are one row of rings (the equipped one has a second outer ring); the slots of the equipped crest use the game's slot signs (attack / defend / explore / skill) tinted by colour, with the tool inside; the tool picker is a row of small rings. The lines between the columns fade at their ends.
- Under the hood: `Icons` can fetch any interface picture by sprite name (`art:` keys, cut out of the atlas like the other icons, without the stray-scrap cleaning); `PaintCore` draws the fallback flourish, the ring, the soft disc and the fading line (checked on previews, no Unity needed).
- Not tested in the game: whether the game's corner flourish, slot signs and backs are found at run time (the log lines "значок «art:…» ← …" show it; anything missing is drawn by the mod instead).

## 0.27.1 (pre-release)

- **The "Bindings" tab is laid out again, after a screenshot of 0.27.0 looked messy.** Three columns with thin lines between them, like the bindings screen of Gods of Pharloom: (1) the needle as a tall strip on the left, masks and silk big, below them silk hearts / tool pouch / crafting kit, below them the skills and the red cap (cloaks) in two even rows; (2) the four bindings, "Crestless Hornet" and "Cursed Hornet", below them the crests, the slots of the equipped crest and the tool picker; (3) the name and a short description of the tile under the mouse (like "Needle Art" on the right in GoP) and the three buttons. Every tile has a faint background so you can see where to click. The caption at the bottom of the window is gone (its text moved to the third column).
- **Stray scraps are erased from the pictures.** The skill icons in the game's inventory carry a piece of the ring around the spool, the masks a patch of shadow; in a grid they looked like dirt. After a picture is cut out, every separate part that touches its edge and is smaller than 15 % of the biggest part is erased (the outermost sparks of the Needle Strike icon may go too).
- Silk hearts (0–3, 0 = off) are the round spool tile at the end of the skills, as in 0.27.0 (the same cycle as in GoP).
- Not tested in the game.

## 0.27.0 (pre-release)

- **The "Bindings" tab looks like the bindings screen of Gods of Pharloom: pictures only, no captions on the tiles.** Left: the needle (its picture is the current needle level), "Crestless Hornet", masks, silk spool, "Cursed Hornet"; below: tool pouch, crafting kit and the red cap (the cloaks); below: the skills of Hornet and the silk hearts. Right: the four bindings, three buttons (all on / all off / return skills), the crests, the slots of the equipped crest and a tool picker. A number in the corner of a tile (masks, silk, pouch, kit, silk hearts) is its value. What a tile is called is written at the bottom of the window while the mouse is on it (like the name on the right of the GoP menu).
- **Left click: next value / switch / put on. Right click: previous value (numbers and cloaks) or clear the slot (a crest slot).** The rules are the ones of the GoP buttons: needle 0–4, masks 1–10 (with the Mask binding 1–5, same as GoP's limit, and the "before binding" count is kept), silk 0–18 (with the Silk binding 0–9), silk hearts 0–3, pouch 0–4, kit 0–4, cloaks: none / Drifter's / Faydown / both. Masks are changed by the same GoP routine, so the hearts on the screen follow at once. Everything changes only outside a segment, like the bindings and skills.
- **Crests and tools.** Every unlocked crest is a tile (only its best version); click one to put it on. "Crestless Hornet" and "Cursed Hornet" are put on and taken off like in GoP (taking off returns the crest that was on before). A tool slot is tinted by its colour (red, blue, yellow, skill); click a slot to choose a tool from the ones you own (the first tile clears the slot); a tool is never in two slots of one crest. With the Tools binding on, only skill slots can be changed.
- The cloaks are the red cap now; the two separate cloak tiles of the skill row are gone (the cap shows and changes the same two skills, and a cloak taken off is still written as "-drift" / "-fay" in the statistics). The numbers (needle, masks, silk, pouch, kit), crests and tools are **not** part of the statistics label (GoP has none either).
- Under the hood: new pictures come from the inventory (needle levels, masks, silk spool, silk hearts) and from the GoP resources (pouch, kit, cloaks, crestless, cursed); crests and tools use the pictures of the game itself. The project now references `TeamCherry.Localization.dll` (names of crests and tools).
- Not tested in the game: all the new tiles (are all the pictures found; the log lines "значок «…» ← …" show it), masks and silk with the bindings on, putting on a crest, tool slots.

## 0.26.7 (pre-release)

- **Savage Beastfly with a summon limit: one or two enemies per roar** (0.26.6 called a random number from 1 to all free places). Still never more than the free places under the limit and the free enemies in the pool. Limit 0 (off) and "as in game" are unchanged. The log line "Beastfly: called N of M possible" shows M as 1 or 2 now.
- Not tested in the game.

## 0.26.6 (pre-release)

- **Savage Beastfly (both rooms): with a summon limit the roar calls a random number of enemies, from 1 to the free places under the limit.** In the game a roar calls either one enemy ("Single") or all free ones at once ("Double"); with the limit equal to the pool (5 in the Chapel of the Beast) this gave "1 or all 5". Now, whenever a limit of 1 or more is set, "Double" is sent to "Single", and "Single" / "Single R" run our own action: a random number from 1 to min(free enemies in the pool, limit minus alive), the enemies are taken from the pool at random. Limit 0 (off) and "as in game" are unchanged. The log writes each call: "Beastfly: called N of M possible (free in pool, alive, limit) [names]".
- Not tested in the game.

## 0.26.5 (pre-release)

- **Fix: Broodmother's fly limit allowed one fly more than set.** The pool of hatchlings is four flies (`Spawner Flies`: Small Fresh, (1), (2), (3)), not three as the mod assumed, so limit 1 let her keep 2 alive. Now the pool is counted from the scene (never less than 4) and the limit works as written: at most N hatched flies alive at once (flies of the arena waves and eggs are not hatchlings and do not count). "As in game" is 4 now; the buttons are Off, 1, 2, 3. Saved limits keep their meaning (1 = at most 1 alive). The double hatch (two flies in a row) is switched off whenever a limit is set.
- The log writes a line for every hatch: "Broodmother hatches a fly: free in pool X of P (alive Y), limit N", and at the start the pool size ("fly limit ... pool 4"). If you still see more flies than the limit, send these lines.
- Not tested in the game.

## 0.26.4 (pre-release)

- **Crawfather: if only one kind of crow is left (100 %, the others 0), he calls two of them per call.** Tinies already come two at a time in the game; Pin Wielder and Dagger come one at a time, so now they come in pairs. For that the mod adds one copy of such a crow into the boss's pool at the first call (a copy of the same kind, in the same folder), and calls them with its own "Call Type" action. With a limit of crows set ("not more than N alive") the pair is cut to what the limit leaves (N = 1: one crow). Other splits of the percentages (two or three kinds with a chance above 0) work as in 0.26.0.
- **Moss Mother:** nothing to change: after the first drop her "Crawler" summon already sends two crawlers a time (checked in the FSM dump: "2nd Crawler" calls the second spawner, then "Crawler" the first), a rock fall is the same for any count.
- Not tested in the game: the pair of Pin Wielders / Daggers (do both come out of the arena doors), a copy of a crow with a limit set.

## 0.26.3 (pre-release)

- **The "Bosses" tab: all bosses of the game in one list** (new default view). About 42 rows in the order of Pantheon 5, then the others that have a setting. One setting per boss: what you change is copied into every fight of that boss in every preset (presets with a hidden order are skipped, so that the list does not give their bosses away). Each row says how many fights the boss has ("fights: 3"); if their settings differ, it says so, and an edit evens them out (the row shows the first fight's setting). A boss that is in no preset is grey, without buttons. The old view stays: the header button "View: all bosses / by presets" switches between them; "Collapse all" / "Expand all" work in both (rows of the bosses, or presets).
- Internals: a "shared" entry per boss (not in any preset) is drawn with the same code as a fight of a preset; after each frame, if it changed, it is copied into the fights of that boss and the presets file is saved (`MenuBosses.cs` only).
- Not tested in the game: the look of the list, the copy into fights of several presets.

## 0.26.2 (pre-release)

- **The "Bosses" tab: the seven lines of explanations above the list are removed** (they took a third of the window). The list starts right under the header. Nothing else changes (`MenuBosses.cs` only).

## 0.26.1 (pre-release)

- **The "Bosses" tab shows all presets at once** (it showed only the selected one, switched with `<` `>`). Every preset is a header with a `+/-` button, its name, the number of bosses, how many of them have something set, and a mark for the selected preset; its fights are listed under it. The `+/-` of a preset hides all its fights; "Collapse all" folds every preset to its header, "Expand all" opens every preset and every boss. A preset with a hidden order shows a note instead of bosses (as before). The `<` `>` buttons are gone. Folding is not saved. Nothing else changes (`MenuBosses.cs` only).
- Not tested in the game: the look with many presets, the scrolling of a long list.

## 0.26.0 (pre-release)

Customizable chances of summons, and a boss tab whose blocks fold and unfold. Gear (crest and tools) moves to 0.27.0.

- **Chance of a summon kind** (tab "Bosses", new row "Kind chances"). Percentages that always add up to 100: move one with `-5 -1 +1 +5` and the others re-balance by themselves (their proportions are kept; if the others are zero they share the rest equally). "As in game" returns the default. Crawfather: Pin Wielder / Dagger / Tinies (game: 34 / 33 / 33). Moss Mother (both fights): Crawlers / Rocks (game: 50 / 50).
- **Chance of the summon itself** (row "Chance: ..."). Crawfather: the call (game: 75 %, 0 to 100). Both Savage Beastflies: the roar (game: 33 %, 0 to 95: if a roar is refused ("Roar Pos" cancels it) the boss chooses again at once, so 100 % would loop forever). Broodmother, Splinter and the Forebrothers have no such choice (their summons are the same) and no chance rows.
- **How it works** (`Summons.cs`). The random choices of the game's own FSMs are used: weights of `SendRandomEventV2 / V3ActiveBool / V4` are replaced, and their limits "not more than N in a row" and "not less often than every N" are set to 99, otherwise the chance would not be the one you set. Crawfather: weights CALL / FINISHED in "Call?" and PIN WIELDER / DAGGER / TINIES in "Call Type" (with a limit of crows set, our own "Call Type" action picks the kind by the percentages, 1 / 1 / 2 crows a call as in vanilla). Moss Mother: ROCK / CRAWLER in "Drop Type" and "Drop Type 2"; the vanilla rule "while she has more than HP Half health, always a rock" is switched off when chances are set (otherwise the percentages would work only in the second half of the fight). Beastfly: ROAR weight in "Choice", CHARGE and STOMP keep their ratio. Checked against the FSM dumps of the game (events and variable names).
- **A fight with chances is practice** (not counted), like limits and health. The "Bosses" tag in the preset list says "summons" for chances too.
- **Preset and share string.** `Entry.Sc1..Sc3` (kinds) and `Entry.Sp` (the call), "value + 1", 0 = as in the game; the share string becomes ENC7 only when a chance is set (older strings are read as before; ENC8 is refused with a clear message). Values of a foreign boss, a wrong sum or an out-of-range call are dropped on load.
- **Tab "Bosses": fold and unfold.** A `+/-` button left of the boss number hides or shows all extra rows of that boss (summons, chances, health, summon health); the first row (phases, full health) stays. "Collapse all" / "Expand all" in the header. The state is not saved (everything is open when the window opens); a folded boss with something set keeps a highlighted button.
- Tests outside the game: all OK (886 checks, +62: kinds and limits of chances, the re-balance on a grid of 1000+ cases, normalization, preset copy and file, ENC7 with all older fields). The build is clean.
- Not tested in the game (I cannot launch it). Risks: whether the weights really give the set percentage over a long fight (the FSMs also react to HP and to bools), the look of the new rows on small windows, the crows with a limit and kind percentages at the same time.

## 0.25.1 (pre-release)

- **The "Bindings" tab is now pictures only.** Four tiles for the bindings (the same icons GoP uses in its own menu, and its "off" icons for a disabled binding) and nine tiles for the skills (icons taken from the game inventory; Needle Strike uses the icon GoP gives it). Click a tile to switch it; hover a tile to see its name and state in the line at the bottom. A disabled skill is a red tile with a cross; a skill missing from the save is a pale tile that cannot be clicked. If an icon cannot be found, the tile shows a short name instead, and Encore retries every few seconds. What was found (or not) is written to the BepInEx log once (`Icons.cs`).

## 0.25.0 (pre-release)

Bindings of Gods of Pharloom and disabling of single skills, from the Encore window. For the whole game for now (not per boss).

- **New tab "Bindings"** (fourth tab). Left: the four bindings of GoP (Needle, Silk, Tools, Mask), one button each, with what it does and its key. Below: "All on" / "All off". Right: nine skills of Hornet as buttons (Swift Step, Clawline, Eva Heal, Silk Soar, Cling Grip, Needolin, Needle Strike, Drifter's Cloak, Faydown Cloak): a click disables the skill, another one returns it; "Return all skills".
- **How it works** (`Build.cs`). GoP keeps a binding as a flag in `PlayerDataMod.bindings`, and its own checks and polling loops do the work (needle damage, silk, tools, masks), so Encore only flips the flag, saves the GoP data (`SaveModData`) and refreshes the icons of the GoP menu: the same as the button of GoP. A skill is disabled in the player data (`PlayerData.hasDash`, `hasHarpoonDash`, `hasSuperJump`, `hasWalljump`, `hasNeedolin`, `hasChargeSlash`, `hasBrolly`, `hasDoubleJump`, `HasBoundCrestUpgrader`) exactly as the GoP menu does it. The skills that Encore turned off are remembered in the setting `Build/SkillsOff` and are turned off again before every attempt of a segment (in case they were switched on in the GoP menu); "Return all skills" gives back only what Encore took. A skill that the save never had is not handed out.
- **Hotkeys** (`Keys/BindNeedle`, `BindSilk`, `BindTools`, `BindMask`, `BindAll`): not set by default (no F-keys are taken); set them in the settings file or in the ConfigurationManager window (F1). A press changes the binding and shows a message.
- **Only outside a segment.** While a segment of Encore or a pantheon of GoP runs, buttons and keys are locked (GoP does the same), the tab says why. Nothing is saved in `presets.json`: the choice belongs to the game and works for any preset.
- **Statistics: a challenge with its own rows.** A fight with a binding or a disabled skill counts in the statistics on its own row, like a speed-up: the key gets a mark `build NS -dash` (letters N S T M for the bindings, `-code` for a skill), together with the speed `speed 150 | build NS`. The preset keeps separate attempts and clears for every build, the table of the Statistics tab shows the build that stands now (` · NS -dash` after the name), the status line during a segment too. Renaming a preset moves the numbers of all its builds. Without bindings and with all skills on nothing changes: the old numbers stay where they were.
- **Changed in the middle of an attempt** (another mod, a file edit): the rest of that attempt is not counted, a message says so (the same rule as for the speed). If Encore cannot find out the build at the start of an attempt, the attempt is not counted either.
- Tests outside the game: all OK (824 checks, +38: the labels, the list of skills, the keys of the statistics with speed and phase, the rename, the file, the change in the middle of an attempt). The build is clean against the current `GodsOfPharloom.dll` (205 755 392 bytes, from 19 Nov) and `Assembly-CSharp.dll`; the handlers of the bindings and skills in that GoP are the same as in the previous one.
- Not tested in the game (I cannot launch it). Risks: whether the loops of GoP (mask, silk, tools) react to a flag changed from outside the way they react to its own button (by the code they poll the flag), the look of the new tab on small windows, whether a disabled skill really disappears at once (Swift Step, cloaks).
- Not in this version: gear (crest and tools) and a build per boss; they are next ("then it can be changed" per boss).

## 0.24.3 (pre-release)

Robustness only: a review of the hooks and of the error paths of 0.19 – 0.24. No new functions, nothing changes while everything works.

- **Every hook binds** (checked against the real `Assembly-CSharp.dll` and `GodsOfPharloom.dll`): each hooked game method exists, has no unexpected overloads, and the two overloaded ones (`HealthManager.ApplyExtraDamage`, `PlayerData.TakeHealth`) are chosen by their parameter types. The reflected fields (`HealthManager.initHp`, `SendEventByName.sendEvent`) and `HealthManager.ApplyDamageScaling(HitInstance)` exist too. `TimeManager` has a static constructor, so the speed can be set at plugin start.
- **One broken part no longer stops the others.** `Plugin.Update` ran `Mods.Tick`, `Runner.Tick`, `GameSpeed.Tick` and `menu.Update` in a row: an exception in one of them skipped all the rest on every frame and filled the log. Each is now in its own `try`; an error is written to the log three times per place (with the stack), then silently (`Plugin.Caught`).
- **A failed step of a room load no longer skips the next ones.** `Runner.OnSceneLoaded` ran six steps one after another; an exception in an early step meant the boss settings (phase, health, summons, immortality) were never applied. Each step is now isolated.
- **Fail closed for the statistics.** If setting the difficulty or arming the boss settings throws, that fight is marked as practice (not counted): better to lose a record than to put a wrong one into the statistics.
- **`NextBoss` and `PlayerDead` hooks** are wrapped in `try` like the others (they only set a flag, so nothing was wrong, but an exception in a prefix would break the game's own method).
- **A broken speed hook now really switches the speed off.** `GameSpeed.Fail` asks the game to count the time again (before, the time stayed multiplied until the next recount), tells the player with a message on the screen, and the window shows "(not working)" next to the speed. The statistics count such a game as 100% (`Plugin.EffectiveSpeed`) instead of writing "speed N" rows for a game that did not run at that speed, and a later change of the number cannot switch the broken hook on again.
- Tests outside the game: all OK (786 checks; these paths are not in the test project, the build is clean). Not tested in the game.

## 0.24.2 (pre-release)

Second fix of the game speed. The tester's log (0.24.0) showed that the speed was set (`Time.timeScale` 1.5) and then lost; the cause is found in the game's code.

- **The cause:** the game sets `Time.timeScale` behind `TimeManager` in two places: `TimeManager.Reset` (leaving to the menu) and the PlayMaker action `ScaleTime` (scripts of scenes and fights: pause, resume, slow-downs). Any such write put the time back to 1 and the speed was lost until something went through `TimeManager.UpdateTimeScale` again; being hit does (the freeze frame), so "150% does not work until I take damage".
- **`ScaleTime` is patched now** (`GameSpeedActionPatches`, its own `Mods.Patch`, a postfix on `ScaleTime.DoTimeScale`): the value that the action wrote is multiplied by the speed factor too. A slow-down of a script stays a slow-down (0.5 becomes 0.75 at 150%), `1` becomes the chosen speed at once, `0` (pause) stays 0. No lag, no fight with the game's scripts.
- The safety net `GameSpeed.Tick` stays for everything else (`TimeManager.Reset`, other mods that set the time themselves).
- The log shows how many times the `ScaleTime` patch worked: `Скорость игры: N% (… перехват сработал K раз, ScaleTime до этого M раз)` and in the lines `время было сбито`.
- Tests outside the game: all OK (786 checks). Not tested in the game. If it still does not hold the speed, send `LogOutput.log`.

## 0.24.1 (pre-release)

Fix of the game speed (a tester wrote that "150% does not speed up until I take damage").

- **The speed now works at once, in a segment too.** In 0.24.0 a change made in the middle of a segment waited for the next attempt (so that the statistics would not mix two speeds), and with the hitless mode the next attempt starts only after a hit: that is exactly what the tester saw. Now `-` / `+` and the setting `Mode/GameSpeed` change the speed of the game at the same moment, whatever is going on.
- **What the statistics do when the speed is changed in the middle of an attempt:** the fights already finished stay as they were counted; the fight in which the speed was changed, the following ones and the clear / death of that attempt are not counted (the speed in them is mixed), and a message on the screen says so. The next attempt is counted as usual, at the speed that stands then. `RunLog.MarkFrom`, `Runner.OnSpeedChanged`.
- **A safety net** (`GameSpeed.Tick`): the game sets `Time.timeScale` behind `TimeManager` in a few places (leaving to the menu, the PlayMaker action `ScaleTime`). Ten times a second the mod checks that the game time is what its patch wrote last; if not, it asks the game to count the time again, and the patch applies the speed again. A pause (0) and the normal speed are never touched. If that recount does not reach the patch at all, the speed switches itself off and writes it to the log.
- **The log now shows what happens:** `Скорость игры: N% (Time.timeScale сейчас X, перехват сработал K раз)` on every change (K = 0 with a speed above 100% would mean that the patch does not work), and up to 6 lines `время было сбито … пересчитано заново` if the game or another mod put the time elsewhere.
- Tests outside the game: all OK (786 checks, +7: the fights before the change stay, the rest of the attempt is not counted, repeated calls, a change after the end of an attempt).
- Not tested in the game (I cannot launch it). If the speed still does not apply at once, send `LogOutput.log`: the lines `Скорость игры:` show whether the patch ran.

## 0.24.0 (pre-release)

Game speed, 25% to 300%, for the whole game. Speed-ups are a challenge and have their own rows in the statistics.

- **New control at the top right of the Encore window:** `-` / `Speed: N%` / `+`. A click on `-` or `+` moves the speed by 5 (with Shift by 25), a click on the middle button returns it to 100%. The status line under the window says what the chosen speed means for the statistics. The same number is the setting `Mode/GameSpeed` in `sova.encore.cfg` (25 to 300, 100 by default, in steps of 5).
- **One speed for the whole game, always on** (not only in Encore segments), like the plain Hall of Gods challenges. At 100% the mod does not touch the game's time at all.
- **Above 100% it is a challenge:** the fights are counted in the statistics, on their own rows. A boss row gets the mark `speed N` (`Boss | Attuned | speed 150`, with a chosen phase `... | phase 2 | speed 150`), the statistics table shows the speed after the name (`Widow · 150%`), and the attempts, clears, hitless clears, deaths and the best result of a preset are kept for every speed separately (the key of the preset is `Name | speed 150`). The table shows the numbers of the speed set right now. Renaming a preset moves the numbers of every speed with it.
- **Below 100% it is practice:** the fights are not counted (like every other custom fight setting). In the table the rows stay those of the normal speed.
- **During a segment the speed is taken once per attempt** and kept to the end of the attempt (a restart after a hit or a death takes the number again). If you change it in the window during a segment, the status line says "from the next attempt"; after the segment the game goes at the number set in the window. Outside a segment a change works at once. The status line of a segment shows `speed N%` when it is not 100.
- **How it works** (`GameSpeed.cs`). The game keeps `Time.timeScale` in `TimeManager.UpdateTimeScale`: it multiplies all its own factors (slow-downs, camera shake, pauses) and cuts a result above 1 down to 1. A slow-down therefore works by itself, a speed-up does not. A postfix on that method multiplies the result by the speed factor. A pause stays 0, the game's own slow-downs and freezes stay, everything just goes faster or slower. When the number changes, `TimeManager.TimeScale = TimeManager.TimeScale` makes the game count again at once. All the timers of Encore use `Time.unscaledTime`, so the speed does not change them. The patch is installed separately (`Mods.Patch`), an exception in it is written to the log once and the speed switches itself off.
- **Not in the preset and not in the share string:** one number for the whole game, so a preset that you send does not carry a speed, and ENC strings stay as they were (ENC6 is still the newest).
- Tests outside the game: all OK (779 checks, +35: the limits and the step, the factor, challenge / practice / normal, the buttons, the statistics marks and keys, separate numbers of different speeds, the rename, saving and reading the file).
- Not tested in the game (I cannot launch it). Risks: another mod that sets `Time.timeScale` directly (they would fight over it); an effect of the game that sets `Time.timeScale` without `TimeManager` (the game does it only in `TimeManager.Reset` when leaving to the menu; PlayMaker has a `ScaleTime` action, whether any of the game's scripts use it is unknown); the physics step count per frame at 300% on a weak PC; the sound does not speed up.

## 0.23.0 (pre-release)

Restart after death without the hitless mode.

- **Three modes on one button** (bottom right of the Encore window, it cycles): **Normal** (nothing is restarted, as before), **Restart on death** (new: hits do not matter, but when Hornet dies the segment starts again from the first boss), **Hitless** (as before: any hit restarts the segment at once, a death too). Until now the automatic restart worked only in the hitless mode.
- **Settings:** the new key `Mode/RestartOnDeath` in `sova.encore.cfg` (false by default); `Mode/Hitless` stays as it was and wins when both are on. The button changes both keys for you.
- **The status line of a segment** shows the attempt number in the new mode too ("hits: N · attempt M"). The "Stop" button works as before and cancels a waiting restart.
- Same machinery as the hitless restart after a death: Gods of Pharloom returns Hornet to the hall, Encore waits until she can move and starts the same preset again (`Retry.cs`). It was written for Radiant, where a hit is a death; with Attuned and Ascended the death comes after several hits and the way back is the same, but it is not confirmed in the game.
- Tests outside the game: all OK (744 checks, +11: the mode from the two keys, the cycle of the button, what each mode restarts on death and on a hit).
- Not tested in the game (I cannot launch it).


## 0.22.0 (pre-release)

Boss immortality (practice only): the boss cannot be killed, so you can train one phase as long as you want.

- **New button "Immortal: on / off" on the Bosses tab**, on the "Health" row, right under "Full health". It is stored in the preset entry (`Entry.Imm`, written to `presets.json` only when on), shows as "immortal" in the preset list and in the status line during a segment, and travels with the preset.
- **What it does.** The boss does not lose its last point of health: every hit leaves it with at least 1, the fight goes on, and it never ends with the boss's death. With a chosen phase the boss stays in that phase for good (a phase ends when the boss's health reaches its threshold, and in the last phase the threshold is death). The fight ends when you press "Stop" in the Encore window, or when Hornet dies.
- **Always practice:** a fight with immortality is not counted in the statistics. It combines freely with the phase, the boss health, the summon limits and the summon health.
- **How it works** (`Immortal.cs`). A hit (`HealthManager.TakeDamage`) is marked non-lethal, the game's own flag that keeps 1 health. Extra damage (`ApplyExtraDamage`, poison and the like) is cut so that 1 health is left. Who is a boss: a creature with 50 or more health when it first takes damage (or with the boss's own health from the "Health" row, if that is smaller); the summons are weaker and die as usual, and a summon with its own health from "Summon health" is never taken for the boss. The decision is remembered per creature and made again if its health grows (the phase change sets it anew). The names and health of the creatures it looked at are written to the log (`Бессмертие: …`), up to 24 lines per fight.
- **Not covered:** the bosses whose every phase is a separate pool of health, because there a phase changes when "the health ran out" (Grand Mother Silk, Clover Dancers, Cogwork Dancers, Bell Eater, Father of the Flame): they have no button. Covered: all the others (38 of the 42 bosses of Pantheon 5).
- **Share string ENC6** (ENC5 plus one sign per entry: 1 = immortal). Written only when some boss is immortal; ENC1 to ENC5 are read as before. The sign for a boss that cannot be immortal is dropped on reading. ENC7 and above: "string from another version".
- The patches are installed separately (`Mods.Patch`), so a failure there cannot stop Encore; an exception inside a patch is written to the log once and the immortality switches itself off.
- Tests outside the game: all OK (733 checks, +47: who gets the button, the entry and the file, copying a preset, ENC6 round trip and the version choice, damaged and short strings, the boss threshold and the damage arithmetic).
- Not tested in the game (I cannot launch it). Risks: a boss whose script ends the fight by another way than its health (a scripted death, "kill all"), a boss whose script waits for the health to be exactly 0 or below at some moment other than death, the hit effect and sound of a boss that stays on 1 health, a creature other than the boss with 50 or more health (it would become immortal too: the log shows who was taken), a boss with its own health below 50 when its summons have more.

## 0.21.0 (pre-release)

Two small mods of the author, Hit Counter and Always Double Incoming Damage, now live inside Encore (HP Dist stays a separate mod).

- **Hit Counter 1.1.4, moved as it was.** The "HITS: N" box in the top right corner of the screen counts the hits Hornet takes. A hit that the game reports in several calls in a row (the ticks of a special attack and the normal tail after it, 0.75 s window) is counted once. The reset key (default `P`; yours is whatever your `abdulbasit.silksong.hitcounter.cfg` says) sets the counter to 0. The counter is never reset by itself, not on a new room and not on a new fight. It works in the whole game, not only in segments. It has nothing to do with the "hits" in Encore's status line (those come from Gods of Pharloom).
- **Always Double Incoming Damage 1.7.0, moved as it was.** A key (default `'`) switches the multiplier of incoming damage on and off; the box "DOUBLE DAMAGE: ON (x2)" / "OFF" shows for 3 seconds after the switch and at the start of the game. The multiplier (default 2, a number below 1 counts as 1), the switch state and the keys are saved in `abdulbasit.silksong.alwaysdoubleincoming.cfg`. It works in the whole game; with Gods of Pharloom's own Ascended multiplier the two multiply (as before).
- **The old settings are picked up as they are.** Both modules read and write the very same files, sections and names as the standalone mods (`BepInEx/config/abdulbasit.silksong.hitcounter.cfg`: `Controls/ResetKey`, `Display/ShowCounter`; `BepInEx/config/abdulbasit.silksong.alwaysdoubleincoming.cfg`: `Damage/Multiplier`, `Damage/Enabled`, `Controls/ToggleKey`, `Display/ShowStatus`). Same labels, same sizes and places of the boxes. They are not shown in Configuration Manager (F1): edit the files. On a clean install `Damage/Enabled` starts as false (the standalone mod started with true).
- **If the standalone mods are still installed** (`HitCounter.dll`, `AlwaysDoubleDamage.dll`), Encore sees them at start, switches its own copy of that module off (otherwise the damage would be doubled twice and every hit counted twice), writes it to the log and shows a message on the screen. Remove or rename the old DLLs (with their `.pdb` and `.deps.json`) to use the built-in ones.
- **New `Modules` section in `sova.encore.cfg`:** `HitCounter` and `AlwaysDoubleDamage` (both true) switch a module off completely (no patches, no box, no key).
- The keys of the two modules are ignored while the Encore window is open (you can type in it).
- Code: `Mods.cs` (start, config files, the two modules and their Harmony patches), `ModLogic.cs` (the hit filter and the damage arithmetic, without Unity). The methods patched are the ones the standalone mods patched: `PlayerData.TakeHealth(int, bool, bool)`, `HeroController.TakeDamage(GameObject, ..., ..., ..., ...)` (only to name the source in the log) and `HeroController.DoSpecialDamage`; checked against the game's `Assembly-CSharp.dll`.
- Tests outside the game: all OK (686 checks, +20: the hit filter on the cases of the standalone mod, the damage arithmetic and its overflow, the box text, every section / key / description / label of the two standalone mods against the strings read from their DLLs).
- Not tested in the game. Risks: the two boxes next to Gods of Pharloom's own interface, the Harmony patches of the second `TakeHealth` prefixes together with Gods of Pharloom's, SilksongRecorder (it looks for the standalone mods by their assembly name and version; after the move its profile must be updated).

## 0.20.0 (pre-release)

Health of the summons (practice only): the next step after your own boss health.

- **New row "Summon health" on the Bosses tab** (under the "Health" row): one field per kind of summon, a number from 1 to 9999; the vanilla number is shown in brackets next to the field, an empty field means vanilla. It works at once, is stored in the preset entry (`Entry.Sh1`..`Sh3`, written to `presets.json` only when set), shows as "summons" in the preset list and travels with the preset.
- **Which summons.** Only those whose health Gods of Pharloom takes from its own table (`EnemyHp`), read when the boss FSM starts: Savage Beastfly in the Chapel of the Beast (Kilik, the fly, Vicious Caranid), Savage Beastfly in the Far Fields (Tarmite), Sister Splinter (Splinterbark), Forebrothers Signis & Gron (Flintstone Flyer, Smokerock Sifter), Crawfather (Pin Wielder, Dagger and Tinie crow: three separate fields). Broodmother's flies and Moss Mother's crawlers and rocks are not in that table; they need another approach and are not covered yet.
- **How it works** (`SummonHp.cs`). Right after the room loads, the rows of the GoP table for the chosen kinds are replaced (all difficulties), GoP reads them when the fight starts and gives every summon that health; when the room is left or the segment ends, the rows are put back. The copies made by the summon limits (0.18.0) copy an existing summon, so they have the same health.
- **Always practice:** a fight with summon health is not counted in the statistics (the same marker as the other fight settings). It combines freely with the phase, the boss health and the summon limits.
- **Share string ENC5** (ENC4 plus three groups of four digits per entry: the health of the first, second and third kind). Written only when some boss has summon health set; ENC1 to ENC4 are read as before. A value for a boss without such summons, or for a kind it does not have, is dropped on reading. ENC6 and above: "string from another version".
- Tests outside the game: all OK (666 checks, +58: the kinds of every boss, the GoP table rows and the GoP code that reads them, ranges, `Normalize`, `PresetStore.SetSummonHp`, the file, ENC5 round trip, the version choice, damaged and short strings).
- Not tested in the game (I cannot launch it). Risks: whether the replaced table rows are read at the moment the fight starts (they are for the boss table), the width of the new row on small windows (three fields of Savage Beastfly), the new field in the scroll list.

## 0.19.0 (pre-release)

Your own boss health (practice only): the roadmap item after the summon limits.

- **New row "Health" on the Bosses tab** (under the phase buttons and the summon row): a field for a number from 1 to 9999 and the "As in game" button. Empty or 0 means vanilla. The number works at once, is stored in the preset entry (`Entry.Hp`, written to `presets.json` only when set), shown in the preset list ("health 300") and travels with the preset. Next to the field the tab shows the vanilla total ("game: 1200") when it is known (not for the double fights of Moss Mother and for Broodmother).
- **What the number means.** With a phase chosen ("only phase 2") it is the health of that phase instead of the phase's own pool; with "All" it is the health of the whole fight and the phases keep their shares (the GoP health table is scaled, `PhaseLock.ScalePlan`, the sum is exact and every phase keeps at least 1). Example: "All" with health 600 on a boss whose vanilla health is 1200 gives half of the health, and the phase changes come at the same relative points.
- **Always practice.** A fight with your own health is not counted in the statistics (the same marker as the phase practice and the summon limits). It does not combine with "Full health": setting a number turns "Full health" off, pressing "Full health" clears the number.
- **Bosses without phases too:** Skull Tyrant, Lost Garmond, Shakra and Garmond & Zaza (one row in the GoP table, `PhaseRule.Single`, `PhaseLock.SingleDefs`) get the Health row only (the tab says "no phases").
- **Engines.** Same table swap as the phase lock (`BossFx.ApplyTable`), now with the `custom` parameter in `TablePlan` / `Plan`; Trobbio and Broodmother (their own path) get the health and the second-phase threshold by the same shares. Bell Eater and Father of the Flame scale their own tables (head and rear share the total; the arms, the threshold and the core keep their shares). The Unravelled with your health and no phase chosen still gets its third-phase threshold fixed (otherwise the vanilla Init would put it far from the table).
- **Share string ENC4** (ENC3 plus four digits per entry: the health). Written only when some boss has the health set; ENC1, ENC2 and ENC3 are read as before. A health value for a boss that cannot have it is dropped on reading; "Full health" together with a health value keeps the health. ENC5 and above: "string from another version".
- Tests outside the game: all OK (608 checks, +82: ranges, the scaling and the exact sum for every rule and every boss of the list, Distribute, Trobbio/Broodmother shares, Entry/PresetStore rules, the file (clamping, unknown bosses), ENC4 round trip, GoP table rows of the four bosses without phases).
- Not tested in the game. Risks: the scaled thresholds of bosses whose FSM has some absolute number of its own (a very small or very large health can start a later phase too early or too late), double fights (Moss Mother: every mother gets the number), Forebrothers (only Signis' table is scaled, Gron keeps his health), the vanilla total hint.

## 0.18.3 (pre-release)

Eight tester reports about phase locks (extra hits needed, transitions that should not be there, a soft lock).

- **Seth: no more stagger and flight off the map (soft lock).** PhaseKick pushed "P3 STUN" while Seth was still jumping from his resting place to the ground ("Rest Idle", "Roar Antic", "Roar", "Roar End"), so he was thrown from a wrong point and "Stun Air 2" never got its LAND. Those states now count as the intro, and Seth gets the push one second after the intro ends (`KickDelay`).
- **Clover Dancers / Cogwork Dancers: the dancers no longer die one after another.** The skipped phases were sent as "ZERO HP" to Dancer A, i.e. one dancer died per skipped phase. Now, for the phases below the death threshold of "Dancers Stunned" (Cogwork 3, Clover 2), the main FSM gets the global event "DANCER STUNNED" instead: the normal path ("Return Dancers", "Phase Check", "Set Phase k+1") without any dancer falling or dying. "ZERO HP" is kept only for the phase after which the first dancer really dies (Cogwork, before the last phase).
- **Lace in the Cradle and Lost Lace: no hit needed for the phase change.** Their phases are checked in "Control" only after a hit or an attack ("Idle" -> "P3 Check" / "Attack Choice"). The state has many transitions, so it was not recognised as a gate; now `SkipData.IdleGate` lists it and PhaseKick pushes "TOOK DAMAGE" there. (Lace in Deep Docks has no phase change at all, only a threshold for the cross slash, so nothing to do there.)
- **Bell Eater: the phase change starts at once, not after the first attack.** "Wait For Clear" waits until both heads are "Waiting", which happens only after they finish an attack. It is now part of the skipped chain ("Wait For Clear/NEXT > To P2 Pause > To P2 > Set Wide"), so the arena widens right away.
- **Nyleth: crooked animation fixed.** The chain skipped all four states and "Shift Recover" played one frame of its animation. Now only the roar ("Shift Antic", "Shift Sing") is skipped; the floor break (0.5 s) and the recover animation play as in the game.
- **The Unravelled: no extra hits, no husk waves between the phases.** The 0.18.1 explanation was incomplete: "Check P2" / "Check P3" of "Control" have no incoming transitions at all. The phase change is started by another FSM of the same object, "Custom Stun Control" ("P1" / "P2" check the health on "TOOK DAMAGE" and send STUN to "Control"), and its thresholds are 0.75 and 0.4 of the health it read at start, which has nothing to do with the table. So in "phase 3" (health 200) the first stun came after about 50 damage, the second after about 120, each followed by a wave of husks. Now: (1) its "P2 HP" / "P3 HP" are copied from "Control" (the table values, -1 where the change must not happen); (2) PhaseKick pushes the gates "P1" / "P2" without a hit (the intro states "Tele Antic Intro" etc. do not count as ready, delay 1.5 s); (3) the stun is skipped by PhaseSkip ("Stun Hit > Stun Tele Out"); (4) the scene "Boss Scene / Control" goes "Boss Phase 1" -> (TO P2) -> "Pos Suck Spears Pause" -> "Boss Phase 2" and, for phase 3, "Boss Phase 2" -> (TO P3) -> "Enemy Phase End Pause" -> "Boss Phase 3" instead of the husk waves. Both targets send CONTINUE to the boss, as the waves did.
- **Raging Conchfly: the roar of phase 2 at the start, without the first attack.** "Phase Check" is entered only after an attack ("Away"). New mechanism `Detours` (PhaseSkip): when phase >= 2 is selected, "First Exit" (the first dive) leads straight to "Phase Check". Also the events "ROOF SLAM" and "DRILL INTRO ROAR" are muted in the skipped chain (they played a lone sound without the roar), possibly the "broken sound" of the report.
- **Forebrothers: no pile of reinforcements at the start.** "P2/P3 Roar End" send "CALL ENEMIES" and "P4 Roar End" sends "CALL THROWER"; skipping the chains sent them all at once. Now only the last skipped chain (the phase you selected) calls its reinforcements.
- Tests outside the game: all OK (526 checks, +40: the ready states, the delays, the gates of Lace and The Unravelled, the detour, the call events, and the FSM dumps: Lace "Idle" transitions, "Custom Stun Control", the stun chain and the scene states of The Unravelled, "Wait For Clear" chain of Bell Eater, "First Exit" / "Phase Check" of Conchfly, Nyleth "Shift Break/Recover", the "CALL" events of Forebrothers, the death thresholds of both dancer fights).
- Not tested in the game (I cannot launch it). The riskiest: The Unravelled (scene redirect + stun skip), Bell Eater (heads are not "Waiting" when the arena widens) and Conchfly (detour).

## 0.18.2 (pre-release)

Five tester reports about phase locks: the phase change left remnants at the start of the fight.

- **Bell Beast, phase 3:** the rage (roar, then a 10-second bell rain) no longer starts right after the first dash. The rain after the rage roar cannot be skipped like a roar chain (its "DROP" events are sent with delays and keep falling after the state is left), so in phase 3 the flag "Raged" is set in advance, as if the rage had already happened. In a fight from phase 2 the rage goes as in the game (the chain is now skipped only when phase 3 is selected, "3|" prefix).
- **Fourth Chorus, phase 3 and 4:** one stagger instead of three at once. The "hits" of PhaseKick go one after another within a couple of frames, and each "Stun K" of "Phase Control" sent STUN to the golem. Now only the last "Stun K" sends it (the others send an empty event; the variable "Phase" is set by every state as before).
- **Sister Splinter, phase 3:** the chain of the phase change now goes to the end ("Spikes Leave 7-9", "Shake Start", "Spike Spree"), so the fight no longer starts with her disappearing, the camera shake and the spike spree. The spree event itself is muted while skipping.
- **Widow, last phase:** it starts without a hit. The last phase begins with the "fake death", and its FSM "Fake Death" waited for the first hit like the gates of PhaseKick. Now it gets the same push (only for Widow in the last phase), the fake death (0.8 s, death effects) and the rage scream are skipped. The kick also waits for the end of her intro ("Set Intro Pos", "Scream Start").
- **The Last Judge, phase 3:** the fire roar and the fire censer spin ("Flame Roar 5" goes straight into "Spin Antic") no longer come before the rage: the flag "Flamed Up" (set by "Flame Roar 2" in the game) is set in advance, and "Flame Up Check" passes to "Rage Check". The fight starts with the rage roar (skipped) and the long spin.
- New mechanisms in `PhaseSkipData.cs`: chain prefix "N|" (skip only when phase >= N), `Presets` (flags set in advance), `KickFakeDeath`, `QuietStunStates`.
- Tests outside the game: all OK (the new ones: the prefix, presets, the quiet stuns of Chorus for phases 2-4, Widow's intro states, and the FSM dumps: the flags exist and are not written by "Init", "Stun 1-3" of Chorus, the Splinter chain up to "Spike Spree").
- Not tested in the game (I cannot launch it): the tester should check the five bosses in the last phase.

## 0.18.1 (pre-release)

- **The Unravelled: the phase modes ("phase N only") now work.** The boss's FSM computes the threshold of phase 3 ("P3 HP") as "health x 0.4" in the **last** action of "Init", i.e. after Gods of Pharloom has set the thresholds from its health table (P2 = all - phase 1, P3 = P2 - phase 2); so the table threshold was overwritten. In the fight as a whole it only made phase 2 shorter, but in the Encore modes the threshold of phase 3 landed on a random number: "phase 2" turned into "phase 2 + 3", "phase 3" started late, and in "phase 1" the check for phase 3 could fire before phase 2 (the scene waits for "TO P2" there, so the boss stayed invisible without "CONTINUE"). Encore now adds its own last action to "Init" of "Conductor Boss / Control" that sets "P3 HP" = "P2 HP" - the health of phase 2 from the table (and does the same at once if the FSM has already left "Init"). Only used when a phase mode is set for the boss. **Not tested in the game.**
- Tests outside the game: all OK (the thresholds of all six modes, and the FSM dumps: the order of the actions in "Init", "Check P2" / "Check P3", "Notify Scene 2", "Boss Phase 1" of the scene).

## 0.18.0 (pre-release)

- **Summon limits for Savage Beastfly (both rooms) and Crawfather, up to 5.** The second line of the row has the buttons "As in game", 5, 4, 3, 2, 1, "off". Unlike the limits of 0.17.0 (which only go below the vanilla number), these can go **above** it: the number is how many summoned enemies can be alive at once, and when the pool has fewer, Encore adds copies. As before, a fight with a limit is practice (not counted in the statistics). Presets carry the limit in `ENC3.` (the digit is the limit plus one).
- **Savage Beastfly (Chapel of the Beast and Far Fields).** The enemies wait in the pool "Summon Enemies" (children of the object; 5 in the Chapel, 2 in Far Fields); a summoned one leaves the pool and comes back when it dies. The roar with a summon ("Roar Pos") is possible only when the pool has at least "Enemy Remaining Max" free ones, so for the limit N Encore sets it to pool - N + 1 (and in "Rematch Ready", where the boss sets it to 1 itself). If N is above the pool, the missing enemies are copies of the existing ones (`Object.Instantiate` under "Summon Enemies"). While N is below the pool, the double summon ("Double" / "Double R": all free ones at once) is redirected to the single one; when N equals the pool it is harmless and stays. **In the Chapel the first fight takes the enemies from "Summon Cages"; with a limit they are taken from the pool** (the same way the rematch does), so the count can be controlled.
- **Crawfather.** Four crows in the pool (Pin Wielder 1, Dagger 1, Tinies 2, found by the state "Summon Ready"); "Summons Active" is how many are alive. In the vanilla "Call?" he calls only when none is alive. For the limit N the check compares "Summons Active" with N - 1 (he calls while fewer than N are alive; "off": never) and "Call Type" is one action instead of three states: a random type, the count as in the vanilla (1 / 1 / 2) but not more than N minus the alive ones; the free crows of that type are called first, then any free ones. For N = 5 one more crow is a copy (created at the first "Call?"). Before the check "Summons Active" is recounted from the crows that are not in "Summon Ready", so the copies cannot spoil the count.
- **A fix for rooms whose boss lives in an additional scene.** In GoP the room of Far Fields is `Bone_East_08`, but the objects of Savage Beastfly and Fourth Chorus are in `Bone_East_08_boss_beastfly` / `Bone_East_08_boss_golem` (GoP binds its own FSM patches to those names). Until now the phase engines compared the scene names strictly, so in these two rooms they might have found nothing; and the additional scene loading after the room was taken as "another room" (it restored the GoP table). Now a scene counts as the room if it is the room or the room plus `_something` (`Rooms.Same`). This is used by the phase table, the phase kick, the phase end, the roar skip, Father of the Flame and the summon limits.
- Tests outside the game: all OK (the new ones cover the kinds and limits 0-5 for both bosses, storing and rejecting values, ENC3 with the digit 6, `Rooms.Same`, and the structure of the FSMs checked against the dumps: "Call?", "Call Type", "Call End" of Crawfather; "Roar Pos", "Summon Type", "Summon Type R", "Single", "Double", "Rematch Ready" of both Savage Beastfly rooms and the "Summon Cages" of the Chapel). **Not tested in the game.** What can go wrong: the copies (do they come alive like the originals: the references inside a copy are remapped by Unity, but the references to the outside are not), the Chapel with the pool instead of the cages, and Far Fields with a pool of 2 where only the copies make 3-5.

## 0.17.0 (pre-release)

- **Limits of summons and helpers (the Bosses tab).** A boss that has summons gets a second line under its row: a group of buttons for every kind of summon, "As in game", a number or "off". The limit is how many of them can exist at once; the setting is saved in the entry of the preset (`"Sum1"`, `"Sum2"` in `presets.json`, the value is the limit plus one, entries without a limit stay as they were). **A fight with a limit is practice**: it is not counted in the statistics (the same as the phase training). In the list of the preset such a fight gets the note "summons".
- **Broodmother: flies (limit 2, 1 or off).** She can hatch a fly only while the pool "Spawner Flies" (three flies) has a free one; a live fly leaves the pool and comes back when it dies. The states that choose an attack count the free flies in "Free Flies" and compare the number with zero; Encore changes the zero to 3 minus the limit, so a fly can hatch only while fewer than the limit are alive ("off": never). The double hatch ("Double Hatch", a random bool in "Hatch Antic") is switched off for a limit below three, otherwise the second fly would exceed it.
- **Moss Mother (both entries, and both mothers in the double fight): crawlers (limit 1 or off) and rocks (off).** The rocks fall on the signal "FALL" that the states "Rock" and "Rock - DB" send to the object "Stals": the limit "off" disables these actions (she makes the swoop, nothing falls). The crawlers: "Crawler" sends "SPAWN" to the first summoner and sets "Spawned" (while it is true she hovers and waits for the crawler to die), "2nd Crawler" sends "SPAWN" to the second one. Limit 1 disables the signal to the second summoner, limit "off" disables both signals and the "Spawned" flag. Crawlers appear only when her health is under the threshold "HP Half"; until then she throws rocks only.
- **Copy / Paste:** a preset with a summon limit is `ENC3.` (the same as ENC2, plus two digits per entry for the limits). Without limits the string is still ENC1 or ENC2, so older versions can read it. Values that the boss cannot have are dropped when the string is read.
- Technically: the FSMs are found by the object name and by the states they have (every 0.2 s, once per FSM; every 1.5 s after all expected ones are found, because the second mother of the double fight can come later) and edited in place; nothing is replaced. If something is not found, the log says so and the limit may not work.
- Tests outside the game: all OK (406 checks; the new ones cover the kinds of summons per boss, storing and normalising the limits, JSON, ENC1/ENC2/ENC3 round trips and rejected values, and the structure of the FSMs checked against the dumps: both checks of "Free Flies", "Double Hatch", the states "Rock", "Rock - DB", "Crawler", "2nd Crawler" in both rooms of Moss Mother). **Not tested in the game.** The things that can go wrong: the pool of Broodmother is three flies (taken from the dump), the game may create more; and the hover of Moss Mother while crawlers are switched off (the "Spawned" flag is kept false on purpose).

## 0.16.0 (pre-release)

- **Bell Eater now has the phase lock (3 phases).** The fight has two bodies, the head and the rear (400 health each), and one controller, "Centipede Control". Its health is shared: GoP keeps in its table the head, the rear, the damage of phase 1 (300) and the damage of phase 2 (200), and the controller moves on when the sum drops below a threshold (500, then 300). The table is replaced before the fight (the same way as for most bosses): the chosen phase gets its own health split between the head and the rear, the phases before it end at once (their threshold is above the health), the chosen phase ends at zero. The fight ends with the chosen phase (phases 1 and 2): the transition "P1" or "P2" is redirected to the state "Can Die", the one the fight reaches after phase 3 anyway.
- **Father of the Flame now has the phase lock (3 phases).** Phase 1: the four arms (BL, TL, TR, BR) share the health, phase 2: two arms (BL and TR) share it and the other two are broken in advance, phase 3: all four arms are broken in advance and the core has the health. In the game "phase 2" is only a faster rhythm of the summons after the arms lose half of their health, so the lock for phases 1 and 2 means the same arms, the threshold only decides the rhythm. An arm with zero health is really broken: as soon as it lights up (state "Idle") it gets the event "DAMAGED", the same as a hit, and breaks the normal way (animation, "Pop Core", one less in "Shell Pieces"). The fight ends with the chosen phase (1 and 2) when the last arm is broken: the transition "Broken Pause" -> "Flare Up" is redirected to "Final Hit". With the full health (challenge) the cap of hits (11 for an arm, 29 for the core) is raised, otherwise an arm would break early.
- Tests outside the game: all OK (363 checks; the new ones cover the arithmetic of both tables for every phase and both modes, the rejected tables, the end-of-fight plans, and the structure of both FSMs checked against the dumps: every state, transition and the hit caps that are used). **Not tested in the game.** The things that can go wrong: how the death sequence of Father looks when the core was never opened (phases 1 and 2), the order of the broken arms in phase 2 (BL and TR stay, I could not see which pair is more natural), and Bell Eater with a negative sum of health (the end of every phase waits for "below zero", as the vanilla phase 3 does).

## 0.15.0 (pre-release)

- **The phase setting now belongs to a fight in a preset, not to the boss.** Until now one setting per boss was shared by every preset. Now every entry of a preset has its own phase and its own *Full health* switch: the same boss can be locked in phase 2 in one preset and fought normally in another, or even be set differently twice in one preset. The setting is saved in `presets.json` inside the entry (`"Phase": 2`, `"Full": true`; entries without a setting stay as they were).
- **The Bosses tab** now lists the bosses of the **selected preset** (one row per fight, numbered like the preset); the arrows `<` `>` in the header switch the preset without leaving the tab. Bosses that have no setting are shown with a note. If the preset is hidden (a random preset with the order concealed), the tab asks you to reveal it first, so the buttons do not give the bosses away. In the preset's own list (the Presets tab) a fight with a setting gets a small note, for example "phase 2 only".
- **Old settings move on their own.** On the first start the old per-boss list in `presets.json` is copied into every entry of that boss that has no setting of its own, the old list is dropped and the file is rewritten. Nothing changes in how the fights behave at that moment.
- **Copy / Paste of a preset carries the settings.** A string that has a phase setting somewhere is `ENC2.` (every entry gets the phase digit and the Full health flag). A preset without any setting is still `ENC1.`, so it can be read by older versions. The mod reads both. Values that the boss cannot have (a phase above its count, a setting for a boss without phases, for a bench) are dropped when the string is read. Duplicating a preset copies the settings too. A re-rolled random preset starts clean, as its bosses are new.
- Statistics keys are unchanged (`boss | difficulty | phase N` for a challenge): the numbers of one boss in the same mode are still shared by all presets; only the choice of the mode is per fight.
- Tests outside the game: all OK (327 checks; the new ones cover the per-entry storage, the file without extra keys, the move of the old list, ENC1/ENC2 round trips, rejected values and a damaged string). **Not tested in the game yet**, nor the new tab.

## 0.14.0 (pre-release)

- **Cogwork Dancers (4 phases) and Clover Dancers (2 phases) now have the phase lock.** Both fights have two dancers and one main FSM, "Dancer Control", which gives the dancers the health of a phase when the phase begins, so every phase already has its own pool (GoP takes the numbers from its table). Encore changes the table like for the other bosses: the chosen phase gets its health (the phase's own for practice, the total of all phases for the challenge), the other phases stay as in GoP.
- **How the phases before the chosen one are skipped.** A mark is put at the start of every state "Start Phase k" (k below the chosen phase). One frame after it fires, the dancer "Dancer A" gets "ZERO HP", the same event that the dancer gets when its health runs out. Everything after that is the game's own path: the stun, the call "DANCER STUNNED" to Dancer Control, the next phase. After phase 3 of Cogwork the stunned dancer dies as in the normal fight, so phase 4 starts with one dancer. Each skipped phase takes about 3 s of the stun and return animation (about 15 s before phase 4 of Cogwork). The music of the skipped phases is switched off ("START Pk" is not sent to "Music Control"); the music of the chosen phase starts as usual.
- **The fight ends with the chosen phase** (all phases except the last one). In "Dancers Stunned" the phase number at which the dancers die is set to the chosen one, and for Cogwork the counter of killed dancers is set to 1 beforehand, so the stun at the end of the phase goes through "Dancer Death" to "BOTH KILLED" and the end of the fight. In Clover the death of the first dancer ends the fight anyway (in GoP "Dancer Death" has no transitions).
- The switch "KickPhaseChange" does not affect them: without the skip this mode makes no sense. If the main FSM or its states are not found, the log says so and nothing is changed.
- Not covered yet: Father of the Flame and Bell Eater. Their controllers are not in my FSM dumps (Bell Eater's "Centipede Control" has no health manager and was never dumped; the room of Father of the Flame, Belltown_08, was never visited with the dump mod on), so there is no ground truth to build on.
- The text of 0.12.3 and the code comment said that Bell Eater has the "Phase Control" gates; they belong to Fourth Chorus (the golem, object `SG_head`). Fixed in the text.
- Tests outside the game: all OK (the table arithmetic, the plan and the end-of-fight rule, every state, action and transition used, checked against the FSM dumps of both fights). **Not tested in the game yet.**

## 0.13.1 (pre-release)

- **Grand Mother Silk: the phase roars are skipped** (the request: "remove the roar, leave only the one at the start"). With her locked in phase N, the roars on the way to it fly by: the roar of phase 3, the roar after the collapse of the arena (phase 4) and the roar of phase 6. The roar of the intro (the very start) stays. Her roars sit in two FSMs, so there are two table rows: `Attack Control` (`Roar Antic > Roar > Roar End`, phases 3 and 6) and `Control` (`Rerise Roar Antic > Bound 2 > Rerise Roar > Rerise Roar End > Rerise End`, after the collapse). Sounds and the roar emitter in these chains are switched off (the roar does not stun Hornet), everything else (flags, events, the music cue of phase 4) still runs.
- Two states are left alone on purpose. `Roar Prepare` waits for an event from the boss's `Control`; if it was skipped earlier, `Control` could be left waiting in `Attack Prepare` and the boss would freeze. `Rerise Up` is the real rise of the boss by 18 units in 1.2 s, not a roar.
- What still plays as in the game on the way to phase 4-6: the collapse of the arena after phase 3 (about 14 s: stagger, fall, rubble) and the rise (1.2 s). So phase 4 starts about 17 s after the start of the fight, phase 6 about 20 s.
- The log line for her says how many roars were skipped in each FSM: "skipped phase-change roars: 3 (Attack Control 2, Control 1)".
- Tests outside the game: all OK (both chains checked state by state against the FSM dumps, 7 sound/roar actions switched off in them). **Not tested in the game yet.**

## 0.13.0 (pre-release)

- **Grand Mother Silk now has the phase lock (6 phases).** She is the first boss with a separate health pool for every phase (the FSM "Phase Control" sets the health of phase 1 and adds the health of each next phase, and checks "health <= 0?" at the end of every phase). Practice (health of one phase) and challenge (*Full health*: all 1600 health in the chosen phase) both work. The Bosses tab now has 36 bosses.
- How it works, in three parts. (1) The GoP table of phase health is changed like for the other bosses: the phases before the chosen one get health -1 (their check passes at once), the chosen one gets "needed + N-1", so the health after the changes is exactly the needed value. (2) The "phase check without a hit" of 0.12.3 opens the gates between the phases (now it also waits for the end of her intro: states Ready, Title Up and Begin). (3) New: **the fight ends with the chosen phase.** For phases 1-5 the transition of "HP Check N" to the next phase is redirected to "Death Hit", the state the fight enters after phase 6 anyway, so after the chosen phase she dies in the usual way. Phase 6 is the last one and needs no redirect.
- **The phase changes before the chosen phase were not skipped in 0.13.0** (they played as in the game; the roars are skipped since 0.13.1). The arena and the boss are left in exactly the state the game expects.
- Not covered yet (a separate health pool for each phase): Cogwork Dancers, Father of the Flame, Bell Eater, Clover Dancers.
- Tests outside the game: all OK (the health arithmetic for all 12 cases, and every state and transition used, checked against the FSM dumps). **Not tested in the game yet.**

## 0.12.3 (pre-release)

- **A phase change no longer waits for the first hit.** Some bosses (Shrine Guardian Seth, and later Grand Mother Silk and Fourth Chorus) check their health for the next phase in a separate FSM ("Phase Control") that sits and waits for exactly one event: "the boss took damage". With the boss locked in phase N the thresholds were already below its health from the first second, but nothing looked at them until you hit the boss once, and only then the phase change (with its roar) started. Now, as soon as the boss has left its waiting and intro states, Encore sends that FSM the same event a hit sends, once for each waiting state, so the boss goes through the phase changes at the very start. Only the phase FSM gets the event: the stun gauge and the boss's own attack FSM do not count it as a hit. Switch: `Mode/KickPhaseChange` (on by default). The log says "phase check started without a hit: N times".
- Bosses whose phase check already runs by itself (The Last Judge, Khann, Nyleth, Forebrothers, Splinter and others) behave as before.

## 0.12.2 (pre-release)

- **Skipped roars are now really quiet.** In 0.12.0 the states of a skipped roar flew by in a frame, but their effects still all went off at once: the roar burst, the camera shakes, the glow and the sounds (The Last Judge's second phase showed it most). Now, in the chains that are going to be skipped, the purely cosmetic actions are switched off before they run: sounds and voices, camera shake, the roar emitter, particles, controller rumble (and the Judge's "Roar Glow" flashes). Everything that matters for the fight (flags, invulnerability, positions, events) still runs. The switched-off actions are turned back on as soon as the skip is over, so later phases play as in the game. The log line now also says how many effects were switched off.
- **Forebrothers Signis & Gron are now covered**: the real phase roars of the boss (`P2 Roar`, `P3 Roar`, `P4 Roar`, about 0.9 s each) are skipped like the others. They were found by checking the recordings of real fights (about 3,400 BossLiveLog files), which also showed that the first version of this table had two rows that never worked.
- **Savage Beastfly (both rooms) has no table row any more.** Its "roar" is an ordinary attack (`Choice → ROAR → Summon`: it calls flies), not a phase change, and the "start in rage" chain from the old row was never entered in 112 recorded fights, so nothing is skipped there (the log used to say "0 of 2"). Crust King Khann phase 2 is similar: its `P2 Roar` is never used in GoP (the roar you see there is a normal attack), only the phase 3 roar is a real phase change.
- Covered now: **17 rows** (Bell Beast, Moorwing, Sister Splinter, Widow (rage), The Last Judge, Nyleth, Raging Conchfly, Groal the Great, Crust King Khann (phase 3), Phantom (last phase), Forebrothers, Lace in the Cradle, Skarrsinger Karmelita, Lost Lace (phases 2 and 4), Gurr the Outcast, First Sinner, Tormented Trobbio).

## 0.12.0 (pre-release)

- **No roars on the way to the chosen phase.** When a boss is locked in phase N, the fight starts with the boss passing phases 2…N one after another, and each change used to play its roar, exit or landing. Now these animations fly by in a frame or two, and the fight starts in phase N at once. The roar before the chosen phase is skipped too. Phases after the chosen one are not touched. Switch: `Mode/SkipPhaseAnimations` in the config (on by default), or the F1 configuration window.
- How: the boss's FSM leaves a phase change through a chain of states (Antic, Roar, Recover …). While the boss is in such a chain Encore sends it the event it waits for (`FINISHED`, or the one listed in the table), so every state is still entered and does its work (flags, invulnerability, sounds), only the waiting is cut. If a state does not react for 2.5 s, Encore lets the boss play the rest as in the game. The log says how many phase changes were skipped.
- **Covered in 0.12.0 (18 rows, see 0.12.2 for the corrections): Bell Beast, both Savage Beastflies, Moorwing, Sister Splinter, Widow (rage), The Last Judge, Nyleth, Raging Conchfly, Groal the Great, Crust King Khann, Phantom (last phase), Lace in the Cradle, Skarrsinger Karmelita, Lost Lace (phases 2 and 4), Gurr the Outcast, First Sinner, Tormented Trobbio.** Trobbio keeps its own way from 0.8.0. The chains come from the FSM dumps; the tests check every state and every transition of every chain against the dumps (75 checks).
- **Not covered:** bosses whose phase change is not a chain of states with a roar (Fourth Chorus changes phase through the stun of its body; Moss Mother, Great Conchflies, Palestag, Voltvyrm, Lugoli, Crawfather, Forebrothers and others change only the attack set), bosses with a separate health pool for each phase, and the third phase of Lost Lace and Seth. These keep their normal transitions.
- Tests outside the game: all OK. **Not tested in the game yet**: please try a few (for example Sister Splinter phase 3, Moorwing phase 2, Khann phase 3, Last Judge phase 3) and say what looks wrong.

## 0.11.2 (pre-release)

- **The "Stop" button no longer breaks the game.** If it was pressed while Hornet was still entering the boss room (the first seconds after the room loads), the second room change started before the first one had finished, and the game was left in a stuck state: Hornet could not move and the pause menu did not open. Now "Stop" waits for a safe moment (the game is in normal play, not entering a room, not paused, Hornet is alive and not in a transition or on spikes) and then ends the segment. Usually that is the very same frame; the window says "The segment will end as soon as Hornet can leave the room" while it waits.

## 0.11.1 (pre-release)

- **The Encore window can no longer leave Hornet without control.** While the window is open it blocks Hornet's keys (so typing a preset name does not make her move). Before, the block was released only by the window's own Close action. Now it follows the window exactly: it is re-checked every frame (also after Hornet is re-created), and it is dropped at once when the window is closed by any way.
- **Esc closes the window**, and so does any scene change (a boss, the Atrium, a bench). A forgotten open window is closed by the game's next scene load.
- The log now says when the window is opened and closed, and why (key, Esc, button, start of a preset, scene change), and whether Hornet's input is blocked.
- Known conflict, not in the mod: the default key of the window (`End`) is also used by other mods (for example "finish the run" or a HUD mode), so one press can do several things. Change the key in `BepInEx\config\sova.encore.cfg` (`Menu`) or in the F1 configuration window.

## 0.11.0 (pre-release)

- **Two more kinds of boss in the phase lock: Moss Mother and Broodmother.** The Bosses tab now has 35 rows.
- **Moss Mother** (four phases; the single one and the pair of Weave_03, which GoP uses on Ascended and Radiant) works the same way as the other bosses of 0.10.0: Encore changes the GoP table of phase health, and the boss's own thresholds follow. The list has two rows: `Moss Mother` (changes the single boss and the pair, whichever the segment uses for the chosen difficulty) and `Moss Mother (Weave_03)` (the pair, as in the default Pantheon 5 segment 1). In the pair, both mothers are locked in the same phase.
- **Broodmother** (two phases) is not in the GoP table: GoP leaves her health and the phase threshold of the original game (`P2 HP`, 55% of the health). Encore waits until her FSM has started, takes the health and the threshold the game set (700 and 385 in the dump), splits them into two phases (315 and 385) and sets the same numbers as for Trobbio: phase 1 only = health 315, threshold 0; phase 2 only = health 385, threshold 386 (the rage roar stays); with *Full health* the health stays 700. The wait is up to five minutes, because she comes in the last wave.
- Not covered yet (they have a separate health pool for each phase and their own phase-change animations; this needs work boss by boss): Cogwork Dancers, Father of the Flame, Grand Mother Silk, Bell Eater, Clover Dancers.
- Tests outside the game: 199 checks (Moss Mother lists, Broodmother arithmetic).
- Not tested in the game yet.

## 0.10.0 (pre-release)

- **Phase lock for every boss with health phases, not only Trobbio.** The Bosses tab is now a scrollable list of 33 bosses of Pantheon 5 (every boss of Gods of Pharloom whose phases are driven by health). Each row has the buttons **All** (as in the game) and **1, 2, 3 …** (one per phase of that boss), plus the **Full health** toggle from 0.9.0. Pick a phase and the boss fights only that phase.
- Practice or challenge, as in 0.9.0. *Full health off* (default): the boss has the health of that one phase, the fight is practice and is not counted in statistics. *Full health on*: the boss keeps its whole health pool, the fight is a challenge and is counted, with its own row (`Boss · phase 3`).
- How it works: Gods of Pharloom keeps a table of phase health per boss (`EnemyHp.enemies`) and builds both the boss's health and the thresholds of its FSM from it. Encore changes this table for the fight (before the boss's FSM starts) and puts it back when the fight ends, so the boss runs its own, unchanged FSM. The earlier phases are passed at once through their normal transitions at the start of the fight (roars and exits of the boss stay; Fourth Chorus needs one hit per transition). Palestag and Great Conchflies (two phases, a direct threshold) have their own rule; Trobbio keeps the 0.8-0.9 way, with the roar skipped.
- Settings are saved per boss in `presets.json`; an old file reads as before. A phase that the boss does not have, or a boss that is not in the list, is dropped when the file is read.
- Tests outside the game: 192 checks, including a simulation of the GoP thresholds for every boss × every phase × practice/challenge (170 cases) and a comparison of the list with the GoP table (names, number of phases).
- Not covered yet: Moss Mother, Broodmother, and bosses with a separate pool for every phase (Cogwork Dancers, Father of the Flame, Grand Mother Silk, Bell Eater, Clover Dancers) - they need their own approach.
- **Not tested in the game yet: all the new locks.** Please try a few (a boss with 2 phases, with 3, Fourth Chorus, Palestag, Great Conchflies, Forebrothers) and report what looks wrong.

## 0.9.0 (pre-release)

- **Challenge mode for boss settings: the "Full health" button.** Until now a boss locked in one phase had only that phase's health (Trobbio: 500 or 700), which is right for practice but not for a challenge run. Now each boss in the Bosses tab has a **Full health** toggle. *Off* (the default, as in 0.8.x): practice, one phase's health, not counted in statistics. *On*: challenge, the boss keeps its **whole** health pool (Trobbio: 1200) and stays in the chosen phase from start to end of the fight, so you fight the whole health bar of, say, phase 2 only. The roar is skipped in phase 2 as before.
- A challenge fight **is counted**: attempts, hits, clears and deaths go to statistics as usual. Boss statistics of a locked boss are kept in their own row (`Trobbio · phase 2`), so they do not mix with normal fights; the row appears in the table as soon as you play one. The preset's own numbers (attempts, clears, hitless clears) count a challenge segment like any other, since the preset is the same list of bosses.
- "Both phases" with "Full health" on is allowed and changes nothing in the fight; the choice is kept.
- The log line for a changed boss now says whether the fight is a challenge ("считается в статистике") or practice ("статистика не меняется").
- Old `presets.json` files keep working: a boss setting without the new field is a practice setting.
- Tests outside the game: 169 checks (full-health arithmetic: 1200 hp, threshold 0 or 1201; statistics keys; saving and hand-edited files).
- Not tested in the game yet: challenge mode, "Phase 1 only", the roar skip from 0.8.1.

## 0.8.1 (pre-release)

- **Phase 2 only now skips the roar.** In 0.8.0 the boss went to the second phase by itself, so you waited through his intro, roar and exit before the fight started. Now Encore sets the flags that the roar sets in the original fight (`Phase 2`, `Doing First Burst Column`, `Will Burst Column`; `Doing First Attack` is cleared), and the boss starts straight in phase 2: first a tornado, then his exit and the burst columns. If the boss's FSM does not have these variables, the old way (the roar) is used and the log says "рёв остался". The log line for a changed boss now ends with "рёв пропущен" or "рёв остался".
- Phase 1 only is unchanged.

## 0.8.0 (pre-release)

- **Bosses tab and the first boss setting.** The third tab, **Bosses**, has a setting for **Trobbio**: *Both phases* (as in the game), *Phase 1 only* or *Phase 2 only*. The boss keeps the chosen phase for the whole fight and stays the real boss: same attacks, same FSM. The choice is saved in `presets.json` and applies to Trobbio in every Encore segment (not to normal pantheons and statues).
- How it works: Gods of Pharloom sets one health pool and a threshold for the second phase in the boss's FSM (`P2 HP`). Once it has done so, Encore replaces both numbers. *Phase 1 only*: the health of phase 1 (700) and the threshold 0, so the second phase never starts. *Phase 2 only*: the health of phase 2 (500) and a threshold one above it, so the boss starts the second phase by itself at the first check, with its usual roar and exit.
- A fight with a changed boss is **practice**: it does not touch statistics (no fight, hit or death for that boss, no clear or death for the preset, and the attempt is not counted). The toast at the start and the message after the segment say so.
- Tests outside the game: 157 checks (the phase arithmetic, saving, hand-edited files, practice in statistics).
- Not tested in the game yet. Tormented Trobbio (three phases) is not covered: it is a different boss with its own FSM.

## 0.7.0 (pre-release)

- **Tabs.** The window has two tabs under the title, **Presets** and **Statistics**, instead of the Statistics button at the bottom. The preset list stays on the left on both tabs. The Hitless button moved to the right edge. This is groundwork: boss settings will get their own tab.
- The window is 8 units taller (968) to fit the tab strip; the columns keep their size.

## 0.6.1 (pre-release)

- Less garbage while the window is open: the Pool button text is no longer glued together every frame, and the "random" hint in an empty seed field no longer asks the game for the name of the focused control every frame. Nothing changes on screen. Roll, Copy and Paste are one-off clicks and cost well under a millisecond (measured outside the game); with the window closed the mod still draws nothing.

## 0.6.0 (pre-release)

- **Random preset from Pantheon 5.** Roll now picks from the 42 bosses of Pantheon 5 by default; the new Pool button (`Pool: P5` / `Pool: all`) switches to every boss of Gods of Pharloom. The choice is saved in `presets.json`.
- **Seeds.** Every roll has a seed, shown in a field under the Roll row, in the status line and in the log. Type a seed to repeat a roll: the same seed, pool and number of bosses give the same order on any PC. A seed made by the mod is replaced by a new one on every roll; a seed you typed is kept. With one seed, 5 bosses are the first five of 10. The mod has its own random number generator for this, so the order does not depend on the .NET version.
- **Presets as strings.** The Copy button puts the selected preset on the clipboard as one line (`ENC1.xxxx.…`), the Paste button adds a preset from the clipboard (text around the string is ignored; a damaged or foreign-version string gets a clear message). Hidden order, difficulties and benches travel with the preset.
- The window is a bit taller (960 instead of 900 units) to fit the new rows.
- Tests outside the game: 130 checks (seeds, pools, shuffling, the string format and its error cases).

## 0.5.4 (pre-release)

- Faster start. BepInEx rewrites the settings file after every single setting is created, so the mod wrote it nine times at every launch; in one log a single step took 100 ms of the 164 ms start. Now the file is written once, and only when a setting is missing from it (first launch or a new version with new settings).
- The log shows how long the first opening of the window takes: the search for the game's fonts, building the styles and collecting the boss list.

## 0.5.3 (pre-release)

- The start-up line in the log now shows how long every step of the start took (keys, general settings, random, mode, theme, key names, presets, hooks, window), in tenths of a millisecond.

## 0.5.2 (pre-release)

- New **Stop** button in the menu (shown while a segment is running or a restart is pending): it cancels the pending restart and, if a segment is running, ends it and returns Hornet to the hall. The attempt is not counted as a clear or a death.
- While the menu is open, the automatic restart after a death waits, so you can press Stop (or Start) first.
- The automatic restart waits a little longer after Hornet is back in the hall (about a second and a half).

## 0.5.1 (pre-release)

- Hitless mode: a death now also restarts the segment. On Radiant a single hit kills, so before this fix Hitless never restarted anything there: Gods of Pharloom just sent you back to the hall. Now the mod waits until Hornet is back in the hall and can move (about a second and a half) and starts the same preset again; the attempt number keeps counting. Turning Hitless off, or starting a preset by hand, cancels the pending restart.

## 0.5.0 (pre-release)

- Hitless mode (the Hitless button at the bottom of the menu, or `Mode / Hitless`): a hit restarts the segment from its first boss with full health and tools. A hit that kills is still a normal death. The mode is new and has had little testing in the game.
- Statistics (the Statistics button): attempts, clears, hitless clears, deaths and the best run for every preset; fights, wins, hitless wins, hits per fight and deaths for every boss on every difficulty. Stored in `BepInEx/config/Encore/stats.json`.
- The message after a clear lists the bosses that hit you.

## 0.4.1 (pre-release)

- The boss order of a random preset is hidden: the window shows "? ? ?" instead of names, so the roll stays a surprise. The Reveal button shows the order; the log does not print a hidden order either.
- New setting `Random / HideOrder` (on by default).

## 0.4.0 (pre-release)

- Random preset: type how many bosses you want and press Roll. The mod picks that many random bosses, without repeats while there are enough of them, puts a bench after every five, and saves them as the preset "Random". Rolling again replaces it; a renamed random preset is kept.
- New setting `Random / BenchEvery`: how often a bench appears in a random preset (0 turns benches off).

## 0.3.1 (pre-release)

Less work for the game, no changes in how the mod is used.

- With the window closed and no segment running, the mod draws nothing at all: the drawing part is switched off until it is needed.
- The window and the status line are drawn once per frame instead of twice.
- The status line, key names and counters are no longer rebuilt every frame, only when they change.
- The boss list draws only the rows that are on screen.
- The log shows how long the mod took to start. New setting `General / LogFrameLoad` writes the mod's per-frame cost to the log every 30 seconds.

## 0.3.0 (pre-release)

- New look of the window: the game's own fonts (Trajan and Perpetua), dark panels, difficulty shown as colored tags, icons instead of text arrows, a thin scrollbar.
- The window is drawn at the real screen resolution, so text stays sharp on 1440p and 4K, and it shrinks to fit narrow screens.
- The segment status line and messages are shown on dark plates.
- Enter confirms a preset name; the header of the order column shows how many bosses and benches the preset has.
- New setting `General / GameFont` (turn it off to get the plain font back).

## 0.2.0 (pre-release)

First public version.

- Presets: custom boss order with optional benches, saved to `BepInEx/config/Encore/presets.json`.
- Difficulty per boss: Attuned, Ascended, Radiant.
- Nine default presets: Pantheon 5 of Gods of Pharloom cut at its benches.
- Mouse-driven menu (End), quick start of the selected preset (Page Down).
- Status line during a segment and a summary with the number of hits at the end.
- English and Russian interface; Russian boss names follow the game's official localization.
