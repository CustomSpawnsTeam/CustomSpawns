# Invasion lord resupply

An independent invasion lord whose party size ratio is at or below the army model's
minimum receives a journey to the invasion capital. The mission continues until
the party reaches `max(0.8, min(1, armyMinimum + 0.1))`, fills its available room,
or can no longer use the bank. With the normal 0.6 army minimum, that means starting
at 60% or less and releasing at 80% or more.

The capital must still belong to the live invasion, have a garrison with troops
above the configured reserve, be reachable, and be free of siege. The order ends
when any of those conditions stops holding; lords do not wait for nonexistent
stock. Army members, the player, fighting or disbanding parties, and parties with
locked movement do not receive the order.

`InvasionResupply` owns these rules and the set of active missions. It uses plain
snapshots, settlement IDs, existing configuration/garrison ports, and the narrow
`IInvasionResupplyRoutes` port. It has no Bannerlord dependency.

`BannerlordInvasionResupplyOrders` translates campaign facts, checks routes with
the game's settlement navigation helper, and adds a `GoToSettlement` score through
`AiHourlyTickEvent`. The checked navigation type and port flags also drive the
order. Normal tactical fleeing remains available. The event runs when the party
rethinks its plan, rather than on every campaign hour.

Existing settlement-entry and daily capital refill handlers perform troop
withdrawals and preserve the garrison reserve. Resupply does not generate troops
or change reinforcement timing. Once ready, the lord returns to normal military
AI and can form or join armies.

The campaign behaviour composes the policy and adapter, removes destroyed parties
from missions, and saves active party IDs under `_invasionResupplyParties`.
Older saves without that key start with no active missions; depleted lords are
evaluated on subsequent AI ticks. Mission eligibility is checked again after load.

`InvasionResupplyTests` cover thresholds, mission persistence, clan membership,
bank availability, route safety, and excluded party states. Settlement movement
and campaign save/load still need an in-game smoke test.
