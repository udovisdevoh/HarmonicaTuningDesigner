# CONTRIBUTING.md

## Guidelines

This project displays harmonica layouts and derived note lists. When adding features that modify models or pages, follow existing naming and formatting conventions and update view models accordingly.

## Project Preference

- Add `AvailableNotes` to `HarmonicaDesignerViewModel`
- Include a new partial `_AvailableNotes.cshtml` showing notes
- Compute distinct note names from current reed plates in `IndexModel`
- Update `Index.cshtml` to render the partial

These changes alter the project model and add a shared partial view.