# TODO

- Make a migration file that will import old settings (from the old puppetmaster) and put them in the new settings so you don't lose them when going from old to new version
- Add the text input "Test input" back again and also add it inside of whitelisted players with their specific settings
- Add a boolean "strict player name check" for whitelist/blacklist to make a strict check (exact match instead of partial match)
    - Issue, when in party, player name looks like this : "1 FirstName LastName", "2 FirstName LastName" ...
    - Solutions : Check the channel type, if party, remove first char etc
- Command blacklist and possibly command whitelist (blacklist /fc and /say for exemple)
