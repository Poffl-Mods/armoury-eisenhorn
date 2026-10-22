# Armoury: Eisenhorn

**Status:** Quest-system prototype 0.1.0 built and installed for local testing. Gameplay validation is pending.

This project explores story-driven progression for Eisenhorn's equipment in *Warhammer 40,000: Rogue Trader*. The first prototype contains a native journal quest, an additional Mechanics Scene with an interactive document, and a three-page Book dialogue. It activates after the prologue's out-of-control servitor encounter. No custom weapons or rewards are included yet.

- [Test walkthrough](docs/prototype-testing.md)
- [Implementation and build notes](docs/prototype-implementation.md)
- [Installation verification](docs/prototype-install-verification.json)
- [Research report](docs/eisenhorn-weapon-quest-research.md)

`src/Prototype` contains authored content/runtime sources and stable Unity metadata. `src/Editor` contains the prototype's Unity build helper. `src/TechnicalChecks` and `tools` contain read-only validation and content-generation tools. The built ZIP is local and ignored by Git.

Use the existing Rogue Trader Unity integration and Mac-ModInstaller. Installation success does not establish in-game or save/load compatibility.
