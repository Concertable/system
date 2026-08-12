# E2E Baseline

The contract for which scenarios are **expected** to pass vs fail. `./scripts/e2e.ps1 regress` reads this file to know which scenarios to run; PR review reads it to see what changed.

<!-- ============================================================
     EDITING RULES — the regress script parser depends on these

     ALL data the parser reads lives BELOW the
       <!-- BASELINE-DATA-START -->
     marker further down this file. Anything ABOVE the marker is
     ignored, including this comment.

     Each scenario section MUST be formatted as:

         ### <Suite> <status> (<N>)

         ```text
         scenario name 1
         scenario name 2
         ...
         ```

     - Suite      : "B2B" or "Customer"
     - status     : "passing" or "failing"
     - (<N>)      : MUST equal the line count inside the fenced block
                    parser throws if mismatched
     - Fence      : MUST be ```text (not ``` alone, not ```markdown)
     - One scenario per line. No bullets, no quotes, no leading/trailing whitespace
     - Scenario name MUST exactly match the Reqnroll DisplayName
       (= the Scenario line in the .feature file, no decorations)

     UPDATE FLOW
     When `./scripts/e2e.ps1 run` shows a scenario crossing the line (now
     passes that didn't, or now fails that did), move it between
     the passing and failing fenced blocks below AND update both
     (N) counts in the headings AND update the summary table.
     ============================================================ -->

## Summary

Last reconciled: 2026-06-01 / Stripe new-card flows fixed (card-entry viewport + Customer stripe-cli webhook wiring).

| Suite | Total | Passing | Failing |
|---|---|---|---|
| B2B | 29 | 29 | 0 |
| Customer | 7 | 7 | 0 |
| **Total** | **36** | **36** | **0** |

Entire suite green. The previously-failing Stripe payment flows (3DS challenge, "new card" variants, declined-card variants) are fixed: new-card entry now fills the Stripe iframe reliably (tall viewport), and the Customer E2E AppHost now forwards Stripe webhooks via stripe-cli so ticket-purchase confirmation completes.

<!-- BASELINE-DATA-START -->

## B2B (29 total)

### B2B passing (29)

```text
New artist manager registers, signs in, creates their artist profile
New venue manager registers, signs in, creates their venue
Venue manager signs in via OIDC
Venue manager books artist on a door split
Venue manager books artist on a flat fee
Venue manager books artist on a versus deal
Artist pays hire fee upfront to book venue
Venue manager books artist on a door split with a new card
Venue manager 3DS authentication fails on door split
Venue manager completes 3DS challenge on door split
Venue manager door split card registration is declined
Venue manager declares external door takings on top of Concertable sales
Venue manager books artist on a flat fee with a new card
Venue manager 3DS authentication fails on flat fee
Venue manager completes 3DS challenge on flat fee
Venue manager flat fee attempt is declined
Venue manager books artist on a versus deal with a new card
Venue manager 3DS authentication fails on versus
Venue manager completes 3DS challenge on versus
Venue manager versus card registration is declined
Artist pays hire fee upfront with a new card
Artist 3DS authentication fails on venue hire
Artist completes 3DS challenge on venue hire
Artist venue hire attempt is declined
Owner invites a member who accepts and is then managed
Switching organization scopes member management to the chosen tenant
A venue's members share one inbox with independent read state
A visitor rejects all cookies and the choice persists
A visitor accepts all cookies
```

### B2B failing (0)

```text
```

## Customer (7 total)

### Customer passing (7)

```text
New customer registers and signs in
Customer signs in via OIDC
Customer 3DS authentication fails
Customer completes 3DS challenge
Customer purchases a ticket using a new card and views the QR code
Customer purchase is declined
Customer searches for concerts, purchases a ticket, and views the QR code
```

### Customer failing (0)

```text
```
