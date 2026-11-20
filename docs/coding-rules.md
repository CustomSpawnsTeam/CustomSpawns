# Coding rules

How C# in this repository should be designed. Layout (braces, paragraphs, comments, field names)
lives in [code-style.md](code-style.md); this is about where code goes and how its parts meet.

## Hexagonal: the domain never sees Bannerlord

Game rules live in plain classes that take and return plain data: ids as `string`, counts as
`int`, small `readonly struct`s such as `InvasionWarband` or `InvasionClanPurse`. Everything that
touches `TaleWorlds.*` lives in an `Adapters/` folder (`Invasions/Coalitions/Adapters`, `AI/Adapters`) and
is reached through an interface the domain owns.

Group a large area by feature: each feature is a folder and namespace with its domain classes at
its root and its adapters in its own `Adapters/` (`Invasions/Conquest`, `Invasions/Conquest/Adapters`).
The area's root keeps only what every feature shares, such as `InvasionState` and the campaign
behaviour that wires the features together. Tests mirror the same folders.

- A port interface carries no TaleWorlds type, not in a parameter, not in a return value.
  `IInvasionWarbands.FindIn(string settlementId)` is right; `FindIn(Settlement settlement)` is not.
- The adapter does the translation: it finds the `Settlement` or `MobileParty` by id, reads what
  the domain asked for, and hands back plain data.
- A decision belongs in the domain even when the adapter could make it. The wage fallback
  (`FindGarrisonWagePerMan(capital) ?? FindAverageWage()`) is a rule, so the adapter exposes both
  numbers and the use case chooses.

## An interface is a set of methods

An interface exists to let a use case ask for something or have something done. It names
behaviour (`HasGarrison`, `DrawFromGarrison`, `Give`) and is not a bag of data properties. Data
travels in structs; behaviour sits behind interfaces.

Split an interface by who uses it. If one use case only deposits and another only reads, they get
two interfaces (`IInvasionGarrisons`, `IInvasionGarrisonDeposits`), even when one adapter
implements both.

## Small classes, one reason to change

A class stays under about 200 lines and does one job. When a change pushes it past that, or adds a
second job, split it before adding more: one class per responsibility, each injected through the
composition root. An adapter that keeps a daily update, a siege preference, alliances and a save
format is four classes, not one.

The SOLID principles apply:

- **Single responsibility.** One reason to change per class. A rule holder holds one family of
  rules: target choice, landing sites and siege scores are separate holders.
- **Open/closed.** Extend behaviour with a new class or a decorated `GameModel`, not by growing a
  `switch` or a flag in an existing one.
- **Liskov substitution.** An implementation honours the whole contract of its interface. A fake
  in a test behaves like the adapter it stands in for.
- **Interface segregation.** Split interfaces by the use case that calls them (see below).
- **Dependency inversion.** Use cases depend on ports the domain owns, never on adapters or
  `TaleWorlds.*` types (see above).

Splitting is a refactor: it moves code without changing behaviour, so the existing tests pass
unchanged. Do it as its own step, before or after a behaviour change, never mixed into one.

## No public static methods

Collaborators are objects, created in the composition root (the campaign behaviour) and passed in
through constructors. A rule holder such as `InvasionTroopBank` or `InvasionWarChest` is a
`sealed class` with instance methods, injected where it is used, not a static class called by
name. That keeps every dependency visible in the constructor and replaceable in a test.

- `private static` helpers are fine: they are implementation details of their own class.
- Public statics the framework requires are exempt: Harmony patch methods,
  `[CommandLineArgumentFunction]` cheats, extension methods and the entry point.
- Do not add new public static members. Remove existing ones in code you are already changing,
  not as a drive-by.

## No `internal`

Types and members are `public` or `private`. `internal` hides nothing that matters in a mod and
gets in the way of tests and of other modules.

## Prefer the game's extension points over Harmony

Before writing a patch, look for a Bannerlord seam: a `GameModel` to decorate (as
`InvasionArmyManagementModel` decorates `ArmyManagementCalculationModel`), a campaign event, or a
`CampaignBehaviorBase`. A Harmony patch is the last resort, for behaviour vanilla offers no hook
for.

## Every rule has a unit test

Use cases and rule holders are tested in `CustomSpawns.Tests` (NUnit + Moq, net472) through their
ports, with mocks or small hand-written fakes standing in for the adapters. A behaviour change or
a bug fix comes with the test that pins it down.

```sh
dotnet test CustomSpawns.Tests/CustomSpawns.Tests.csproj -p:BuildForWindows=false -p:BuildForWindowsStore=false
```

## Scope

New and edited code follows this. Converting untouched code to these rules is a refactor of its
own, done when asked, not slipped into an unrelated change.
