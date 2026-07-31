Feature: Group inbox
  A venue's members share one inbox over the venue's conversations. Each member tracks their own
  read state, so one member reading a message does not clear it for another.

  @VenueManager
  Scenario: A venue's members share one inbox with independent read state
    Given the venue owner opens their mailbox
    Then the mailbox shows a message from "The Rockers"
    And the owner has no unread messages
    When a colleague of the venue signs in and switches to the venue organization
    Then the colleague has 1 unread message
    When the colleague opens their mailbox
    Then the mailbox shows a message from "The Rockers"
