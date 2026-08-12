# E2E Test Considerations

## Do not add timeouts to fix failures

Increasing timeouts (WaitUntilSavedAsync, checkout-awaiting, etc.) does not fix anything — it just makes
tests slower and masks the real problem. If something is timing out, find out why it is not responding,
not how long to wait for it.

## Tests must pass in isolation

If a test only passes when the full suite runs before it, it has a test isolation problem. Do not paper
over this by running the full suite. Fix the underlying dependency (cold app, shared Stripe state, DB
state left over from a previous scenario).

## Stripe test card authentication persists across runs

Card `4000002500003155` (the 3DS off-session card) only requires 3DS **once per PaymentMethod**. After the
first successful `setup_future_usage=off_session` authentication, Stripe never challenges it again — even
in subsequent test runs. This means:

- `CompleteChallengeAsync` (always waits 30 s for the iframe) will time out on all subsequent runs once
  the card is authenticated.
- `CompleteChallengeIfRequiredAsync` (5 s optional check) is the correct approach for saved-card verify
  flows because the 3DS requirement is genuinely non-deterministic across runs.
- The dedicated 3DS scenarios ("completes 3DS challenge on door split/versus") always use
  `PayWithNewCardAsync(StripeCards.Requires3ds)` — a fresh card that always triggers 3DS. That is where
  deterministic 3DS coverage lives.

The root fix is to provision a fresh Stripe test customer per run (via the AppFixture, not the seeder)
so the card's off-session state is always clean. Until that is done, `CompleteChallengeIfRequiredAsync`
is the pragmatic stopgap.

## checkout-awaiting timing out means the webhook did not arrive in time

If `GetByTestId("checkout-awaiting")` times out, the webhook from Stripe has not been processed yet.
The correct fix is to investigate why the webhook is slow (stripe-cli forwarding, backend processing),
not to extend the wait timeout.
