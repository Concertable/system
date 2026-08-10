Feature: Cookie consent
  A first-visit PECR/UK-GDPR consent banner shows on every SPA before any
  non-essential cookie is set; the choice persists and stays re-openable.

  @CookieConsent
  Scenario: A visitor rejects all cookies and the choice persists
    Given a visitor is on the business home page
    Then the cookie consent banner is shown
    When they reject all cookies
    Then the cookie consent banner is dismissed
    And no non-essential cookies are stored
    And the stored consent denies every optional category
    When they reload the page
    Then the cookie consent banner is dismissed
    When they open cookie preferences from the footer
    Then the cookie preferences dialog is shown

  @CookieConsent
  Scenario: A visitor accepts all cookies
    Given a visitor is on the business home page
    Then the cookie consent banner is shown
    When they accept all cookies
    Then the cookie consent banner is dismissed
    And the stored consent grants every optional category
