# Contributing to SoulsRandomizers

## Integrating with randomizer
Note there are simple ways to extend randomizer without making changes to it, and more ways to do this are planned.

The simplest way to augment the randomizer's output is to merge a mod into it. Randomizer tries to make incremental edits on top of game files, and makes additional attempts to tolerate common mod changes, like allowing emevd condition group rewrites. If randomizing something causes a mod's gameplay to break, you can put a randomizer_merge_config.yml in the mod directory before merging it. This is mainly a list of things to not randomize, but may allow providing detailed instructions to randomize modded content in the future.

## Contributions
### Licensing
From 2019 until 2026, randomizer did not permit contributions and the published source was for reference purposes only. It now does under a [Contributor License Agreement (CLA)](https://cla-assistant.io/thefifthmatt/SoulsRandomizers). This allows me to distribute and build on your contributions while you may continue to use your contributions for any other purpose.

Currently the SoulsRandomizer repository and randomizer releases are licensed under a [source-available license](LICENSE.md), whereas for a few years before that it was unlicensed. The license now grants rights to users and contributors that explicitly allow for use and contributions, but note it does have restrictions, for instance distributing randomizer releases.

As an alternative to a CLA for sufficiently standalone features, you can contribute it to a repository which randomizer depends on which is permissively licensed, which should be [SoulsRandomizers.Contrib](https://github.com/thefifthmatt/SoulsRandomizers.Contrib) by default. This may require an initial CLA-based contribution to SoulsRandomizers to set up the dependency. Definitely reach out to me first over Discord or by filing a GitHub issue to ensure such a feature is both compatible with randomizer scope *and* coherent enough to make standalone!

### Scope
For the most part, I am not seeking any contributions in particular and I am not looking for other maintainers for SoulsRandomizers. If you want a specific feature or fix to be part of the randomizer, I appreciate the interest. Please read through the guidelines below before starting. Per the license, you can also make modifications for your own private non-distributed use.

### Guidelines
First off, **do not use LLMs to create substantial pieces of code**. If you use an LLM in any capacity, no matter how minor, you must disclose how you used it and what you used it for. All Github comments and PR descriptions, including the LLM disclosure, must be written by a human in their own words.

The reason for this is because the review and maintenance burden for any contributed code is ultimately on me, and I do not have the capacity to accept that burden for code that no person can fully answer for. See [Godot's policy statement](https://godotengine.org/article/contribution-policy-2026/) on this topic which I broadly agree with.

Some other broad guidelines are as follows:
* New features must align with existing design ethos. Among other things, this means keeping the default in-game experience intuitive and functional and balanced, avoiding options clutter in favor of sensible defaults, and ensuring that quality attributes such as backwards compatibility, merge compatibility, config-driven logic, determinism, accessibility, and localization are prioritized.
* Keep contributions limited in scope to the feature at hand and at a suitable level of abstraction in the context of existing and planned future randomizer features. Avoid changing unrelated code.
* Match the style and library usage of existing code, even if it is not standard or modern C#. One example of this is preferring explicitly typed variables.
* Note that TODOs do not necessarily mark things that are ready to be done, and may be markers for complex future refactorings.

A lot of these are subjective so consider opening a GitHub issue in this repository or asking in the Discord server first, *especially* if there are new user interface or configuration elements. If a feature is not design-wise compatible with randomizer scope, it may not be accepted. If it's not code-wise compatible with abstractions or quality attributes, you may need to rewrite it. If a proper implementation is blocked by other features or rewrites, you may need to wait for those.

### Codebase state
The codebase is somewhat constrained by planned migrations and features. A few major ones to call out:
- I am migrating from WinForms to Avalonia, a cross-platform UI library similar to WPF, so new UI development should be in Avalonia as much as possible. For now, this is Avalonia 11 only, as 12 has some issues which make it incompatible with the MSBuild I use.
- Until migrating away from WinForms, keep RandomizerCommon on .NET 6.0 so that existing users don't need to reinstall a new .NET Desktop Runtime. Outside of RandomizerCommon, .NET 8.0 should be highest used version at least until it reaches the end of its support window.
- Porting features between randomizers, especially preset UIs, is a longterm goal and means that any configuration or processing that could be useful for multiple games should be implemented in a relatively game-agnostic way.
- The SoulsFormats format APIs are robust but have issues around performance and memory usage and sometimes being stringly typed, so I plan on piecewise replacing them over time.
- Mainly in Elden Ring, many enemy/item configs have automatic update flows which validate and automatically update the configs, which are planned to be made available for advanced mod merging. Direct config changes may not be usable as-is if they're not compatible with these flows.

## Building randomizer for local development
Randomizer is meant to be developed using a standard C# toolchain, with MSBuild or dotnet, and IDEs like Visual Studio and Rider should work. As of August 2026, various randomizers use .NET 6.0 (including the desktop runtime) and .NET 8.0, so you may need to install those SDKs through your IDE or from Microsoft.

The SoulsRandomizer repository must exist in the same directory as other source dependencies, because the dependencies are loaded using relative paths. This is slightly cumbersome and may cause version skew but it is a convenient setup for me, as git submodules have a lot of downsides for local development. I could switch over if I find a good workflow for it. These other repositories can be found at [SoulsIds](https://github.com/thefifthmatt/SoulsIds), [SoulsFormatsNEXT](https://github.com/thefifthmatt/SoulsFormatsNEXT) (pre-GPL), and [SoulsRandomizers.Contrib](https://github.com/thefifthmatt/SoulsRandomizers.Contrib). Finally, legacy UIs also use [yet-another-tab-control](https://github.com/thefifthmatt/yet-another-tab-control).

Adding these should be sufficient to build and run the randomizer UI from IDEs, which typically uses the binary created under `bin\Debug`. When the debug build has an ancestor directory named `SoulsRandomizers`, it will use its `dist*` directory to read files necessary for the randomizer to work, but it will output randomized files to its own directory to avoid contaminating the top-level one.

To actually do randomization, you may need files not present in any source repository because they are proprietary game files, and also big files not suitable for git. I am working towards extracting the files client-side but it's not fully there yet. For now, I'd recommend getting the files by downloading the mod from Nexus Mods and copying the directories in. This is usually the `Vanilla` and `AI` directories in the `dist*` directory. Then to launch the game using the built-in launcher, use the `ModEngine` directory in `dist*` and also the `dll` directory. These paths are excluded in .gitignore.

## Using randomizer code elsewhere
Per the license, the randomizer codebase and data files cannot be used in other works. However, I do try to freely license parts of it in separate libraries which may be useful for other mods, most notable SoulsIds. If there is a routine that would be useful to you for a different mod, I can consider splitting it out. However, for extensions of randomizer or using core randomization routines elsewhere, these can't be distributed and must be part of official randomizer releases.
