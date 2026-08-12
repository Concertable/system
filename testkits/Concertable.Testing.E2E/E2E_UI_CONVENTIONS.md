# UI E2E Conventions

## Page Objects

- Named after the TSX page component they represent (`AcceptApplicationPage.cs` ↔ `AcceptApplicationPage.tsx`)
- Constructor stores the full URL as `private readonly string url` via string interpolation — no trailing slash
- Parameterised routes store the base segment; `GotoAsync` appends the id inline (`$"{url}/{id}"`)
- Elements are `private ILocator X => page.GetByTestId("...")` properties — never inline literals in methods
- Page objects contain all selector + Playwright logic; step bindings contain none

## Selectors

- `data-testid` on every SPA element a page object touches — added in the same PR as the page object
- Kebab-case, purpose-named (`opportunity-add`, `contract-flatfee-fee`) — feature prefix only on collision
- No type prefixes (`btn-`, `txt-`, etc.)
- Stripe iframe content is the unavoidable exception — use Stripe's stable element names

## Steps

- One-liners that delegate to page objects — if a step body exceeds ~5 lines, move the logic into a page object
- Scenario state via `WorkflowState` (typed, scoped) — never `ScenarioContext.Set("...")`

## Test Setup

- Use the API (`CreateAuthenticatedClientAsync`) for setup steps not under test — UI only for what you're testing
- Full click-through in the happy-path scenario proves the chain works; variants skip to the step under test via API setup
