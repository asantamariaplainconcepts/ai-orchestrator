# Design: adopt-foundations

## D1 — Adopt the foundation, not the whole stack

Foundations' principles transfer whole (established libraries, shadcn unwrapped, share only what
repeats); its stack defaults transfer where they pay. Tailwind and the theme are the foundation;
Next.js is a delivery choice our DEC-009 already made differently, and the theme — deliberately
tokens-only — does not care.

## D2 — Migration by replacement, one screen per change

Both CSS systems load side by side; a screen is either kit or shadcn, never both. The projects
list goes first because it is the smallest screen that exercises everything a migration needs:
a list, a form, states, pills. Its diff becomes the recipe the dashboard slices follow. A
big-bang restyle would couple every screen's risk into one PR.

## D3 — The validator survives the kit it was built for

The three-stage design gate's job was never "our kit" — it was "no visual decision outside the
token source". The source moves (theme.css), the job stays: no raw hex, no raw px, no
non-approved font in app code. What retires with the kit is only the DESIGN.md generator over
kit classes.
