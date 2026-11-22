[[Yegoza Wire — Mod for SPT 4.1.6 and Fika 2.4.6]]

Allows you to purchase a coil of the "Yegoza" helical wire barrier from Prapor and deploy barbed wire obstacles on floors, in doorways, or on stairs during a raid. When needed, the wire can be rolled up and retrieved. The barrier is synced via Fika: teammates can see the deployed wire and take damage from it.

>_______

<[Key Features]>

[Trader and Item Stats.] Available from Prapor at Loyalty Level 2 for 28,000 ₽. Takes up a 2x2 slot and weighs 3.5 kg.

[Terrain Placement.] A preview mode ("ghost") lets you set the start and end points of the stretch. The line follows the slope of floors and steps. Default max length is 5 m and is configurable in the settings.

[Deployment and Roll-Up Times.] Deployment takes 5.5 seconds (you must stand still), and rolling it up takes 3 seconds.

[Physics and Damage.] The wire slows movement (preventing sprinting), deals damage to limbs, and can cause bleeding. Uses standard barbed wire sound effects from the game.

[Bot Interaction.] The obstacle affects bots just like players.

[Retrieving.] Holding the key next to deployed wire drops the coil onto the surface so you can pick it up like a regular item.

[Fika Sync.] The host broadcasts deployment state to all players in the raid; late joiners see already placed barriers.

[Localization.] The interface supports Russian and English, switchable in the F12 menu.

<<[Important Note - AI Claude was used during development. It helped tremendously with this mod. Most of the credit goes to it; I am merely the initiator of the idea who got it working after hundreds of tweaks.]>>

>_______

[Installation]

1. Download the mod archive.
2. Extract it and move the contents (the `BepInEx` and `SPT_Runtime` folders) into the SPT root directory (where `EscapeFromTarkov.exe` is located).
3. Start the server and client as usual. No extra setup is required before the first launch.

[File Locations]

[ Server component (item and Prapor offer): `SPT_Runtime/user/mods/YegozaWire/`
[ Client plugin (placement, damage, slow, Fika sync): `BepInEx/plugins/YegozaWire/`

[Multiplayer (Fika)]

[ The host installs the full archive.
[ Other players only need to copy the client plugin (`BepInEx/plugins/YegozaWire/`). The `SPT_Runtime` directory is updated only on the SPT server side.
[ Players without the plugin installed can still join the raid, but they will not see the wire, take damage from it, or be able to use the item.

>_______

[How to Use]

1. Buy the "Yegoza Helical Barrier" from Prapor (LL2) and keep it in your inventory.
2. In a raid, press [Y] to activate the green preview outline.
3. Aim at the start point and press [Y], then point at the end point and press [Y] again. A red outline means placement is not possible in that spot.
4. Stand still for about 5.5 seconds until the action bar finishes. To cancel, walk a significant distance away or press [U].
5. To retrieve the barrier, walk up to it and hold [U] for 3 seconds. Pick up the dropped coil like regular loot.

Keybinds and timings can be changed in the F12 menu.

>_______

[Settings (F12 -> Yegoza Wire)]

A detailed list of parameters and default values is provided in the English README. Main options:

[ interface language (En / Ru);
[ control keys (default [Y] and [U]);
[ deployment duration (5.5 s) and roll-up duration (3 s);
[ maximum barrier length (5 m);
[ slow multiplier (down to 0.2 of base speed);
[ damage (12 every 0.5 s);
[ bleed chance (35%);
[ wire shadow rendering (Near / Always / Off).

>_______

<[Known Limitations]>

[ Bots do not recognize the wire as an obstacle and do not try to go around it, walking straight through while taking damage and being slowed.
[ The pickup zone of a dropped coil is slightly larger than its visual model and may also shift slightly.
[ The item uses the "Bundle of wires" icon, on which the object is based.
[ Deployed wire persists only within the current raid.
[ Tested only on SPT 4.1.6 and Fika 2.4.6.
[ Mods that alter movement speed or character height may affect how the slow feels. Bots under SAIN may ignore the wire slow while sprinting.

>_______ 

[Troubleshooting]

[ Item is missing from Prapor. Check that `SPT_Runtime/user/mods/YegozaWire/YegozaWire.Server.dll` exists and verify that the server console prints `[YegozaWire] Egoza added to Prapor`.
[ Pressing Y does not start placement. Make sure the plugin is placed in `BepInEx/plugins/YegozaWire/` and "Yegoza" is in your inventory.
[ Reporting an issue. When describing a problem, attach `BepInEx/LogOutput.log` (entries related to `Yegoza Wire`).

>_______

<[Deinstalation]>

[Delete both `YegozaWire` folders from the server and client directories. Move or remove "Yegoza" from your stash beforehand—or allow the server to remove items automatically via `SPT_Data/configs/core.json` in the `"removeModItemsFromProfile": true` setting (default is `false`).

>_______

[License]

Licensed under the MIT License (see `LICENSE` for details).