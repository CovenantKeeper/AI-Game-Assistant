# Suggested Maintenance Tasks

## Fix a Typo in Success Log Messages
* **Issue**: Both `ChatGPTClient` and `GeminiClient` log strings say "Verify if it's valid C# cod" (missing the final "e"), which is a visible typo in the editor console.
* **Files**: `Assets/TheCovenantKeepers/AI_Game_Assistant/Scripts/ChatGPTClient.cs`, `Assets/TheCovenantKeepers/AI_Game_Assistant/Scripts/GeminiClient.cs`.
* **Proposed Task**: Update the success log messages to spell "code" correctly and remove any stray placeholder text.

## Fix Backtick Trimming Bug in Chat Responses
* **Issue**: `ChatGPTClient.GenerateScriptAsync` removes the leading "```csharp" fence with `Substring(7)`, leaving "rp" at the start of responses because "```csharp" is nine characters long.
* **File**: `Assets/TheCovenantKeepers/AI_Game_Assistant/Scripts/ChatGPTClient.cs`.
* **Proposed Task**: Correct the substring length (and mirror the change in `GeminiClient` if necessary) so code fences are fully stripped.

## Align Documentation Comment with Current Asset Loading
* **Issue**: `ChatGPTClient.cs` still tells users to keep the settings asset in a `Resources` folder, but `ChatGPTSettings.Get()` now searches the project and even migrates assets out of the old path.
* **Files**: `Assets/TheCovenantKeepers/AI_Game_Assistant/Scripts/ChatGPTClient.cs`, `Assets/TheCovenantKeepers/AI_Game_Assistant/Scripts/ChatGPTSettings.cs`.
* **Proposed Task**: Update the comment to describe the actual lookup/migration behaviour so documentation matches the code.

## Flesh Out the Placeholder Test Script
* **Issue**: `Editor/TestSpawnerCommands.cs` is an empty file (0 bytes), so there are no automated checks for the test spawner utilities.
* **File**: `Assets/TheCovenantKeepers/AI_Game_Assistant/Editor/TestSpawnerCommands.cs`.
* **Proposed Task**: Implement meaningful editor tests (or remove the placeholder) to cover spawning commands and guard against regressions.
