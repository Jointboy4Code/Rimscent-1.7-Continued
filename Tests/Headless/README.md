# Headless integration tests

Test-only assembly. Never put Probes.dll into a normal installed mod. It modifies a disposable map, creates pawns, saves a test colony and exits the test game.

Build using `dotnet build RimScent.HeadlessTests.csproj -c Release -p:RimWorldDir=<game-root> -p:RimScentDir=<isolated-mod-root> -p:OutputPath=<isolated-mod-root>/1.7/Assemblies/`.
Run an isolated copy of the game using `-Headless -quicktest -NoFileLog -NoCrashDialog -DisableLogStackTraces -savedatafolder=<isolated-profile>`. Only activate Harmony, RimScent and the desired test companions in that profile. The new-game component produces map-checks.json, rimscent-checks.json, rimscent-coverage.json and Saves/rimscent-behavior.rws.
To verify reload, enable devMode in that isolated profile, copy the test save to Saves/autostart.rws, then launch without quicktest. The component produces reload-checks.json. Read the JSON pass/fail fields; exit code zero alone is not proof the assertions passed.
The C# cases evaluate real pawns and game definitions, but accelerate evaluation by calling the scent-update method directly in most cases. One case explicitly exercises the scheduled CompTick entry point. This is not a long-duration TPS or full-game benchmark.
