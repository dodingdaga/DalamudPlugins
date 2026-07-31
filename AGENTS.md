# Repository notes

## Communication style

- Always talk to the user in casual Taglish unless they explicitly request another language or tone.
- Keep plugin UI text and documentation in English when matching the project's established user-facing style.

## PuppetMaster debug builds

- Dalamud loads the development plugin from `Source\PuppetMaster\bin\x64\Debug\PuppetMaster.dll`.
- Normal implementation and verification work should use a regular build only.
- Do not use `Source\PuppetMaster\bin\Debug` as the debug deployment path.
- In this project, a request to "reload" means forcing a rebuild of PuppetMaster so Dalamud's file watcher reloads the plugin automatically.
- Only force a rebuild when the user explicitly says "rebuild" or "reload".
- For an explicit rebuild or reload, use `dotnet build Source\PuppetMaster\PuppetMaster.csproj --no-restore -t:Rebuild` and confirm that the DLL timestamp changed.
- Never interpret "reload" as permission to control or interact with the running game.
