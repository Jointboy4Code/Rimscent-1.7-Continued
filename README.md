# RimScent - Continued

Local continuation of https://github.com/ocarina0001/rimscent under its MIT licence. Original credits and identifiers retained. Branch: `jointboy/1.7-compatibility`. Nothing pushed or published.

## Build

`dotnet build Source/RimScent.1.7.csproj -c Release -p:RimWorldDir="C:/Users/dalet/OneDrive/Documents/RimWorld"`

Targets the locally installed RimWorld 1.7.4984 rev1242 (.NET 10), UnityStubs and Harmony 3.0.0-rc.3. Game assemblies are references only, never copied into the mod. The original 1.6 assembly is retained; 1.7 has a separately compiled assembly and versioned definitions.

## Compatibility

- Preserves `reo.RimScent`, original assembly/class names, thoughts, traits, genes and settings for save and expansion compatibility. Enable this instead of the original, never both.
- Retains upstream optional integrations including Dubs Bad Hygiene and shipped Vanilla Expanded patches. Appropriate load-after metadata accompanies optional patch targets.
- Adds Alpha Crafts: perfume and soap wearer hediffs, and eleven harvested flower/vanilla products, use the original floral scent. Stored perfume bottles and candle stockpiles do not emit scents.
- Adds Lovin Is Messy: `lim_Filth_LoveJuice` emits a mild -2 stale bodily fluids scent. Existing RimScent sensitivity, traits, radius, line-of-sight and strongest-scent rules apply.
- New patches guard missing targets and preserve existing scent extensions.
- Skunks - Continued already applies its own wearer/nearby mood penalties, so no duplicate RimScent penalty is added. DBH Expanded defines no separate filth/odor hediffs requiring new mappings in the inspected source.
- Other mods can declare `RimScentReworked.ModExtension_Scent` on ThingDefs and HediffDefs; RimScent discovers these after definitions load. Arbitrary item names do not automatically generate smells.
- Removes dangling RBSE/EPOE load-folder entries from the 1.7 configuration: those directories do not exist upstream. General nose capacity handling remains.
- Fixes upstream malformed French colour markup and a null-check ordering issue in the pawn tracker.
- Original perfume/incense expansion binaries are not ported or certified here. Their package dependencies can still find the retained RimScent ID.

## Validation

Release build: zero warnings/errors against the installed 1.7 game. All 98 XML files parse, all 23 versioned 1.7 load folders exist, and all 14 new scent targets and their ThoughtDefs resolve against inspected mod definitions. New patches were checked for idempotence with a representative merged definition document.

In-game startup, save loading, XML inheritance/patch ordering and mood behavior still need testing. Existing upstream integrations are retained, not fully runtime-certified. No claim of complete compatibility with every installed mod.

The original Workshop ID is removed to prevent accidental uploads to the original listing. A separate GitHub fork and Workshop release remain unpublished.

## Headless regression testing (28 October 2026)

Confirmed native Waylith headless startup with exit code 0. The isolated runtime harness passes 24/24 checks, including settings, human tracker attachment, scent definitions and trait/gene helper behavior. Fixed both trait-degree helpers to test each actual pawn trait using Verse.TraitRequirement.Matches instead of treating a non-null requirement list as a match. Tests cover empty lists, missing traits, wrong degrees, matching degrees and unspecified degrees. This is not a complete pawn-on-map or save/reload certification.

## Pawn behavior fixes (28 October 2026)

The focused headless suite now includes 24 startup/helper assertions, 31 pawn/map assertions, and 3 save/reload assertions. Fixed stale smell memories and dominance after removing sources, stale sensitivity/memory age, uncapped-mode cleanup and switching back to capped mode, source-specific anosmia (immunity rather than multiplying mood penalties), the combined keen/dysosmic mood multiplier, overridden genes and suppressed traits, and independent slave/colonist eligibility. Scent effects now synchronize to currently detectable sources at the configured check interval.

Regression harness source is in Tests/Headless. It is test-only and is not installed in normal game mod copies. A native CLR/MonoMod crash occurred during one isolated reload attempt; repeating the same save completed successfully. That runtime-level failure has not been established as a RimScent defect or repaired. Full-pack results are recorded separately from focused-mod results.

## Full active-pack verification

The 1,455-package active snapshot passed 61/61 RimScent assertions: 24 startup/helper checks, 34 pawn/map checks (including Alpha Crafts lavender/perfume and Lovin Is Messy filth), and 3 save/reload checks. Both the generated-colony run and the saved-colony reload exited with code 0. All loaded humanlike pawn races had a scent tracker; 70 ThingDefs and 4 HediffDefs declared resolved scent sources.

This is a controlled scent integration test, not a certification of all installed mods or a long-duration simulation. The pack also logged errors in other mods. One initial full-pack attempt timed out during texture loading; a subsequent attempt completed. An earlier focused reload had a native CLR/MonoMod JIT crash; repeating the same save and then the full-pack reload passed. That native failure's cause has not been established or fixed.

## Brewing aromas

Added 21 guarded item mappings for Vanilla Brewing Expanded and its Coffees and Teas addon: coffee aroma (+2), tea aroma (+1), and mint aroma (+1). Each aroma stacks once and follows the existing radius, sensitivity, immunity and dominant-scent rules. The mappings cover prepared coffee/tea, coffee beans, tea leaves, and mint leaves; machines, growing plants and iced coffee are not mapped in this batch.

The new optional `onlyFresh` scent-extension field prevents these pleasant aromas from rotting/desiccated items that have a CompRottable. It defaults to false for backward compatibility with existing extensions. Both mod package IDs are optional load-after entries in About/About.xml and conditional LoadFolders entries.

Brewing validation: 38/38 pawn-map checks and 24/24 startup/helper checks passed with the 1,455-package snapshot; process exit code 0. New cases cover brewed coffee, masala chai, mint leaves, and removal of aroma from rotting coffee beans. This batch did not repeat the separate save/reload suite.
