# Code style

How C# in this repository should be laid out. This is about breathing room, not naming or design;
it exists because dense code hides the decisions inside it.

## Braces are never optional

Every `if`, `else`, `foreach`, `for` and `while` gets braces, and its body goes on its own line —
even when the body is a single `return`, `continue` or call.

```csharp
// no
if (party == null) return null;
if (!donorInBattle) ready.Add(capture);

// yes
if (party == null)
{
    return null;
}

if (!donorInBattle)
{
    ready.Add(capture);
}
```

This is about statements. Expressions keep their compact forms: expression-bodied members,
ternaries, null-coalescing and LINQ chains are single expressions and stay as they are.

```csharp
private InvasionData? GetConfiguration(InvasionGroupState group) =>
    _spawns.FindByPartyTemplateId(group.SpawnTemplateId)?.Invasion;

private static string SkipReason(InvasionReinforcementOutcome outcome) =>
    outcome == InvasionReinforcementOutcome.MaxConcurrentPartiesReached ? "max_concurrent" :
    outcome == InvasionReinforcementOutcome.NoLandingSite ? "no_held_port" : "configuration_or_clan";
```

## A method is a few paragraphs, not one

Separate semantic blocks with a single blank line. A block is a step of the method's story: look
something up, refuse the cases you cannot handle, do the work, report it. Two consecutive blank
lines are never needed; if a method needs three or four paragraphs to be readable, that is fine,
and if it needs eight it wants splitting.

The usual shape:

```csharp
internal void Something(...)
{
    <look up what this step needs>

    <guards that refuse, each with braces>

    <the work>

    <log or return the result>
}
```

`InvasionKingdomRetirement.DiscontinueKingdom` is the reference for this: two paragraphs, each
doing one thing, and you can see where one ends.

## Keep a local next to the block that uses it

Do not hoist every declaration to the top of the method. A value is introduced in the paragraph
that needs it, so the guard that rejects it sits right underneath.

## A comment opens the block it explains

Put the comment on the line above the block it is about, with a blank line before the comment
(unless the block is the first thing in the method). A comment glued to the end of a previous
paragraph reads as a footnote to the wrong statement.

## A field is named for what it holds

A private field holding a collaborator takes its type's name, camelCased. An interface drops the
leading `I`.

```csharp
// no
private readonly SpawnDao _spawns;
private readonly DynamicSpawnData _parties;
private readonly IInvasionReinforcementQuery _campaign;

// yes
private readonly SpawnDao _spawnDao;
private readonly DynamicSpawnData _dynamicSpawnData;
private readonly IInvasionReinforcementQuery _invasionReinforcementQuery;
```

A shortened name reads like a concept or a collection — `_spawns` sounds like a list of spawns,
`_correlation` like a number — and you have to go back to the declaration to find out what the
thing actually is. The type already answers that, so use it.

This is for fields holding a named collaborator. Primitives, flags, delegates and collections have
no useful type name and keep a descriptive one: `bool _initialized`, `Func<int, int> _randomIndex`,
`Dictionary<string, bool> _coastalBySettlementId`. Locals and parameters are unaffected; they are
read next to their own declaration.

## Before and after

`InvasionSieges.ObserveOwnerChange` as first written — four unrelated steps with no seam between
them, and five one-line guards you have to read character by character:

```csharp
InvasionFoothold? previousFoothold = _state.FindFoothold(settlement.StringId);
_state.ObserveOwner(settlement.StringId, settlement.OwnerClan?.StringId);
if (previousFoothold != null && _state.FindFoothold(settlement.StringId) == null)
    Logger.Info("Invasion coastal foothold lost: group=" + previousFoothold.GroupId + " town=" + settlement.StringId);
if (!settlement.IsTown || detail != ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege) return null;
MobileParty? party = capturer?.PartyBelongedTo;
if (party == null) return null;
```

The same code, with the seams put back:

```csharp
InvasionFoothold? previousFoothold = _state.FindFoothold(settlement.StringId);
_state.ObserveOwner(settlement.StringId, settlement.OwnerClan?.StringId);

if (previousFoothold != null && _state.FindFoothold(settlement.StringId) == null)
{
    Logger.Info("Invasion coastal foothold lost: group=" + previousFoothold.GroupId +
                " town=" + settlement.StringId);
}

if (!settlement.IsTown || detail != ChangeOwnerOfSettlementAction.ChangeOwnerOfSettlementDetail.BySiege)
{
    return null;
}

MobileParty? party = capturer?.PartyBelongedTo;

if (party == null)
{
    return null;
}
```

## Scope

New and edited code follows this. Reformatting a file nobody is otherwise touching is not a
change worth making on its own.
