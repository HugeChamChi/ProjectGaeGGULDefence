# Three-column unit upgrade concept

Open `UnitUpgradeUI.unity` and enter Play. `Legacy/DroneUpgradeConcept2.unity` retains the earlier concept. The current project prefab is `Prefabs/UnitUpgradeUI.prefab`.

- All units form one continuous three-column list below the reference artwork. No deck-selection cards or deck popup remain.
- Units belonging to the same deck stay consecutive, with their chief first. Decks continue in the next available cell.
- Within the same scroll panel, owned units come first with no section heading. A rectangular “보유하지 않음” divider follows, then unowned units start on a fresh three-column row. Owned-only filtering hides both the divider and unowned cells.
- Drone contains Alphan and five existing units. Frog and knight each have three locked placeholder cells, not real unit data.
- Owned units open the existing information/upgrade popup directly. Alphan keeps Spine idle.
- Owned count uses bold gold text; the right-aligned owned-only toggle filters unowned cells.
- Drone cards share metal corner trims, amber indicators and a large combat-drone badge overlapping the upper-left frame. Reference: Downloads/Screenshot_20261016_232423_Lucky_Defense.jpg. Unit backplates use saturated blue; chiefs use gold with double side rails and a crown-shaped upper crest. The list panel uses warm brown. Cost gauges and their numbers use centered geometry, including the chief and detail popup.
- Drone badges sit slightly inset from the upper-left frame like tags. Chief text badges are removed. The information popup uses the second-newest download reference, `Screenshot_20261016_200040_Lucky_Defense.jpg`: game-theme navy shell and dark teal information and ability panels, a selected-unit frame at left, name/level and affordability gauge above right-aligned stat rows. The compact upgrade button shows required gold only, without an affordability slider.
- Stats use a parent panel with separate attack and attack-speed rows. Each row has its own inset icon slot and right-aligned value. The selected chief portrait uses the same gold rails, crest, colors and drone trim as the collection frame. Detail frames fill most of the information panel height; the unit stage retains its original floating placement above the dialog. Only portraits inside the information frames are lowered.
- Existing local preview progression is reused. Account ownership is not connected.

`DeckSelectorConcept.cs` reparents existing cards and live level/cost displays into the new collection. The original upgrade screen code is unchanged.
Authoring: `Tools/verification/DeckSelectorConceptAuthoring.cs`. Checks and captures: `DeckSelectorConceptChecks.cs`.
Verification and screenshots: `outputs/deck-selector/`.
