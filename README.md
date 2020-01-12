[![following][library]](https://www.youtube.com/watch?v=gFX9OVSS3fg)

# It's time to adventure (NPC Adventures)

**THIS MOD IS IN ALPHA BUILD!** It may be unstable with some bugs. Found any defect? Report it and help improvement this mod. Any idea? Create a proposal on Github. Thanks.

Go to an adventure with Pelican Town's villagers! Recruit bachelorete or bachlor and go to adventure togehter.

**Looking for user guide? See [Documentation](docs/index.md)**

## Features

- Ask NPC to a follow farmer (5 hearts and more required)
- Recruited NPC can fight with monsters (with swords and for NPC with personal skill fighter)
- Various dialogues for different locations (incomplete yet)
- Can save items from our repository to a npc's backpack
- If you want to break adventure, then you can release a companion
- Next morning you can find a package with your items you saved in companion's backpack
- Idle animations
- Speech bubbles while companion fighting
- Different personal skills: warrior, fighter and doctor (next comming soon)
- Doctor can heal you if your health is under 30% and can try to save your life before death
- Warrior can use critical defense fists
- Fighter can level up (syncing level with player) and can upgrade swords
- Display personal skills in HUD (with mouseover tooltip)
- Better critical defense fist fight animation and sound
- Support for content packs and localizations. You can add custom NPC as companion (see [how to create content pack](https://github.com/purrplingcat/PurrplingMod/wiki/Content-packs) on wiki)
- User configuration (via `config.json`, see [SMAPI docs](https://stardewvalleywiki.com/Modding:Player_Guide/Getting_Started#Configure_mods))
- Every companion NPC grants a buffs
- [NEW] Two gameplay modes: Adventure (default) and classic

Next features comming soon...

**Watch [trailer video](https://www.youtube.com/watch?v=gFX9OVSS3fg) for this mod**

## Install
- [Install the latest version of SMAPI](https://smapi.io).
- Download this mod and unzip it into *Stardew Valley/Mods*.
- Run the game using SMAPI.

## Supported companion NPCs

| Companion | Personal skills | Buffs                        |
| --------- | --------------- | ---------------------------- |
| Abigail   | Warrior         | +1 Speed, +1 Luck, +1 Attack |
| Alex      | Warrior         | +1 Speed, +2 Attack          |
| Haley     |                 | +2 Luck                      |
| Maru      | Doctor          | +1 Mining, +1 Luck, +1 Speed |
| Shane     |                 | +3 Farming                   |
| Leah      |                 | +2 Foraging                  |
| Emily     |                 | +2 Mining                    |
| Penny     |                 | +3 Farming                   |
| Sam       |                 | +2 Speed                     |
| Sebastian | Warrior         | +1 Speed, +1 Luck, +1 Attack |
| Elliott   |                 | +3 Fishing                   |
| Harvey    | Doctor          | +3 Defense                   |

All listed NPCs can figth with sword! **Married spouse** grants additional buffs **+1 Luck** and **+1 Magnetic** radius

For some NPCs listed above we're missing companion dialogues. You can help us and you can create it. How? Fork this repo and see [Dialogue wiki](https://github.com/purrplingcat/PurrplingMod/wiki/Dialogues). You can get inspiration [from code](https://github.com/purrplingcat/PurrplingMod/tree/master/PurrplingMod/assets/Dialogue). Are you created dialogues? Just send us a PR.

## Custom NPCs

You can add your custom NPC as companion to this mod via content pack. See [documentation](https://github.com/purrplingcat/PurrplingMod/wiki/Content-packs)

## Compatibility

- Works with Stardew Valley 1.4 on Linux/Mac/Windows.
- Works in **single player** ONLY.

### Compatibility with other mods

- ✅ **Custom Kissing Mod** by *Digus* - 100% compatible (from version 0.9.0 with version 1.2.0 and newer of Custom Kissing Mod.
- ⚠️ **Automatic gates** - NOT COMPATIBLE! Companion can stuck in gate when gate is automatic closed after farmer.
- ⚠️ **Json Assets** - CAN'T USE CUSTOM ITEMS FOR COMPANIONS. Can't use custom weapons in disposition file from JA in your content packs, because this mod not exported stacit item ids.

## Translations

- English (Corashirou, [RememberEmber](https://www.nexusmods.com/users/11961608), [PurrplingCat](https://www.nexusmods.com/users/68185132))
- Portuguese Brazilian ([andril11](https://www.nexusmods.com/users/68848663))
- French ([Reikounet](https://www.nexusmods.com/users/70092158))
- Chinese ([wu574932346](https://www.nexusmods.com/users/67777356))

## Feature preview

**Asking NPC to a follow farmer**

![Ask to follow farmer][ask2follow]

**Companion follows you**

![following][library]

**Various dialogues for different locations**

![Various dialogues][dialogues]

**We can save items from our repository to a companion's backpack**

![Using companion's backpack][usebag]

**If we want to break adventure, then we can release a companion**

![Release companion][release]

**Next morning we can find a package with our items we saved in companion's backpack**

![Delivered items][delivery]

**Companion with `doctor` skill can heal you if you are injured**

![Companion heal a player][heal]

More features comming soon...

## Contributors

- [purrplingcat](https://www.nexusmods.com/users/68185132) - main developer and producer
- Corashirou - author of dialogues and texts
- [RememberEmber](https://www.nexusmods.com/users/11961608) - author of dialogues and texts
- [andril11](https://www.nexusmods.com/users/68848663) - Portuguese translation
- [Reikounet](https://www.nexusmods.com/users/70092158) - French translation
- [wu574932346](https://www.nexusmods.com/users/67777356) - Chinese translation

[library]: docs/images/library.gif
[ask2follow]: docs/images/asktofollow.gif
[usebag]: docs/images/usebag.gif
[dialogues]: /docs/images/dialogues.gif
[release]: docs/images/release.gif
[delivery]: docs/images/delivery.gif
[heal]: docs/images/harveyheal.gif
