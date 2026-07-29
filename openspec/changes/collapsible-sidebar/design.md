# Design: collapsible-sidebar

## D1 — The token has to become real before it can be used

`tokens.ts` binds `sidebarWCollapsed` to `var(--sidebar-w-collapsed)` and nothing defines that variable.
The design contract's own rule is that *the runtime adapter binds names, not copied values* — so a name
bound to nothing is the adapter keeping its half of a bargain the CSS never kept.

So this change defines both variables in the design system's CSS, at 280px and 64px, and uses them for
the shell's width. 64px because that is what the token entry was named for and it is the width an icon
rail needs — a 40px icon with the shell's gutter either side.

Choosing the value here rather than treating it as pre-decided matters for honesty: had the value
existed, this slice would be "use it"; it does not, so this slice owns the decision and says so.

## D2 — Collapsed is a rail, and that is a rule rather than a taste

The design contract already requires that navigation and the inbox count stay reachable when the shell
folds at phone width. The reason it gives is not about phones — it is that a person cannot navigate from,
or be warned by, a panel that is not there.

Collapsing at desktop width raises exactly the same question, so it gets the same answer: every entry
keeps its icon and its destination, and the inbox count survives as a marker on its icon. A hidden panel
would trade the ambient count UC-026 exists for against a few hundred pixels, which is the wrong side of
that trade.

The control appears only from the medium breakpoint up. Below it the sheet menu already *is* the
collapsed state, and offering a second collapse would mean two mechanisms for one idea.

## D3 — Two occurrences is when the pattern graduates

The issue expected three remembered preferences and there is one. That changes the size of the
extraction, not whether to do it: this repository's own rule is that a pattern graduates on the **second**
occurrence, and this is the second.

So one hook — read lazily, write defensively, degrade to the default — and `ProjectScreen`'s view toggle
moves onto it with no behaviour change. Its existing care is the specification for the hook rather than
something to reinvent: a blocked or absent `localStorage` must cost the preference and never the
interaction, which is why the read is lazy and the write is wrapped.

Moving the existing call site is part of the change rather than a follow-up, because a shared hook that
one of its two candidates does not use is not shared — it is a third copy.

## D4 — A label that is off screen still has to exist

A collapsed entry shows an icon. An icon is not a name, so the name moves to where a name can still be
found: an accessible label on the control, and a title on hover for sighted users who have forgotten
which glyph is which.

This is not decoration. Without it the collapsed rail is navigable only by people who have already
memorised it, which makes the feature a cost for everyone else.

## D5 — What is deliberately not fixed

`tokens.ts` has other entries bound to variables nothing defines; this change wires up only the two it
needs. A sweep is worth doing and is its own item, because the interesting question there is *which*
dangling names should gain values and which should be deleted — and answering it well means looking at
every one, not at the two that happened to be in the way today.
