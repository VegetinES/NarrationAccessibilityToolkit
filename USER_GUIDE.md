# NAT User Guide

This guide explains how to install NAT in Unity, how to set it up in a scene, and how the narration flow works in practice.

## 1) What NAT Does

NAT (Narration Accessibility Toolkit) helps Unity projects provide spoken feedback for:

- focused UI controls
- changing HUD values
- gameplay events and announcements
- localized text keys or literal text

## 2) Compatibility Status

Currently validated:

- Unity 6.3 (`6000.3`)
- Windows with NVDA support, falling back to the current system narrator speech path
- Android with system TTS

Expected but not validated yet:

- iOS
- Linux

## 3) Requirements

- Unity 6.3 (`6000.3`)
- Input System package enabled in the project
- UGUI package available in the project

TextMeshPro is supported via reflection, so hard compile-time TMP references are not required by NAT.

## 4) Installation (Assets/NAT format)

### Option A: Full repository zip

1. Download the repository zip.
2. Extract it at your Unity project root.
3. Confirm this path exists:

```text
Assets/NAT
```

4. Open Unity and wait for script import/compile.

### Option B: Copy only NAT folder

1. Copy the folder:

```text
Assets/NAT
```

2. Paste it into your target Unity project's `Assets/` folder.
3. Open Unity and let it reimport.

## 5) First Scene Setup

Use this flow for a clean first integration.

### Step 1: Create manager objects

In Unity menu:

```text
Tools > Narration Accessibility Toolkit > Create Narration Manager
```

This creates and wires:

- `NarrationI18nSource`
- `NarrationManager`
- `NarrationInputToggle`

### Step 2: Tag current UI

In Unity menu:

```text
Tools > Narration Accessibility Toolkit > Add NarrationElement To Scene UI
```

This adds `NarrationElement` to supported controls.

### Step 3: Configure narrator toggle input

`NarrationInputToggle` listens to one Input System action.

Default values:

- Action Map: `UI`
- Action Name: `ToggleNarrator`

If your project has no matching action, enable fallback and set `Fallback Binding Path`, for example:

```text
<Keyboard>/f1
<Keyboard>/n
<Gamepad>/select
```

### Step 4: Handle runtime-generated UI (optional)

If UI is spawned at runtime:

- Add `NarrationAutoTagger` to your canvas, or
- Add `NarrationElement` by code when UI objects are created

### Step 5: Add dynamic value narration (optional)

Use `NarrationLiveRegion` for values that change without focus movement:

- health
- score
- currency
- objective text

## 6) How NAT Works Internally

A typical speech flow is:

1. Your game calls `Narrator`.
2. `NarrationManager` decides enabled state, queueing, and language.
3. Text is resolved through i18n if needed.
4. `SystemNarrator` sends speech to platform-specific implementation.

### Speech modes

Use `Interrupt` when the new message is more important than current speech.

Examples:

- focus changes
- critical warnings
- menu navigation

Use `Queue` for non-critical updates.

Examples:

- score increments
- passive status updates
- background progression messages

## 7) Core Components

### NarrationManager

Central runtime service for:

- enable/disable state
- language selection
- queue handling
- i18n resolution

### NarrationElement

Attach to UI objects and define what gets spoken:

- text before
- main text/value
- role hint (`button`, `toggle`, etc.)
- text after

### NarrationAnnouncement

Event-driven narration not tied to focus.

Good for:

- screen titles
- checkpoint/save confirmations
- tutorial prompts

### NarrationLiveRegion

Monitors changing values and announces updates.

### NarrationAutoTagger

Scans and tags compatible UI objects automatically.

### NarrationInputToggle

Maps one input action to narrator on/off.

## 8) Scripting API Examples

Namespace:

```csharp
using NarrationAccessibilityToolkit;
```

Speak text:

```csharp
Narrator.Speak("nav.menu");
Narrator.SpeakLiteral("Door locked");
```

Formatted messages:

```csharp
Narrator.SpeakFormat("hud.coins", coins);
Narrator.SpeakFormatQueued("hud.score", score);
```

Values and state:

```csharp
Narrator.AnnounceValue("hud.health", health);
Narrator.AnnounceState("settings.music", true);
```

Global control:

```csharp
Narrator.SetEnabled(true);
Narrator.SetLanguage("en-US");
Narrator.Stop();
```

## 9) Localization (i18n)

NAT reads JSON dictionaries from:

```text
Assets/NAT/Resources/NarrationI18n/es.json
Assets/NAT/Resources/NarrationI18n/en.json
```

Nested JSON is flattened into dotted keys.

Example JSON:

```json
{
  "nav": {
    "menu": "Main menu"
  },
  "hud": {
    "coins": "Coins {0}"
  }
}
```

Generated keys:

```text
nav.menu
hud.coins
```

If a key is missing, NAT falls back to the original text.

## 10) Platform Notes

- Windows: NVDA can be used when preferred and available; NAT falls back to the current system narrator speech path otherwise.
- Android: uses `android.speech.tts.TextToSpeech`.
- iOS: expected path available, pending full validation.
- Linux: expected path available, pending full validation.

## 11) Troubleshooting

### Nothing is spoken

- Confirm a `NarrationManager` exists in scene.
- Confirm narrator is enabled.
- Confirm your platform has available voices/TTS service.

### Toggle input does nothing

- Confirm `NarrationInputToggle` exists.
- Verify action map/name configuration.
- If using fallback, validate binding path syntax.

### A UI element is not narrated

- Add `NarrationElement` manually to that object.
- Verify EventSystem focus reaches it.
- For icon-only controls, define text/hint explicitly.

### Runtime-created UI is silent

- Add `NarrationAutoTagger` to the active canvas.
- Enable continuous scan if objects are created later.

## 12) Manual Validation Checklist

After changes, run this basic smoke test:

1. Unity compiles cleanly.
2. Manager creation menu works.
3. Narrator toggle on/off works.
4. `Narrator.Speak("nav.menu")` resolves and speaks.
5. `Narrator.SpeakLiteral("test")` speaks literal text.
6. Focused UI controls are narrated.
7. Slider/toggle values are narrated correctly.
8. Live region announces value changes.
9. AutoTagger tags dynamically created UI.
10. i18n JSON still parses.

Optional JSON check in PowerShell:

```powershell
Get-Content .\Assets\NAT\Resources\NarrationI18n\es.json -Raw | ConvertFrom-Json | Out-Null
Get-Content .\Assets\NAT\Resources\NarrationI18n\en.json -Raw | ConvertFrom-Json | Out-Null
```

## 13) Feedback That Helps Most

When reporting issues, include:

- Unity version
- platform/device
- exact reproduction steps
- expected vs actual behavior
- logs and screenshots/video if possible

That context makes fixes much faster.
