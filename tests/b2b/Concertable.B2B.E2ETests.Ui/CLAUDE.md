# B2B UI E2E (Reqnroll + Playwright) — service-specific authoring notes

**The scenario-authoring rules that apply to every suite** — test one behaviour, start at the nearest
already-verified state, fast-forward via seeded state (never UI replay), what can't be seeded
(payment/Stripe), and baseline discipline — live in `E2E_CONVENTIONS.md`, imported here:

@../../../../docs/E2E_CONVENTIONS.md

This file only adds the B2B-specific mechanics.

## Fast-forward `Given`s read `fixture.App.SeedState`

The pattern in `VenueManagerSteps` — jump to state via seeded data, no browser:

```csharp
[Given(@"a flat fee opportunity has been applied to")]
public Task AFlatFeeOpportunityHasBeenAppliedTo()
{
    state.ApplicationId = fixture.App.SeedState.FlatFeeApp.Id;   // no navigation, no re-drive
    return Task.CompletedTask;
}
```

Seeded applications exist in the **applied** state (`FlatFeeApp` / `DoorSplitApp` / `VersusApp`). If a
scenario needs a *later* starting point (an accepted booking with a draft concert), **add the seeded
state + a new `Given`** — do not reach it by replaying `post → apply → accept → pay` through the UI.

Anti-pattern that prompted this doc — a cancel test re-testing booking creation:

```gherkin
# WRONG
Scenario: Venue manager cancels a flat fee booking and the escrow is refunded
  When the venue manager posts a flat fee opportunity for £500
  And the artist applies to the opportunity
  And the venue manager accepts and pays with a valid card
  And a draft concert is created            # ← all four lines are re-tested setup
  And the venue manager cancels the booking
  Then the booking is cancelled and the payment refunded
```

The cancel + `Cancelled` transition should start from a seeded booked state via a `Given`. The
**refund** assertion needs a real charge to reverse (can't be seeded — see the shared doc), so it stays
on a flow that actually accepted + paid; split the two rather than re-driving everything.
