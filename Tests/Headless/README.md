# Headless integration tests

Test-only assembly. Never put Probes.dll into a normal installed mod. It modifies a disposable map, creates pawns, saves a test colony and exits the test game.

Build using `dotnet build RimScent.HeadlessTests.csproj -c Release -p:RimWorldDir=<game-root> -p:RimScentDir=<isolated-mod-root> -p:OutputPath=<isolated-mod-root>/1.7/Assemblies/`.
Run an isolated copy of the game using `-Headless -quicktest -NoFileLog -NoCrashDialog -DisableLogStackTraces -savedatafolder=<isolated-profile>`. Only activate Harmony, RimScent and the desired test companions in that profile. The new-game component produces map-checks.json, rimscent-checks.json, rimscent-coverage.json and Saves/rimscent-behavior.rws.
To verify reload, enable devMode in that isolated profile, copy the test save to Saves/autostart.rws, then launch without quicktest. The component produces reload-checks.json. Read the JSON pass/fail fields; exit code zero alone is not proof the assertions passed.
The C# cases evaluate real pawns and game definitions, but accelerate evaluation by calling the scent-update method directly in most cases. One case explicitly exercises the scheduled CompTick entry point. This is not a long-duration TPS or full-game benchmark.

Controlled fixture pawns have generated traits/genes cleared and health reset before spawning; this avoids random generated conditions changing the baseline or mutating the health list during spawn. Explicit nose, trait, gene, apparel and scent-source conditions are added by their respective tests afterward. This reset exists only in the test harness, never in the production scent tracker.

Garden cases cover young/mature plants, blight, leaf loss, and mixed flower types with uncapped/stacking settings enabled. They use the installed 1.7 Plant APIs and the optional More Plants definitions.

The leafless fixture temporarily disables dieIfLeafless for its plant definition, restoring it in finally. These flowers normally die from leaf loss; the override allows the separate surviving-leafless scent filter to be asserted without changing production definitions. First-pass results are retained in Garden-first-pass in the isolated test workspace.
