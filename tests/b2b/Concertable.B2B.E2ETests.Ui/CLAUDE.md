# B2B UI E2E (Reqnroll + Playwright) — scenario authoring

Read this before adding or editing a `.feature` scenario or its steps. It exists because scenarios
kept re-driving the whole happy path through the browser just to reach the state they actually test —
minutes of wall-clock per run testing things another scenario already proved.

## A scenario tests ONE behaviour, and STARTS at the nearest already-verified state

**Never re-drive earlier stages through the UI to set up.** If the flat-fee happy path
(`post → apply → accept + pay → draft concert`) is already a scenario, a *cancel* scenario must not
post, apply, accept and pay again through the browser — it starts from an already-booked concert and
only drives **the cancel** and its assertion. Same for any "act on an existing X" scenario.

The litmus test before you write `When`/`And` setup steps: **is this step proving the behaviour named
in the scenario title, or just getting me to the starting line?** If it's getting to the starting
line and another scenario already covers it, it must be a fast-forward `Given`, not UI steps.

Anti-pattern, do not add (this is the exact thing that prompted this doc):

```gherkin
# WRONG — a cancel test that re-tests booking creation
Scenario: Venue manager cancels a flat fee booking and the escrow is refunded
  When the venue manager posts a flat fee opportunity for £500
  And the artist applies to the opportunity
  And the venue manager accepts and pays with a valid card
  And a draft concert is created            # ← all four lines are re-tested setup
  And the venue manager cancels the booking
  Then the booking is cancelled and the payment refunded
```

## Fast-forward with a `Given` backed by `SeedState` — not the UI

Setup jumps to state via **seeded data**, read straight off `fixture.App.SeedState`, with no page
navigation. The pattern already in `VenueManagerSteps`:

```csharp
[Given(@"a flat fee opportunity has been applied to")]
public Task AFlatFeeOpportunityHasBeenAppliedTo()
{
    state.ApplicationId = fixture.App.SeedState.FlatFeeApp.Id;   // no browser, no re-drive
    return Task.CompletedTask;
}
```

When the state you need doesn't exist yet (e.g. "an accepted booking with a draft concert"), **add the
seeded state + a new `Given`** — don't reach it by replaying UI steps. One seeded starting point serves
every scenario that acts from there.

## But you cannot seed payment/Stripe state — so split those assertions

Seeding obeys the same rule as production (`api/docs/SEEDING_CONVENTIONS.md`): a seeder only writes what
production writes **directly**. Payment-derived state is not one of those — real Payment emits only on
live Stripe webhooks, never for seed data. So a scenario whose assertion needs a *real* Stripe object
(e.g. "the escrow is **refunded**" needs a real PaymentIntent to reverse) genuinely has to pay through
the flow; it can't be pure-seed fast-forwarded.

Resolve this by splitting, not by re-driving everything:
- the cheap **state-transition** assertion (booking reaches `Cancelled`) starts from a seeded booked
  state via a `Given`;
- the **refund** assertion, which needs a live charge, stays on a flow that actually accepted + paid.

## Baseline discipline — `api/Concertable.Shared/tests/Concertable.E2ETests/E2E_BASELINE.md`

`./e2e.ps1 ui regress` trusts this file. Two traps:
- When a scenario crosses the line (newly passes / newly fails), move it between the `passing`/`failing`
  blocks **and** fix both `(N)` counts and the summary table — the parser throws on a mismatch.
- **Adding an assertion to an existing green scenario can silently turn it red while the baseline still
  lists it as passing** (the name didn't change). Re-run the scenario and reconcile the baseline; a name
  in `passing` is not proof the scenario's *current* body passes.

## Running (full rules in the `e2e-ui-debug` / `e2e-ui-regress` skills)

- Always via `./e2e.ps1 ui <cmd>` — its Docker health gate is mandatory every run.
- **Headless by default.** `-Headed` only when a human is watching; it does not change what's asserted.
- One behaviour per scenario; each `Then` proves the new behaviour, not the setup.
