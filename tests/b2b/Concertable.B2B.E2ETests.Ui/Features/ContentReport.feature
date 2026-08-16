Feature: Report message content
  Any member who can read a message can report it for illegal or harmful content, which is the
  in-app half of the Online Safety Act reporting route.

  @VenueManager
  Scenario: A venue owner reports a message from the artist
    Given the venue owner opens their mailbox
    When the owner reports the message from "The Rockers" as "Illegal content" with details "This message contains illegal content."
    Then the owner sees the report confirmation
