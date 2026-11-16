# LuxPresenter-Qt6 — Project Rules

## 1. Project Objective

Rebuild LuxPresenter from scratch using C++ and Qt, using the existing C# application as the primary functional and visual reference.

The new application must reproduce the reference application's:

- Features and functionality.
- UI layout, component placement, menus, and toolbars.
- User interactions and workflows.
- Data structures and persistence behavior.
- Media and presentation behavior.

**Feature parity is the primary objective.** Do not arbitrarily remove, redesign, or simplify existing functionality.

## 2. Authorized Filesystem Access

### Writable workspace
`E:\LuxPresenter-QT6`

All new project files and generated artifacts must remain inside this directory.

### Read-only reference
`E:\FreeWorship\Source`

Kiro is explicitly authorized to inspect and read all contents of this directory, including C# source code, resources, assets, and project files.

C# reference files must NEVER be modified, deleted, renamed, moved, or overwritten.

### Qt SDK
`C:\Qt\6.12.0\msvc2022_64`

Use this exact Qt installation.

### Forbidden locations

No other folders or files on drive `E:\` may be accessed.

This prohibition includes scanning, listing, searching, reading, executing, creating, modifying, or deleting files outside the two authorized E: locations above.

If an operation may access an unauthorized location, STOP and request approval.

## 3. C# Reference and Code Porting

- Read the existing C# implementation before recreating an existing feature.
- Reuse and translate existing algorithms, business logic, data handling, and workflows into C++ wherever practical.
- Port existing functionality instead of unnecessarily reinventing it.
- Adapt implementation details to appropriate C++ and Qt conventions.
- Do not mechanically reproduce WPF-specific implementation patterns when an equivalent Qt-native approach is more appropriate.
- Preserve existing behavior unless a technical limitation requires a documented change.
- Do not invent replacement functionality when the reference implementation already provides the required behavior.

The reference implementation is the source of truth for expected application behavior.

## 4. UI and Feature Parity

- Match the reference application's layout and component placement as closely as practical.
- Preserve menu organization, toolbar controls, tabs, dialogs, panels, shortcuts, and interaction behavior.
- Use English for application UI text.
- Preserve the original language of user content, lyrics, and Scripture text.
- Do not introduce unnecessary UI redesigns or experimental features.
- Verify implemented features through the actual running application.

## 5. Open-Source Assets and Dependencies

Free and open-source assets may be reused, including:

- Icons and icon sets.
- Fonts and typography resources.
- Images and visual assets.
- Qt-compatible UI resources.
- Libraries and other dependencies.

Requirements:

- Verify that the license permits the intended use and distribution.
- Prefer permissive licenses where practical.
- Preserve required attribution and license notices.
- Maintain a concise third-party license inventory.
- Do not use pirated, unauthorized, or license-incompatible assets.
- Do not spend excessive time creating custom replacements when suitable licensed assets already exist.

## 6. Scripture and Bible Data

### Built-in Scripture

- Bundle KJV only.
- Track the source and digital provenance of the bundled dataset.
- Do not claim digital provenance is verified unless it actually is.

### User-imported Scripture

- Support importing Terjemahan Baru (TB) through user-provided JSON.
- The user will provide their own TB dataset.
- Implement JSON validation and clear import error handling.
- Do not bundle TB.
- Do not hardcode TB verses.
- Do not fabricate Scripture data for testing.

Keep imported datasets in the application's designated user-data location, not in the source repository.

## 7. Project Structure and Build

- Organize the new project using the existing FreeWorship folder structure as the organizational reference wherever appropriate.
- Use C++20, Qt 6.12.0, MSVC 2022, CMake, and Ninja.
- Keep source code, build outputs, tests, temporary files, and generated artifacts inside the authorized workspace.
- Use modular, maintainable C++ code.
- Avoid unnecessary architectural complexity and excessive abstraction.

## 8. Development Workflow

Use an implementation-first workflow:

1. Inspect the relevant C# reference.
2. Implement the equivalent C++/Qt functionality.
3. Build the application.
4. Run relevant automated tests.
5. Provide a working executable for manual review.
6. Fix issues found during review.
7. Continue to the next implementation milestone.

Do not stop after every small phase to produce lengthy Markdown reports.

Documentation should be limited to essential project rules, technical decisions, usage instructions, and concise milestone summaries.

Do not claim that a feature is complete merely because its source code compiles. Integrate it into the actual application and verify its behavior.

## 9. Benchmarking Policy

The Project Owner personally controls performance benchmarking.

Kiro may:
- Prepare benchmark-ready builds.
- Implement telemetry where required.
- Fix performance issues.
- Provide build configuration and technical details.

Kiro must not:
- Invent benchmark results.
- Claim performance superiority without evidence.
- Substitute synthetic measurements for the Project Owner's actual hardware tests.
- Change benchmark conditions without approval.

Performance comparisons must use equivalent workloads and test conditions.

## 10. Portable Testing and Release Policy

### Portable
`E:\LuxPresenter-QT6\Portable`

Use this directory for executable test builds and their required runtime dependencies.

Portable builds must be independently runnable without requiring the development environment.

The Project Owner will conduct manual application testing using this package.

### Release
`E:\LuxPresenter-QT6\Release`

Reserved exclusively for production distribution:

- Setup installer.
- ZIP update patches for released versions.

Do not place intermediate builds, debug executables, temporary packages, or development dependencies in Release.

Do not create production release packages until the application is genuinely ready and the Project Owner authorizes release.

## 11. Project Owner Approval

- Kiro may implement and complete the authorized milestone sequence without requesting approval after every individual phase.
- Stop and report only when a serious blocker, security issue, licensing issue, or unauthorized filesystem operation is encountered.
- Do not start a new major milestone outside the granted authorization.
- The Project Owner performs final visual, functional, and performance reviews.
- Production release requires explicit Project Owner approval.

## Final Principle

**Build the application, not piles of documentation.**

Prioritize functional implementation, working executable builds, accurate feature parity, and verifiable results.